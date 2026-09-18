// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// CreateVmHandler — VM-specific provisioning flow. Resolves the
// Flavor / Image catalogs + Config overlay into a final
// VmRuntimeConfig, validates it, pins the target node via the
// placement scheduler, persists the Workload row, and enqueues a
// `workload.create` NodeCommand for the assigned NodeAgent. Returns
// the new workload id + the assigned node id.
//
// Differs from CreateWorkloadCommandHandler:
//   - Input is VM-shaped (Flavor + Image + Config overlay) instead
//     of an opaque SpecJson. The handler does the catalog
//     resolution here so the agent receives a wire-stable
//     VmRuntimeConfig (not a JSON string the agent has to parse
//     at runtime).
//   - Output is CreateVmResult (WorkloadId + AssignedNodeId),
//     not a WorkloadSummary. The /vms endpoint only needs the
//     handle for CreatedAtAction + the assigned node for the
//     dashboard's "now provisioning on ..." hint.
//   - Enqueues a workload.create NodeCommand synchronously
//     (instead of relying on the drift-detection job to notice the
//     new Workload row on its next poll). The explicit enqueue
//     matches the user's prompt for issue #6 and ensures the agent
//     gets the new workload within its 5-second long-poll window
//     instead of the (slower) drift-detection interval.
// ============================================================================

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Clusters.Application.Abstractions;
using Plexor.Modules.Clusters.Application.CreateVm;
using Plexor.Modules.Clusters.Application.Flavors;
using Plexor.Modules.Clusters.Application.Images;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Modules.Clusters.Domain.Errors;
using Plexor.Modules.Clusters.Infrastructure.Persistence;
using Plexor.Modules.Clusters.Infrastructure.Placement;
using Plexor.Shared.Identifiers;
using Plexor.Shared.NodeApi;

namespace Plexor.Modules.Clusters.Infrastructure.Clusters;

/// <summary>
///     Provision a new VM by resolving Flavor + Image + Config
///     against the catalogs, validating the final config, pinning
///     the target node, and enqueueing the create command.
/// </summary>
/// <param name="db">EF Core context for the write.</param>
/// <param name="flavorCatalog">Resolves FlavorName → Flavor (vCPU/RAM/disk preset).</param>
/// <param name="imageCatalog">Resolves ImageName → ImageRef (base image + source URL).</param>
/// <param name="scheduler">Picks the target node (manual policy in v0.1).</param>
/// <param name="candidateLoader">Loads Ready nodes in the cluster for the scheduler.</param>
/// <param name="clock">TimeProvider per time-and-wire-format.md §3.</param>
public sealed class CreateVmHandler(
    ClusterDbContext db,
    IFlavorCatalog flavorCatalog,
    IImageCatalog imageCatalog,
    IPlacementScheduler scheduler,
    PlacementCandidateLoader candidateLoader,
    TimeProvider clock) : ICommandHandler<CreateVmCommand, CreateVmResult>
{
    /// <inheritdoc />
    public async Task<CreateVmResult> HandleAsync(
        CreateVmCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ClustersException(
                ClustersExceptions.InvalidWorkloadSpec,
                "VM name is required.");
        }

        // 1. Resolve Flavor (default = first catalog entry).
        var flavor = CreateVmHandlerHelpers.ResolveFlavor(flavorCatalog, command.FlavorName) ?? throw new ClustersException(
                ClustersExceptions.InvalidWorkloadSpec,
                command.FlavorName is null
                    ? "Flavor catalog is empty; cannot provision a VM without a flavor seed."
                    : $"Unknown flavor '{command.FlavorName}'.");

        // 2. Resolve Image — three-way override order:
        //    a) ImageName provided → catalog lookup
        //    b) Config.ImageRef provided → use that directly
        //    c) Flavor.Default.ImageRef bundled → use it
        var imageRef = flavor.Default.ImageRef;
        if (command.ImageName is { } imageName)
        {
            var image = imageCatalog.Get(imageName)
                ?? throw new ClustersException(
                    ClustersExceptions.InvalidWorkloadSpec,
                    $"Unknown image '{imageName}'.");
            imageRef = image.Name;
        }

        if (command.Config is { ImageRef: { Length: > 0 } configImageRef })
        {
            imageRef = configImageRef;
        }

        // 3. Build the final VmRuntimeConfig by overlaying Config on
        //    Flavor.Default. Null Config fields fall through.
        var resolved = new VmRuntimeConfig(
            Vcpu: command.Config?.Vcpu ?? flavor.Default.Vcpu,
            RamBytes: command.Config?.RamBytes ?? flavor.Default.RamBytes,
            DiskBytes: command.Config?.DiskBytes ?? flavor.Default.DiskBytes,
            ImageRef: imageRef,
            NetworkName: command.Config?.NetworkName ?? flavor.Default.NetworkName,
            SshKeyFingerprint: command.Config?.SshKeyFingerprint ?? flavor.Default.SshKeyFingerprint);

        // 4. Validate the final config — pure-function check; throws
        //    on first failure in this call surface (the validator
        //    returns ALL errors, we surface them as a single message
        //    joined by semicolons so the operator sees every problem
        //    at once).
        var validation = VmRuntimeConfigValidator.Validate(resolved);
        if (!validation.IsValid)
        {
            throw new ClustersException(
                ClustersExceptions.InvalidWorkloadSpec,
                "VM runtime config failed validation: " + string.Join("; ", validation.Errors));
        }

        // 5. Name uniqueness — keep the invariant the existing
        //    CreateWorkloadCommandHandler enforces.
        if (await db.Workloads.AsNoTracking().AnyAsync(
                workload => workload.ClusterId == command.ClusterId && workload.Name == command.Name,
                cancellationToken))
        {
            throw new ClustersException(
                ClustersExceptions.InvalidWorkloadSpec,
                $"A workload named '{command.Name}' already exists in this cluster.");
        }

        // 6. Schedule — load candidates, ask the scheduler.
        var candidates = await candidateLoader.LoadAsync(command.ClusterId, cancellationToken);

        var spec = new Plexor.Modules.Clusters.Application.Abstractions.WorkloadSpec(
            ClusterId: command.ClusterId,
            Name: command.Name,
            Kind: "vm",
            TargetNodeId: command.TargetNodeId,
            RequiredCapabilities: []);

        var assignedNodeId = await scheduler.SelectNodeAsync(spec, candidates, cancellationToken);

        // VSTHRD103 false-positive — JsonSerializer.Serialize is
        // pure CPU-bound (no I/O on a small string). The async
        // overload requires a stream/pipeWriter and is strictly
        // heavier for a small payload. Suppress with explanation
        // rather than route through SerializeAsync. Same for
        // JsonDocument.Parse below.
#pragma warning disable VSTHRD103 // Sync serialize + parse — no I/O on a small string payload.
        var resolvedJson = JsonSerializer.Serialize(resolved);
        var resolvedElement = JsonDocument.Parse(resolvedJson).RootElement;
#pragma warning restore VSTHRD103

        var now = clock.GetUtcNow();
        var workload = new Workload
        {
            Id = IdGenerator.NewWorkloadId(),
            ClusterId = command.ClusterId,
            AssignedNodeId = assignedNodeId,
            LocalId = null,
            Name = command.Name,
            Kind = "vm",
            SpecJson = resolvedJson,
            State = Shared.Workloads.WorkloadState.Provisioning,
            LastMessage = null,
            LastReportedAt = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await db.Workloads.AddAsync(workload, cancellationToken);

        // 7. Enqueue workload.create on the assigned node. When the
        //    scheduler couldn't place the workload (assignedNodeId is
        //    null), skip the enqueue — drift-detection will pick up
        //    the row on the next poll once the operator pins a node.
        if (assignedNodeId is { } nodeId)
        {
            // VSTHRD103 false-positive — same rationale as above:
            // pure CPU-bound serialize, no I/O.
#pragma warning disable VSTHRD103 // Sync serialize — no I/O on a small string payload.
            var payloadJson = JsonSerializer.Serialize(
                new CreateWorkloadPayload(
                    Spec: new Plexor.Shared.NodeApi.WorkloadSpec(
                        Kind: new WorkloadKind.Vm(),
                        Name: command.Name,
                        Config: resolvedElement)));
#pragma warning restore VSTHRD103

            var wireCommandId = Guid.NewGuid();
            var nodeCommand = new NodeCommand(
                Id: Guid.NewGuid(),
                NodeId: nodeId,
                CommandId: wireCommandId,
                Type: CommandType.WorkloadCreate.Instance.Name,
                PayloadJson: payloadJson)
            {
                Status = NodeCommandStatus.Pending,
                CreatedAt = now,
            };
            await db.Commands.AddAsync(nodeCommand, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        // 8. Notify the scheduler AFTER persistence so a transient
        //    DB failure doesn't leave the scheduler thinking a
        //    workload is pinned to a node that doesn't have a row.
        if (assignedNodeId is { } pinnedNodeId)
        {
            await scheduler.MarkAssignedAsync(workload.Id, pinnedNodeId, cancellationToken);
        }

        return new CreateVmResult(workload.Id, assignedNodeId);
    }
}

/// <summary>
///     Pure-function helpers for <see cref="CreateVmHandler" />.
///     File-scoped per the class-decomposition rule (no
///     <c>private static</c> on production handlers — pure logic
///     lives in a <c>file static class</c> next to the consumer).
/// </summary>
file static class CreateVmHandlerHelpers
{
    /// <summary>
    ///     Resolve the Flavor the operator asked for, falling back
    ///     to the catalog's first entry when no name was supplied.
    ///     Pure function — no DI, no I/O.
    /// </summary>
    /// <param name="catalog"></param>
    /// <param name="requestedName"></param>
    public static Flavor? ResolveFlavor(IFlavorCatalog catalog, string? requestedName)
    {
        if (requestedName is not null)
        {
            return catalog.Get(requestedName);
        }

        var flavors = catalog.List();
        return flavors.Count > 0 ? flavors[0] : null;
    }
}

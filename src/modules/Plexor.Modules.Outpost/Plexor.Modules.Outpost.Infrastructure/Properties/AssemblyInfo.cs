// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AssemblyInfo — exposes internal types to the unit-test assembly.
//
// The unit tests need to reach the EF configurations
// (`NodeRecordConfiguration`), the in-memory DbContext factory, and
// any other `internal sealed` types we don't want to leak to
// downstream consumers. Per-project InternalsVisibleTo is the
// idiomatic .NET way to scope this access — the alternative is to
// make the configurations public, which leaks EF internals into the
// public API surface.
// ============================================================================

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Plexor.Modules.Outpost.Unit")]
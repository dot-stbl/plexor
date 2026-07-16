# ============================================================================
# Plexor.NodeAgent Dockerfile — multi-stage .NET 10 build.
#
# Stage 1: build the worker executable.
# Stage 2: copy into a runtime image that includes libvirt + (optional)
#          docker CLI / docker socket.
#
# Why runtime (not aspnet): Plexor.NodeAgent is a Worker SDK project,
# not a Web SDK one. `mcr.microsoft.com/dotnet/runtime` is the slim
# base — no ASP.NET, no web server, smaller surface.
#
# Usage (matches deploy/docker/compose.yaml after it's extended):
#   docker run -d \
#     --name plexor-nodeagent \
#     --privileged \
#     -v /var/run/docker.sock:/var/run/docker.sock \
#     -v /var/run/libvirt/libvirt-sock:/var/run/libvirt/libvirt-sock \
#     -v plexor_nodeagent-certs:/home/plexor/.plexor/agent \
#     plexor-nodeagent
#
# Mounts:
#   /var/run/docker.sock   — for DockerWorkloadProvider (Phase D Tier 3).
#                            Drop the mount if running only libvirt
#                            workloads; the agent's InMemoryWorkloadRegistry
#                            skips providers whose DI deps aren't reachable.
#   /var/run/libvirt/...    — for LibvirtKvmProvider / LxcProvider /
#                            QemuProvider (talk to libvirtd on the host).
#                            Required for any v0.1 workload.
#   <volume>:~/.plexor/agent — persist the mTLS client cert + CA
#                            bundle issued by Plexor.Host on join
#                            (see Plexor.Shared.Mtls/PlexorCaBootstrap.cs).
#                            Without a persistent mount the agent
#                            re-enrolls on every restart.
#
# Privileged: required for libvirt's /dev/kvm access. If running
# headless / without KVM, drop --privileged and the QemuProvider
# still works (software emulation).
# ============================================================================

ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

COPY . .

RUN dotnet restore src/agents/Plexor.NodeAgent/Plexor.NodeAgent.csproj
RUN dotnet publish src/agents/Plexor.NodeAgent/Plexor.NodeAgent.csproj \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=true

FROM mcr.microsoft.com/dotnet/runtime:${DOTNET_VERSION} AS runtime

# libvirt CLI + virsh for the KVM/LXC/QEMU providers.
# docker CLI + the socket are exposed by mounting /var/run/docker.sock
# from the host at run-time (the runtime image doesn't bake the socket
# in — that would lock the image to one host).
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
        libvirt-clients \
        libvirt-daemon-system \
        qemu-utils \
 && rm -rf /var/lib/apt/lists/*

# Non-root user — the agent runs as plexor so the bind-mounted cert
# volume can be chowned at runtime without root in the container.
RUN useradd --create-home --shell /bin/bash plexor
WORKDIR /app
COPY --from=build /app/publish ./
RUN chown -R plexor:plexor /app

# Persisted state — mTLS cert + CA bundle written by the join flow.
VOLUME ["/home/plexor/.plexor/agent"]
ENV HOME=/home/plexor

ENV DOTNET_ENVIRONMENT=Production
ENV Plexor__NodeAgent__Mtls__CertDirectory=/home/plexor/.plexor/agent

# Run as the non-root user so the bind-mounted cert directory is
# writable without --user 0.
USER plexor

ENTRYPOINT ["dotnet", "plexor-nodeagent.dll"]
# Plexor CI/CD workflows

Three GitHub Actions workflows that build, deploy, and smoke-test the
Plexor NodeAgent on a test VM. Operated end-to-end on every push to
`main`, individually triggerable from the Actions UI.

## At a glance

| Workflow | Trigger | What it does |
|----------|---------|--------------|
| `build.yml` | PR → `main` / `develop`, push → `main` | `dotnet build` + `dotnet test`, uploads TRX/coverage artifacts |
| `deploy-nodeagent.yml` | push → `main`, manual | `dotnet publish` the NodeAgent, ship to the test VM via SSH, restart systemd unit |
| `smoke-test.yml` | `workflow_run` after deploy, manual | SSH to the VM, run `deploy/scripts/smoke-test-vm-create.sh` |

## Required secrets

| Secret | Required by | What it is |
|--------|-------------|-----------|
| `TEST_VM_HOST` | deploy, smoke | SSH hostname of the test VM (e.g. `test-vm.example.com`) |
| `TEST_VM_USER` | deploy, smoke | SSH username with `sudo systemctl` rights |
| `TEST_VM_SSH_KEY` | deploy, smoke | PEM-encoded private key; matching public key on the VM in `~/.ssh/authorized_keys` |

**All three are optional in this MVP.** If any are missing, the relevant
job logs a warning and exits 0. The workflow as a whole stays green; the
operator configures the secrets and re-runs. This lets PRs from forks
and feature branches pass CI without exposing the test VM.

The SSH user on the VM needs `sudo` for:

- `systemctl daemon-reload` (first-time unit install + unit edits)
- `systemctl restart plexor-nodeagent` (every deploy)
- `virsh` and write access to `/var/lib/libvirt/images/` (smoke test)

A minimal sudoers drop-in for the deploy user:

```sudoers
# /etc/sudoers.d/plexor-deploy
deploy ALL=(ALL) NOPASSWD: /usr/bin/systemctl daemon-reload, \
    /usr/bin/systemctl restart plexor-nodeagent, \
    /usr/bin/systemctl enable plexor-nodeagent, \
    /usr/bin/systemctl is-active plexor-nodeagent, \
    /usr/bin/systemctl show plexor-nodeagent, \
    /usr/sbin/virsh, \
    /usr/bin/install, \
    /usr/bin/rsync, \
    /usr/bin/mkdir, \
    /usr/bin/rm, \
    /usr/bin/useradd, \
    /usr/bin/chown, \
    /usr/bin/cp, \
    /usr/bin/chmod, \
    /usr/bin/systemctl daemon-reload plexor-nodeagent
```

## Required VM setup (one-time)

The deploy workflow assumes the test VM has these prerequisites:

1. **.NET 10 runtime** (`/usr/bin/dotnet`, version ≥ 10.0.0). The deploy
   ships a framework-dependent build — no runtime bundling.
2. **systemd** + the `plexor` user (`useradd --system`).
3. **libvirt + qemu-kvm + virt-install** (smoke test only).
4. **Network egress** for HTTPS — needed for the NuGet restore and the
   Alpine ISO download in the smoke test.
5. **Default libvirt network `virbr0`** up — `virsh net-start default`.

The first deploy creates the `plexor` user if it doesn't exist. Subsequent
deploys reuse the same user / install directory.

## Why no `/health` endpoint on the deploy check

Plexor.NodeAgent is a **Worker Service** (`Microsoft.NET.Sdk.Worker`),
not a Web SDK project. It has no HTTP listener — its health surface is
the systemd unit, not a port. The deploy workflow verifies:

- `systemctl is-active plexor-nodeagent` → `active`
- The last 20 lines of `journalctl -u plexor-nodeagent` show no fatal
  stack traces during startup

This is the canonical health check for any Linux daemon and matches the
operational reality of running NodeAgents as systemd units on bare metal
or VM hosts.

## Manual triggers

Each workflow has a `workflow_dispatch` trigger. From the Actions UI:

- **build**: re-run the build for any branch. Useful for re-running CI
  on a branch after fixing a flaky test.
- **deploy-nodeagent**: force a deploy from any branch. Bumps the test
  VM with the current commit of the branch you trigger from.
- **smoke-test**: re-run the smoke test against an existing deploy.
  Defaults to the latest successful deploy on `main`; pass a specific
  run ID to verify a historical deploy.

## Debugging failures

### Build failed

1. Open the failed workflow run.
2. Expand the failing step — `dotnet build` errors land in the **Build**
   step, `dotnet test` failures in the **Test** step.
3. For test failures, download the `test-results` artifact and open the
   `.trx` file in Visual Studio / Rider — failures have a stack trace
   inline.

### Deploy skipped with "deploy secrets not configured"

1. Go to **Settings → Secrets and variables → Actions → Repository secrets**.
2. Add `TEST_VM_HOST`, `TEST_VM_USER`, `TEST_VM_SSH_KEY`.
3. Re-run the failed deploy workflow from the Actions UI.

### Deploy succeeded but smoke test failed

1. SSH to the test VM as `$TEST_VM_USER`.
2. `sudo journalctl -u plexor-nodeagent -n 200 --no-pager` — look for
   fatal exceptions, mTLS handshake errors, or "host unreachable".
3. The smoke test exits non-zero with a "SMOKE: FAIL" line in the GH
   Actions log. Re-run it manually with `workflow_dispatch` after
   fixing the VM — no need to trigger a fresh deploy.

### SSH connection refused / timeout

The `appleboy/ssh-action` step's full output is in the failed step.
Common causes:

- VM firewall blocks port 22 from the GH Actions runner IP ranges
  (GH publishes [a list](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/about-githubs-ip-addresses)).
- SSH host key verification fails — the action's `known_hosts` mode is
  the default (`strict`, accepts only known hosts). If this is a fresh
  VM, the host key isn't cached. Pass `known_hosts` config or use the
  `appleboy/ssh-action` `key_path` strategy that skips host key
  verification explicitly (NOT recommended — it weakens security).

## Action versions

All third-party actions pin to a major version (no `@latest`, no
`@master`). Where the user spec asked for `appleboy/ssh-action@master`,
we use `appleboy/ssh-action@v1.0.3` instead — `@master` is a moving
branch ref, and the "pin to major" rule supersedes the literal name in
the spec.

| Action | Version | Why |
|--------|---------|-----|
| `actions/checkout` | `@v4` | Current GA major |
| `actions/setup-dotnet` | `@v4` | Current GA major |
| `actions/cache` | `@v4` | Current GA major |
| `actions/upload-artifact` | `@v4` | Current GA major; v3 is EOL |
| `actions/download-artifact` | `@v4` | Current GA major |
| `appleboy/scp-action` | `@v0.1.7` | Latest tagged pre-1.0; v1 not yet released |
| `appleboy/ssh-action` | `@v1.0.3` | Major-1 stable; supports `command_timeout` |

## Limitations and known gaps

1. **No true NodeAgent E2E**. The smoke test exercises libvirt on the
   VM, but does not POST a workload request through Plexor.Host and
   observe the agent create a VM. That's a multi-system test that needs
   the control plane deployed too — out of scope here.
2. **No multi-VM deploy**. The deploy workflow targets exactly one test
   VM. A `matrix` job keyed off `secrets.TEST_VM_*_1` / `_2` / etc.
   would fan out — left as future work when there's more than one VM.
3. **No automatic rollback**. A deploy that ships but doesn't pass the
   smoke test leaves a broken NodeAgent running. Manual `sudo systemctl
   stop plexor-nodeagent` on the VM restores the previous binary state
   (the old `/opt/plexor-nodeagent` is overwritten on every deploy, so
   "rollback" really means redeploying an earlier commit).
4. **No notifications**. Deploy/smoke failures show in the GitHub UI but
   don't ping Slack/Discord/email. Add a `notifications:` job that
   POSTs to a webhook when one is needed.
5. **Single-branch smoke test**. `workflow_run` triggers fire on
   `main` only (PR branches skip). If you need to smoke-test a feature
   branch, run it via `workflow_dispatch` with a specific deploy run ID.

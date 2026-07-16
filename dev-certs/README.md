# Local mTLS Dev Certificates

This directory is **created per-developer, never committed**. Each
CA / host cert is unique to the machine that minted it.

## What lives here

```
ca.crt       — Plexor CA root certificate (PEM)
ca.key       — Plexor CA root private key (PEM, mode 0600 on Unix)
host.pfx     — Host server cert (PKCS#12, password-protected)
node-*.pfx   — Issued per-node client cert at NodeJoin
```

## How they get here

The host's `PlexorCaBootstrap` runs on first startup:

1. `dev-certs/ca.crt` + `ca.key` — generate if absent (RSA-4096, 10y
   TTL, `CN=Plexor Root CA`).
2. `dev-certs/host.pfx` — issue + save as PKCS#12 with the password
   from `CertAuthority:HostCertPassword` if absent.
3. Node certs — issued on each successful `POST /join`, one PFX per
   node id.

## Production

`CertAuthorityOptions` defaults are **relative** (`dev-certs/...`). On
production deploy, the operator sets absolute paths via env vars:

```bash
export CertAuthority__CertPath=/var/lib/plexor/ca.crt
export CertAuthority__KeyPath=/var/lib/plexor/ca.key
export CertAuthority__HostCertPath=/var/lib/plexor/host.pfx
export CertAuthority__HostCertPassword='<from secret store>'
```

Or via `appsettings.Production.json` mounted by the systemd unit.

## Cross-platform

| OS      | Default dev root                  | Production convention             |
|---------|-----------------------------------|------------------------------------|
| Windows | `<repo>\dev-certs\`               | `%LOCALAPPDATA%\plexor\`           |
| Linux   | `<repo>/dev-certs/`               | `/var/lib/plexor/`                 |
| macOS   | `<repo>/dev-certs/`               | `/usr/local/var/plexor/` (Homebrew)|

The repository root is found by walking up parents from
`AppContext.BaseDirectory` looking for `.git/` (or `plexor.slnx`).

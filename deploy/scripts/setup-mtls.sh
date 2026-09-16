#!/usr/bin/env bash
# ============================================================================
# setup-mtls.sh — pre-place the Plexor mTLS triple on a NodeAgent VM.
#
# What this script does:
#   1. Validates the NodeId format (must be Plexor wire-format: node_<...>).
#   2. Verifies the host CA cert (operator pre-shared via scp/USB/etc.)
#      matches the expected SHA-256 fingerprint.
#   3. Generates a self-signed placeholder client cert + key with the
#      correct Plexor NodeId CN — placeholder because in v0.1 the agent
#      does an anonymous /join on first start and the host re-issues a
#      Plexor-CA-signed cert through that flow. Post-enrollment the
#      placeholder is overwritten by Plexor.Shared.Mtls.MtlsCertWriter.
#   4. Places the CA root, client cert, and key under
#      /etc/plexor-nodeagent/certs/ with the right ownership and mode
#      (0600 on the key, 0644 on cert + CA, dir 0750).
#
# Usage (positional, as documented in deploy/ansible/README.md):
#   sudo ./setup-mtls.sh <node-id> <host-ca-fingerprint>
#
# Required positional args:
#   <node-id>              Plexor NodeId in wire form, e.g. node_01HXYZ...
#                          (must match the regex below)
#   <host-ca-fingerprint>  SHA-256 fingerprint of the Plexor CA root,
#                          "sha256:<64-hex-chars>" or bare 64-hex-chars.
#
# Optional env vars (used by the Ansible playbook):
#   PLEXOR_HOST_CA_FILE    Path to the pre-shared CA cert (default:
#                          /etc/plexor-nodeagent/host-ca.crt). Pre-place
#                          this file before running the script.
#   PLEXOR_CERT_DIR        Output directory (default:
#                          /etc/plexor-nodeagent/certs).
#   PLEXOR_SERVICE_USER    Service user that owns the files (default:
#                          plexor-nodeagent, must exist).
#
# Idempotent: re-running overwrites files in place. Safe to invoke from
# the Ansible playbook's `task` block; the playbook also calls this
# after copying the CA cert into place.
# ============================================================================
set -euo pipefail

# ---- arg / env parsing ----------------------------------------------------
if [[ $# -lt 2 ]]; then
  cat >&2 <<EOF
usage: sudo $0 <node-id> <host-ca-fingerprint>

  <node-id>              Plexor NodeId in wire form (node_<ulid>)
  <host-ca-fingerprint>  SHA-256 fingerprint of the host CA root

Environment overrides:
  PLEXOR_HOST_CA_FILE    Path to pre-shared CA cert
                         (default: /etc/plexor-nodeagent/host-ca.crt)
  PLEXOR_CERT_DIR        Output directory
                         (default: /etc/plexor-nodeagent/certs)
  PLEXOR_SERVICE_USER    Service user owning the files
                         (default: plexor-nodeagent)
EOF
  exit 2
fi

NODE_ID="$1"
CA_FINGERPRINT="$2"

CA_SOURCE="${PLEXOR_HOST_CA_FILE:-/etc/plexor-nodeagent/host-ca.crt}"
CERT_DIR="${PLEXOR_CERT_DIR:-/etc/plexor-nodeagent/certs}"
SERVICE_USER="${PLEXOR_SERVICE_USER:-plexor-nodeagent}"

# ---- preconditions --------------------------------------------------------
if [[ $EUID -ne 0 ]]; then
  echo "error: this script must run as root (writes /etc and chowns)" >&2
  exit 1
fi

if ! command -v openssl >/dev/null 2>&1; then
  echo "error: openssl not found; install with 'apt-get install openssl'" >&2
  exit 1
fi

if ! id "$SERVICE_USER" >/dev/null 2>&1; then
  echo "error: service user '$SERVICE_USER' does not exist" >&2
  exit 1
fi

if [[ ! -s "$CA_SOURCE" ]]; then
  echo "error: pre-shared CA cert not found at $CA_SOURCE" >&2
  echo "       copy the Plexor CA root (host's dev-certs/ca.crt or" >&2
  echo "       /var/lib/plexor/ca.crt in production) to that path first" >&2
  exit 1
fi

# Validate NodeId format: node_ + 26-char Crockford-base32 ULID.
if ! [[ "$NODE_ID" =~ ^node_[0-9A-HJKMNP-TV-Z]{26}$ ]]; then
  echo "error: NODE_ID '$NODE_ID' is not a valid Plexor NodeId" >&2
  echo "       expected: node_<26-char ULID>" >&2
  exit 1
fi

# Normalise fingerprint to "sha256:<hex>" form for openssl comparison.
if [[ "$CA_FINGERPRINT" =~ ^[0-9A-Fa-f]{64}$ ]]; then
  CA_FINGERPRINT="sha256:$CA_FINGERPRINT"
elif [[ ! "$CA_FINGERPRINT" =~ ^sha256:[0-9A-Fa-f]{64}$ ]]; then
  echo "error: CA_FINGERPRINT must be 64 hex chars or 'sha256:<64-hex>'" >&2
  exit 1
fi

# ---- verify CA fingerprint ------------------------------------------------
ACTUAL_FP="$(openssl x509 -in "$CA_SOURCE" -noout -fingerprint -sha256 \
  | sed -E 's/^sha256 Fingerprint=//; s/://g; s/.*/&\n/' \
  | tr -d '\n[:space:]' \
  | tr '[:upper:]' '[:lower:]')"

EXPECTED_FP="$(echo "$CA_FINGERPRINT" \
  | sed -E 's/^sha256://' \
  | tr '[:upper:]' '[:lower:]')"

if [[ "$ACTUAL_FP" != "$EXPECTED_FP" ]]; then
  echo "error: CA fingerprint mismatch" >&2
  echo "       expected: sha256:$EXPECTED_FP" >&2
  echo "       actual:   sha256:$ACTUAL_FP" >&2
  echo "       refusing to install — verify the CA file is from the right host" >&2
  exit 1
fi

# ---- lay out cert directory -----------------------------------------------
install -d -m 0750 -o "$SERVICE_USER" -g "$SERVICE_USER" "$CERT_DIR"

# ---- generate self-signed placeholder client cert + key -------------------
# Placeholder: v0.1's join flow overwrites this with a Plexor-CA-signed
# cert from Plexor.Shared.Mtls.MtlsCertWriter on first successful /join.
# 30-day TTL keeps the placeholder from drifting past the join deadline;
# post-enrollment the file on disk is the host-issued cert, not this.
PLACEHOLDER_TTL_DAYS=30
PLACEHOLDER_TMP="$(mktemp -d)"
trap 'rm -rf "$PLACEHOLDER_TMP"' EXIT

openssl req -new -newkey rsa:2048 -nodes \
  -keyout "$PLACEHOLDER_TMP/node.key" \
  -out "$PLACEHOLDER_TMP/node.csr" \
  -subj "/CN=$NODE_ID" \
  >/dev/null 2>&1

openssl x509 -req -in "$PLACEHOLDER_TMP/node.csr" \
  -signkey "$PLACEHOLDER_TMP/node.key" \
  -days "$PLACEHOLDER_TTL_DAYS" \
  -out "$PLACEHOLDER_TMP/node.crt" \
  >/dev/null 2>&1

# ---- install files -------------------------------------------------------
# CA root: 0644, owned by service user (agent reads on every mTLS call).
install -m 0644 -o "$SERVICE_USER" -g "$SERVICE_USER" \
  "$CA_SOURCE" "$CERT_DIR/ca.crt"

# Client cert: 0644 (public material once paired with the key).
install -m 0644 -o "$SERVICE_USER" -g "$SERVICE_USER" \
  "$PLACEHOLDER_TMP/node.crt" "$CERT_DIR/node.crt"

# Client key: 0600 — sensitive, only the service user can read.
install -m 0600 -o "$SERVICE_USER" -g "$SERVICE_USER" \
  "$PLACEHOLDER_TMP/node.key" "$CERT_DIR/node.key"

echo "ok: mTLS triple installed at $CERT_DIR"
echo "    node id: $NODE_ID"
echo "    CA fp:   $ACTUAL_FP"
echo "    cert TTL: $PLACEHOLDER_TTL_DAYS days (placeholder; replaced on first /join)"
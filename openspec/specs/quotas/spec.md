# Capability: quotas

## Purpose

Capacity control for the Plexor platform. A single self-hosted
user can exhaust host resources (CPU, RAM, volume storage,
floating IPs) because nothing currently limits them; multi-tenant
SaaS deploys need per-organization capacity limits to charge for
usage and prevent noisy neighbors.

The quota capability is **not implemented in v0.1**. The current
state is: every resource-creation endpoint accepts whatever
request the operator sends, with no upper bound beyond the host
hardware itself.

The proposed implementation is **scheduled for Phase 4.5**.
See `openspec/changes/phase-4-5-quotas/` for the proposal, the
implementation task list, the design decisions, and the spec
deltas that will land when the change is merged.

## Requirements

This capability is scheduled for Phase 4.5. See
`changes/phase-4-5-quotas/` for the proposed implementation.

No current-state requirements exist for this capability. The
proposed requirements that will land when the change is merged
are listed in
`openspec/changes/phase-4-5-quotas/specs/quotas/spec.md` under
`## ADDED Requirements`. Once that change is merged, those
requirements move up into this file under `## Requirements`.
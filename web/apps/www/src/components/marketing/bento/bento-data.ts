import type { Icon } from '@nine-thirty-five/material-symbols-react';
import {
  AccountTree,
  DeployedCode,
  HardDisk,
  History,
  Storefront,
  VerifiedUser,
} from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * Bento grid data (amendment A3) — six product surfaces, each honestly
 * marked shipped or not-yet. Source of truth for status: the old roadmap
 * `PHASES` array (`git show HEAD:web/apps/docs/src/components/marketing/
 * marketing-roadmap.tsx`, per amendment A8) plus the docs tree under
 * `src/routes/(docs)/docs/**`.
 *
 * v0.1/v0.2 (both "shipped") cover: the binary + orgs/users, VMs,
 * networks and storage. Quotas are explicitly v0.3 ("next"); the app
 * catalog is also explicitly v0.3 ("next"). Audit never gets its own
 * "shipped" bullet in any version, so it is grouped with quotas under
 * "next" rather than claimed as shipped on its own — the conservative
 * reading the spec asks for when a cell is ambiguous.
 */
export type BentoStatus = 'shipped' | 'next';

export type BentoVisualKind =
  | 'status-list'
  | 'chip-row'
  | 'capacity-bar'
  | 'scope-path'
  | 'log-list'
  | 'command-line';

/**
 * `category` (added for the YC-informed services catalog panel and the
 * header mega-menu, both of which group these same six surfaces —
 * product owner decision, 2026-09-24 landing restyle): plain sentence-
 * case label, not a new taxonomy — six surfaces, six categories, one
 * each, because Plexor doesn't have enough shipped surfaces yet to need
 * a denser many-per-category grid (honest content: don't invent extra
 * services or subcategories to fill space).
 */
export interface BentoCellData {
  readonly id: string;
  readonly icon: Icon;
  readonly category: string;
  readonly title: string;
  readonly body: string;
  readonly status: BentoStatus;
  readonly visual: BentoVisualKind;
}

export const BENTO_CELLS: readonly BentoCellData[] = [
  {
    id: 'compute',
    icon: DeployedCode,
    category: 'Compute',
    title: 'Compute',
    body: 'Virtual machines with snapshots and an in-browser console. Boot, resize and tear down from the same place you provisioned them.',
    status: 'shipped',
    visual: 'status-list',
  },
  {
    id: 'networking',
    icon: AccountTree,
    category: 'Network',
    title: 'Networking',
    body: 'Private networks, security groups, floating IPs and load balancers — wired together without touching a router.',
    status: 'shipped',
    visual: 'chip-row',
  },
  {
    id: 'storage',
    icon: HardDisk,
    category: 'Storage',
    title: 'Storage',
    body: 'Block volumes and S3-compatible object buckets, on the disks already in the box.',
    status: 'shipped',
    visual: 'capacity-bar',
  },
  {
    id: 'identity',
    icon: VerifiedUser,
    category: 'Identity & access',
    title: 'Identity & access',
    body: 'Per-org users, roles and API keys. Flat permission strings, no wildcards — bring your own IdP over OIDC.',
    status: 'shipped',
    visual: 'scope-path',
  },
  {
    id: 'quotas-audit',
    icon: History,
    category: 'Operations',
    title: 'Quotas & audit',
    body: 'Folder-level quotas and an append-only audit log on every state change.',
    status: 'next',
    visual: 'log-list',
  },
  {
    id: 'app-catalog',
    icon: Storefront,
    category: 'Managed services',
    title: 'App catalog',
    body: 'Postgres, Redis, Keycloak and more, installed with one command from a versioned, signed manifest.',
    status: 'next',
    visual: 'command-line',
  },
];

const STATUS_LABEL: Readonly<Record<BentoStatus, string>> = {
  shipped: 'Shipped',
  next: 'Next',
};

const STATUS_VARIANT: Readonly<Record<BentoStatus, 'ok' | 'warn'>> = {
  shipped: 'ok',
  next: 'warn',
};

export function bentoStatusLabel(status: BentoStatus): string {
  return STATUS_LABEL[status];
}

export function bentoStatusVariant(status: BentoStatus): 'ok' | 'warn' {
  return STATUS_VARIANT[status];
}

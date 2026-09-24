import type { StatusVariant } from '@/shared/ui/primitives/status-pill';
import type { NodeRole, NodeStatus, TokenStatus } from './cluster-types';

/** Node status → Plexor DS status variant (single closed-enum switch). */
export function mapNodeStatusToVariant(status: NodeStatus): StatusVariant {
  switch (status) {
    case 'ready':
      return 'running';
    case 'pending':
      return 'pending';
    case 'draining':
      return 'idle';
    case 'offline':
      return 'err';
  }
}

/** i18n key for a node status label (rows, strip copy). */
export function nodeStatusLabelKey(status: NodeStatus): string {
  switch (status) {
    case 'ready':
      return 'clusters.node.status.ready';
    case 'pending':
      return 'clusters.node.status.pending';
    case 'draining':
      return 'clusters.node.status.draining';
    case 'offline':
      return 'clusters.node.status.offline';
  }
}

/** i18n key for a node role label. */
export function nodeRoleLabelKey(role: NodeRole): string {
  switch (role) {
    case 'control':
      return 'clusters.node.role.control';
    case 'compute':
      return 'clusters.node.role.compute';
  }
}

/** i18n key for a join-token status label. */
export function tokenStatusLabelKey(status: TokenStatus): string {
  switch (status) {
    case 'active':
      return 'clusters.token.status.active';
    case 'expired':
      return 'clusters.token.status.expired';
    case 'revoked':
      return 'clusters.token.status.revoked';
  }
}

/**
 * Launcher summary — shared between the launcher SUMMARY cards
 * (`shared/ui/app-shell/app-launcher.tsx`) and any test that asserts
 * the launcher renders real numbers instead of `— / нет данных`.
 *
 * The shape mirrors what the home page would render if the user
 * opened it: per-domain counts (VMs by status, clusters by node
 * status, recent audit events). Reading from this fixture means the
 * launcher never drifts from the test fixtures used elsewhere.
 */

import { countByStatus, FLEET } from './compute/vms';
import { clusterSummary } from './fleet/clusters';
import { makeAuditEntries } from './audit/audit';

export interface LauncherSummaryCard {
  /** Stable id — used by tests as `data-od-id` and by the route. */
  id: 'vms' | 'networks' | 'audit';
  /** i18n key — the launcher resolves it via `t()` at render time. */
  labelKey: string;
  /** Where the card navigates to on click. */
  to: string;
  /** Single-line headline (e.g. "6"). */
  value: string;
  /** Context line under the headline (e.g. "running of 8 total"). */
  context: string;
}

/**
 * Build the three launcher SUMMARY cards from the mock fixtures.
 * `vms` → FLEET counts, `networks` → cluster counts, `audit` → event
 * count over the last 24h. Re-runs cheaply on every launcher open.
 */
export function makeLauncherSummary(): LauncherSummaryCard[] {
  const vmCounts = countByStatus();
  const total = FLEET.length;
  const running = vmCounts.running + vmCounts.provisioning;
  const clusters = clusterSummary();
  const recentAudit = makeAuditEntries(12);

  return [
    {
      id: 'vms',
      labelKey: 'shell.launcher.summary.vms.label',
      to: '/vms',
      value: String(running),
      context: `running of ${total} total`,
    },
    {
      id: 'networks',
      labelKey: 'shell.launcher.summary.networks.label',
      to: '/networks',
      value: `${clusters.ready}/${clusters.total}`,
      context: `${clusters.clusters} clusters, nodes ready`,
    },
    {
      id: 'audit',
      labelKey: 'shell.launcher.summary.audit.label',
      to: '/audit',
      value: String(recentAudit.length),
      context: 'last 24h',
    },
  ];
}

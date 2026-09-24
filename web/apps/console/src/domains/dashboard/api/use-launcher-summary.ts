import { useMemo } from 'react';
import { makeLauncherSummary, type LauncherSummaryCard } from '@/mocks/launcher-summary';

/**
 * Launcher SUMMARY cards (VMs / networks / audit) for the app launcher's
 * top region. Wraps `makeLauncherSummary()` in a hook so the composition
 * root (`routes/__root.tsx`) can fetch it and pass it down as a prop —
 * `shared/ui/app-shell` never reaches into a mock/domain module itself
 * (see .agents/docs/architecture/frontend-ddd.md §3 and step 10's
 * AppLauncher fix).
 */
export function useLauncherSummary(): LauncherSummaryCard[] {
  return useMemo(() => makeLauncherSummary(), []);
}

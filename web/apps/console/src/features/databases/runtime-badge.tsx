import type { Icon } from '@nine-thirty-five/material-symbols-react';
import { DeployedCode, DesktopWindows, Hexagon, Package } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { cn } from '@/shared/lib/utils';
import type { Runtime } from './database-types';
import { RUNTIME_META } from './database-types';

export const RUNTIME_ICON: Record<Runtime, Icon> = {
  vm: DesktopWindows,
  lxc: Package,
  docker: DeployedCode,
  k8s: Hexagon,
};

export interface RuntimeBadgeProps {
  runtime: Runtime;
  className?: string;
}

/**
 * Компактный бейдж рантайма (иконка + метка). Монохромный — рантайм не статус.
 * Delegated (k8s) — пунктирная рамка, отличается от direct.
 */
export function RuntimeBadge({ runtime, className }: RuntimeBadgeProps) {
  const meta = RUNTIME_META[runtime];
  const RuntimeIcon = RUNTIME_ICON[runtime];
  return (
    <span
      data-slot="runtime-badge"
      data-runtime={runtime}
      className={cn(
        'inline-flex h-6 items-center gap-1.5 rounded-md border bg-surface-2 px-1.5 text-xs font-medium text-foreground',
        meta.class === 'delegated' ? 'border-dashed border-border-2' : 'border-border',
        className,
      )}
      title={`${meta.label} · ${meta.class} — ${meta.blurb}`}
    >
      <RuntimeIcon className="size-3.5 text-muted-foreground" />
      {meta.label}
    </span>
  );
}

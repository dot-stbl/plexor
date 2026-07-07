import { cn } from '@/lib/utils';

import type { ComponentProps } from 'react';

/**
 * Stat — single-metric card (label + value + optional trend).
 *
 * Abstract: any "one number with context" use case (billing, dashboards,
 * server stats, KPIs, KPIs across product surfaces).
 */
export interface StatProps extends Omit<ComponentProps<'div'>, 'children'> {
  label: React.ReactNode;
  value: React.ReactNode;
  /** Optional trend: 'up' = bad (red), 'down' = good (green), or neutral. */
  trend?: 'up' | 'down' | 'neutral';
  context?: React.ReactNode;
}

const TREND_COLOR = {
  up: 'text-err',
  down: 'text-ok',
  neutral: 'text-muted-foreground',
};

export function Stat({ label, value, trend, context, className, ...props }: StatProps) {
  return (
    <div
      data-slot="stat"
      data-trend={trend}
      className={cn(
        'flex flex-col gap-1 rounded-lg border border-border bg-card p-4',
        className,
      )}
      {...props}
    >
      <div className="text-[11px] uppercase tracking-[0.06em] text-muted-foreground font-medium">
        {label}
      </div>
      <div className="font-mono text-[22px] font-medium tracking-[-0.015em] tabular-nums">
        {value}
      </div>
      {(trend || context) && (
        <div
          className={cn(
            'flex items-center gap-1 font-mono text-xs',
            trend && TREND_COLOR[trend],
          )}
        >
          {trend && <span>{trend === 'up' ? '↑' : trend === 'down' ? '↓' : '—'}</span>}
          {context && <span className="text-muted-foreground">{context}</span>}
        </div>
      )}
    </div>
  );
}

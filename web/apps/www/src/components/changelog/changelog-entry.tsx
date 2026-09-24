import { cn } from '@/lib/utils';
import { StatusPill, type StatusVariant } from '@/components/ui/status-pill';
import type { ChangelogEntryData, ChangelogStatus } from './types';

/** Convention (handoff §5 / foundation report): shipped→ok, next→warn, design→idle. */
const STATUS_VARIANT: Readonly<Record<ChangelogStatus, StatusVariant>> = {
  shipped: 'ok',
  next: 'warn',
  design: 'idle',
};

const STATUS_LABEL: Readonly<Record<ChangelogStatus, string>> = {
  shipped: 'Shipped',
  next: 'Next',
  design: 'Design',
};

/**
 * One release row — nextjs.org-changelog-like: a left column (version +
 * `StatusPill`, sticky on `md:` so it stays visible while a long bullet
 * list scrolls past it) and a right column (title + prose bullets, the
 * MDX body compiled from the matching `src/content/changelog/*.mdx`
 * file). `isFirst` suppresses the hairline divider on the group's first
 * entry so the group doesn't start with a stray rule right under its
 * heading.
 */
export function ChangelogEntry({
  entry,
  isFirst = false,
}: {
  entry: ChangelogEntryData;
  isFirst?: boolean;
}) {
  const { version, title, status, Content } = entry;

  return (
    <article
      className={cn(
        'grid grid-cols-1 gap-3 py-8 md:grid-cols-[9rem_1fr] md:gap-8',
        !isFirst && 'border-t border-border',
      )}
    >
      <div className="flex items-center gap-2 md:sticky md:top-24 md:h-fit">
        {/* `bg-card`, not `bg-muted` — this chip sits inside a `Panel` whose
            own fill is `muted` or `sunken` (see `changelog-list.tsx`); a
            `bg-muted` chip would blend into a `fill="muted"` panel. */}
        <code className="rounded bg-card px-1.5 py-0.5 font-mono text-xs text-foreground">
          {version}
        </code>
        <StatusPill variant={STATUS_VARIANT[status]}>{STATUS_LABEL[status]}</StatusPill>
      </div>

      <div className="min-w-0">
        <h3 className="text-base font-semibold text-foreground">{title}</h3>
        <div className="mt-3 text-sm leading-6 text-muted-foreground [&_ul]:list-disc [&_ul]:space-y-1.5 [&_ul]:pl-5">
          <Content />
        </div>
      </div>
    </article>
  );
}

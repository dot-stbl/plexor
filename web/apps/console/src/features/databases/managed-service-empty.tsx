import { Database, Lightning, ChartBar, Plus, type Icon } from '@/shared/ui/icon';
import { Link } from '@tanstack/react-router';
import { Button } from '@/shared/ui/primitives/button';
import { TechIcon } from '@/shared/ui/primitives/tech-icon';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import type { DbEngine, DbKind } from './database-types';

const KIND_ICON: Record<DbKind, Icon> = {
  relational: Database,
  cache: Lightning,
  queue: Lightning,
  analytics: ChartBar,
};

/**
 * YC-подобный онбординг для managed-движка без кластеров — тонкая обёртка над
 * `EmptyState`: бренд-марка движка слева, объясняющий текст + doc-ссылки + CTA
 * справа. Managed-БД — сложный сервис, поэтому пустой экран объясняет, а не
 * просто говорит «пусто».
 */
export function ManagedServiceEmpty({ engine }: { engine: DbEngine }) {
  return (
    <EmptyState
      data-od-id={`managed-empty-${engine.id}`}
      media={<TechIcon slug={engine.id} fallback={KIND_ICON[engine.kind]} className="size-20" />}
      title={`Create your first ${engine.name} cluster`}
      description={
        <>
          <p className="text-sm text-muted-foreground">{engine.about}</p>
          <p className="text-sm text-muted-foreground">
            Each cluster is one or more hosts running the database on the runtime you choose
            (VM, LXC, Docker, or k8s). Plexor handles networking, internal DNS, and backups.
          </p>
        </>
      }
      docsLabel={
        <>
          To get started, click <span className="font-medium text-foreground">Create cluster</span>.
          Learn more in the docs:
        </>
      }
      docs={[
        { href: `https://plexor.dev/docs/db/${engine.id}`, label: `Getting started with ${engine.name}` },
        { href: `https://plexor.dev/docs/db/${engine.id}/config`, label: 'Configuration and tuning' },
        { href: 'https://plexor.dev/docs/db/backups', label: 'Backups and recovery' },
        { href: 'https://plexor.dev/docs/pricing', label: 'Pricing rules' },
      ]}
      action={
        <Button nativeButton={false} render={<Link to="/managed/new" search={{ engine: engine.id }} />}>
          <Plus className="size-3.5" />
          Create {engine.name} cluster
        </Button>
      }
    />
  );
}

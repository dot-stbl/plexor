import { Add, BarChart, Bolt, Database } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { useTranslation } from 'react-i18next';
import type { Icon } from '@nine-thirty-five/material-symbols-react';
import { Link } from '@tanstack/react-router';
import { Button } from '@/shared/ui/primitives/button';
import { TechIcon } from '@/shared/ui/primitives/tech-icon';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import type { DbEngine, DbKind } from './database-types';

const KIND_ICON: Record<DbKind, Icon> = {
  relational: Database,
  cache: Bolt,
  queue: Bolt,
  analytics: BarChart,
};

/**
 * YC-подобный онбординг для managed-движка без кластеров — тонкая обёртка над
 * `EmptyState`: бренд-марка движка слева, объясняющий текст + doc-ссылки + CTA
 * справа. Managed-БД — сложный сервис, поэтому пустой экран объясняет, а не
 * просто говорит «пусто».
 */
export function ManagedServiceEmpty({ engine }: { engine: DbEngine }) {
  const { t } = useTranslation();
  return (
    <EmptyState
      data-od-id={`managed-empty-${engine.id}`}
      media={<TechIcon slug={engine.id} fallback={KIND_ICON[engine.kind]} className="size-20" />}
      title={t('managed.empty.title', { engine: engine.name })}
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
          <Add className="size-3.5" />
          Create {engine.name} cluster
        </Button>
      }
    />
  );
}

import { useTranslation } from 'react-i18next';
import { ArrowForward, BarChart, Bolt, Database } from '@nine-thirty-five/material-symbols-react/rounded/700';
import type { Icon } from '@nine-thirty-five/material-symbols-react';
import { PageTemplate } from '@/shared/ui/app-shell';
import { Badge } from '@/shared/ui/primitives/badge';
import { Button } from '@/shared/ui/primitives/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/shared/ui/primitives/card';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { TechIcon } from '@/shared/ui/primitives/tech-icon';
import type { DbEngine, DbKind } from '../model/database-types';
import { DB_KIND_LABEL } from '../model/database-types';

const KIND_ICON: Record<DbKind, Icon> = {
  relational: Database,
  cache: Bolt,
  queue: Bolt,
  analytics: BarChart,
};

interface ManagedLandingProps {
  /** Full engine catalog — one card per engine. */
  engines: ReadonlyArray<DbEngine>;
  /** Deployed-cluster count per engineId (zeros included). */
  clusterCounts: Readonly<Record<string, number>>;
  /** Loading state — renders the card-grid skeleton while true. */
  isPending?: boolean;
  /** Open an engine section (/managed/<engine>). */
  onOpenEngine: (engine: DbEngine) => void;
}

/**
 * Landing of the «Data platform» section (/managed): the engine catalog as
 * a card grid — brand mark, kind, version, blurb, deployed-cluster count,
 * and a CTA into the engine's section. This is the section's front door;
 * the per-engine pages keep the strip + table + onboarding.
 */
export function ManagedLanding({ engines, clusterCounts, isPending = false, onOpenEngine }: ManagedLandingProps) {
  const { t } = useTranslation();

  return (
    <PageTemplate
      data-od-id="managed-landing"
      width="wide"
      title={t('managed.landing.title')}
      description={t('managed.landing.description')}
    >
      {isPending ? (
        <ManagedLandingSkeleton />
      ) : engines.length === 0 ? (
        <EmptyState
          data-od-id="managed-landing-empty"
          icon={Database}
          title={t('managed.landing.empty.title')}
          description={t('managed.landing.empty.description')}
          docs={[{ href: 'https://plexor.dev/docs/db', label: t('managed.landing.empty.docs') }]}
        />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {engines.map((engine) => (
            <ManagedEngineCard
              key={engine.id}
              engine={engine}
              clusterCount={clusterCounts[engine.id] ?? 0}
              onOpen={onOpenEngine}
            />
          ))}
        </div>
      )}
    </PageTemplate>
  );
}

interface ManagedEngineCardProps {
  engine: DbEngine;
  clusterCount: number;
  onOpen: (engine: DbEngine) => void;
}

/** One engine in the catalog grid. The CTA is the only click target —
 *  the card itself stays inert (matches the onboarding empty-state CTA). */
function ManagedEngineCard({ engine, clusterCount, onOpen }: ManagedEngineCardProps) {
  const { t } = useTranslation();
  return (
    <Card data-od-id={`managed-card-${engine.id}`} className="flex flex-col">
      <CardHeader className="border-b border-border">
        <CardTitle className="flex items-center gap-2 text-sm">
          <TechIcon slug={engine.id} fallback={KIND_ICON[engine.kind]} className="size-5" />
          {engine.name}
        </CardTitle>
        <CardDescription className="flex flex-wrap items-center gap-1.5">
          <Badge variant="secondary">{DB_KIND_LABEL[engine.kind]}</Badge>
          <Badge variant="outline">v{engine.version}</Badge>
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-1 flex-col gap-4">
        <p className="text-sm text-muted-foreground">{engine.blurb}</p>
        <div className="mt-auto flex items-center justify-between gap-2">
          <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
            <MonoNum muted>{clusterCount}</MonoNum>
            <span>{t('managed.landing.clusters')}</span>
          </span>
          <Button size="sm" variant="outline" onClick={() => onOpen(engine)}>
            {t('managed.landing.open')}
            <ArrowForward className="size-3.5" />
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

/** Card-grid skeleton — three placeholder cards shaped like an engine card. */
function ManagedLandingSkeleton() {
  return (
    <div data-od-id="managed-landing-skeleton" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {Array.from({ length: 3 }).map((_, index) => (
        <div key={index} className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4">
          <Skeleton className="h-8 w-40" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-2/3" />
          <Skeleton className="mt-2 h-7 w-24" />
        </div>
      ))}
    </div>
  );
}

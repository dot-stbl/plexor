import { useMemo, useState } from 'react';
import { Link } from '@tanstack/react-router';
import type { Icon } from '@phosphor-icons/react';
import {
  MagnifyingGlass,
  UsersThree,
  Receipt,
  BookOpen,
  Lifebuoy,
  Cube,
  TreeStructure,
  Scales,
  HardDrives,
  Globe,
  Camera,
  ClockCounterClockwise,
  Gauge,
  Buildings,
  Database,
  Queue,
  ChartBar,
} from '@phosphor-icons/react';
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from '@/shared/ui/primitives/sheet';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { cn } from '@/lib/utils';
import type { AppRoute } from './nav-config';

type Service = {
  name: string;
  description: string;
  icon: Icon;
  /** Present only for shipped routes; absent entries render as «скоро». */
  to?: AppRoute;
};

type Section = { label: string; items: Service[] };

/** Top meta surfaces (project-level, not per-resource). */
const metaCards: Service[] = [
  { name: 'Проект и доступы', description: 'Пользователи, роли, ключи', icon: UsersThree },
  { name: 'Расходы и биллинг', description: 'Потребление и оплата', icon: Receipt, to: '/billing' },
  { name: 'Документация', description: 'Гайды и справочник API', icon: BookOpen },
  { name: 'Поддержка', description: 'Запросы и статус сервисов', icon: Lifebuoy },
];

/**
 * Service catalog. Only shipped routes carry a `to`; everything else is an
 * honest roadmap entry marked «скоро» — no fake links, no invented product claims.
 */
const catalog: Section[] = [
  {
    label: 'Инфраструктура и сеть',
    items: [
      { name: 'Виртуальные машины', description: 'Инстансы, статусы, действия', icon: Cube, to: '/vms' },
      { name: 'Сети и VPC', description: 'Подсети, security groups', icon: TreeStructure, to: '/networks' },
      { name: 'Балансировщик нагрузки', description: 'Распределение трафика', icon: Scales },
      { name: 'Объектное хранилище', description: 'S3-совместимые бакеты', icon: HardDrives },
      { name: 'DNS', description: 'Зоны и записи', icon: Globe },
      { name: 'Снапшоты и образы', description: 'Резервные копии дисков', icon: Camera },
    ],
  },
  {
    label: 'Управление',
    items: [
      { name: 'Журнал аудита', description: 'История действий в проекте', icon: ClockCounterClockwise, to: '/audit' },
      { name: 'Квоты и лимиты', description: 'Ограничения проекта', icon: Gauge },
      { name: 'Организация и команды', description: 'Структура и папки', icon: Buildings },
    ],
  },
  {
    label: 'Платформа данных',
    items: [
      { name: 'Управляемый PostgreSQL', description: 'Кластеры баз данных', icon: Database },
      { name: 'Кэш и очереди', description: 'Redis, брокеры сообщений', icon: Queue },
      { name: 'Аналитика', description: 'Запросы к данным', icon: ChartBar },
    ],
  },
];

function ServiceCard({ service, onNavigate }: { service: Service; onNavigate: () => void }) {
  const { name, description, icon: ItemIcon, to } = service;
  const body = (
    <>
      <div className="flex size-9 shrink-0 items-center justify-center rounded-md bg-muted text-foreground">
        <ItemIcon className="size-4.5" />
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-1.5">
          <span className="truncate text-xs font-medium text-foreground">{name}</span>
          {!to && (
            <StatusPill variant="idle" hideDot className="px-1.5 py-0 text-[10px] font-normal">
              скоро
            </StatusPill>
          )}
        </div>
        <p className="truncate text-[11px] text-muted-foreground">{description}</p>
      </div>
    </>
  );

  // Floating card: hairline border + soft shadow; lifts on hover for shipped routes.
  const base =
    'flex items-center gap-3 rounded-lg border border-border bg-card p-2.5 text-left shadow-sm transition-all';

  if (to) {
    return (
      <Link
        to={to}
        onClick={onNavigate}
        className={cn(base, 'hover:-translate-y-px hover:border-foreground/20 hover:shadow-md')}
      >
        {body}
      </Link>
    );
  }
  return <div className={cn(base, 'cursor-default opacity-65')}>{body}</div>;
}

/**
 * App launcher — a large left Sheet (Plexor's "Центр управления", the analog of
 * a cloud console launcher). Meta cards on top, live service search, then the
 * grouped catalog. Opened from the sidebar's apps button.
 */
export function AppLauncher({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const [query, setQuery] = useState('');
  const q = query.trim().toLowerCase();

  const filtered = useMemo(() => {
    if (!q) return catalog;
    return catalog
      .map((section) => ({
        ...section,
        items: section.items.filter(
          (item) =>
            item.name.toLowerCase().includes(q) || item.description.toLowerCase().includes(q),
        ),
      }))
      .filter((section) => section.items.length > 0);
  }, [q]);

  const close = () => onOpenChange(false);

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent
        side="left"
        className="gap-0 p-0 data-[side=left]:w-[min(760px,94vw)] data-[side=left]:sm:max-w-[min(760px,94vw)]"
      >
        <SheetHeader className="border-b border-border p-5">
          <SheetTitle className="text-sm">Центр управления</SheetTitle>
          <SheetDescription>Все сервисы и разделы проекта Plexor в одном месте</SheetDescription>
        </SheetHeader>

        <div className="flex-1 overflow-y-auto">
          {/* Meta cards */}
          <div className="grid grid-cols-1 gap-2 p-5 pb-4 sm:grid-cols-2">
            {metaCards.map((card) => (
              <ServiceCard key={card.name} service={card} onNavigate={close} />
            ))}
          </div>

          {/* Sticky search over the catalog */}
          <div className="sticky top-0 z-10 border-y border-border bg-popover/95 px-5 py-3 backdrop-blur-sm">
            <div className="relative">
              <MagnifyingGlass className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
              <input
                type="text"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Поиск по сервисам"
                className="h-8 w-full rounded-md border border-input bg-input/20 pr-2 pl-8 text-xs outline-none transition-colors placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/30 dark:bg-input/30"
              />
            </div>
          </div>

          {/* Catalog */}
          <div className="space-y-5 p-5">
            {filtered.map((section) => (
              <section key={section.label} className="space-y-2.5">
                <div className="flex items-center gap-3">
                  <h3 className="text-[11px] font-medium tracking-[0.06em] text-muted-foreground uppercase">
                    {section.label}
                  </h3>
                  <div className="h-px flex-1 bg-border" />
                </div>
                <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                  {section.items.map((service) => (
                    <ServiceCard key={service.name} service={service} onNavigate={close} />
                  ))}
                </div>
              </section>
            ))}
            {filtered.length === 0 && (
              <p className="py-6 text-center text-xs text-muted-foreground">
                Ничего не найдено по запросу «{query}».
              </p>
            )}
          </div>
        </div>
      </SheetContent>
    </Sheet>
  );
}

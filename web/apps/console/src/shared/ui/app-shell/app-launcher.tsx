import { useMemo, useState } from 'react';
import { Link } from '@tanstack/react-router';
import { Dialog } from '@base-ui/react/dialog';
import type { Icon } from '@phosphor-icons/react';
import {
  MagnifyingGlass,
  X,
  SquaresFour,
  BookOpen,
  GearSix,
  SlidersHorizontal,
  Cube,
  Plus,
  Image,
  Camera,
  Hexagon,
  TreeStructure,
  ShieldCheck,
  Globe,
  Scales,
  HardDrives,
  Archive,
  UsersThree,
  Key,
  ChartLine,
  ListDashes,
  ClockCounterClockwise,
  Database,
  Lightning,
  ChartBar,
  Package,
} from '@phosphor-icons/react';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/shared/ui/primitives/card';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { cn } from '@/lib/utils';
import type { AppRoute } from './nav-config';

type Service = {
  name: string;
  description: string;
  icon: Icon;
  /** Present only for shipped routes; absent entries render as «скоро». */
  to?: AppRoute;
  soon?: boolean;
};

type Block = { title: string; caption: string; icon: Icon; soon?: boolean; items: Service[] };
type MetaHub = { name: string; caption: string; icon: Icon; to?: AppRoute; soon?: boolean };

/** Cross-cutting entry hubs (not per-resource groups). */
const META: MetaHub[] = [
  { name: 'Обзор проекта', caption: 'Сводка и действия', icon: SquaresFour, to: '/' },
  { name: 'Документация', caption: 'API (Scalar) и гайды', icon: BookOpen, soon: true },
  { name: 'Администрирование', caption: 'Ноды, провайдеры', icon: GearSix, soon: true },
  { name: 'Настройки', caption: 'Профиль, ключи, тема', icon: SlidersHorizontal, soon: true },
];

/**
 * Resource blocks (from the architecture docs — no billing, self-hosted).
 * `to` = route shipped today; `soon` = roadmap (Phase 2/3), honest + disabled.
 */
const BLOCKS: Block[] = [
  {
    title: 'Вычисления',
    caption: 'ВМ, образы, снапшоты',
    icon: Cube,
    items: [
      { name: 'Виртуальные машины', description: 'Инстансы и статусы', icon: Cube, to: '/vms' },
      { name: 'Создать ВМ', description: 'Мастер, 6 шагов', icon: Plus, soon: true },
      { name: 'Образы', description: 'ОС и свои образы', icon: Image, soon: true },
      { name: 'Снапшоты ВМ', description: 'Копии дисков', icon: Camera, soon: true },
      { name: 'K8s-кластеры', description: 'Managed K3s', icon: Hexagon, soon: true },
    ],
  },
  {
    title: 'Сеть',
    caption: 'VPC, доступ, трафик',
    icon: TreeStructure,
    items: [
      { name: 'VPC и подсети', description: 'Изолированные сети', icon: TreeStructure, to: '/networks' },
      { name: 'Security Groups', description: 'Правила доступа', icon: ShieldCheck, soon: true },
      { name: 'Floating IP', description: 'Внешние адреса', icon: Globe, soon: true },
      { name: 'Балансировщики', description: 'HAProxy L4/L7', icon: Scales, soon: true },
      { name: 'DNS-зоны', description: 'PowerDNS', icon: Globe, soon: true },
    ],
  },
  {
    title: 'Хранилище',
    caption: 'Блочное и объектное',
    icon: HardDrives,
    items: [
      { name: 'Диски', description: 'Block volumes (SSD/HDD)', icon: HardDrives, soon: true },
      { name: 'Бакеты', description: 'S3-совместимые', icon: Archive, soon: true },
      { name: 'Снапшоты', description: 'Копии томов', icon: Camera, soon: true },
    ],
  },
  {
    title: 'Доступы · IAM',
    caption: 'Пользователи и ключи',
    icon: UsersThree,
    items: [
      { name: 'Пользователи', description: 'Учётные записи', icon: UsersThree, soon: true },
      { name: 'Роли', description: 'RBAC-права', icon: ShieldCheck, soon: true },
      { name: 'SSH-ключи', description: 'Доступ к ВМ', icon: Key, soon: true },
      { name: 'API-ключи', description: 'Сервисные аккаунты', icon: Key, soon: true },
    ],
  },
  {
    title: 'Наблюдаемость',
    caption: 'Метрики, логи, аудит',
    icon: ChartLine,
    items: [
      { name: 'Метрики', description: 'Prometheus', icon: ChartLine, soon: true },
      { name: 'Логи', description: 'Поиск по логам', icon: ListDashes, soon: true },
      { name: 'Журнал аудита', description: 'История действий', icon: ClockCounterClockwise, to: '/audit' },
    ],
  },
  {
    title: 'Платформа данных',
    caption: 'Управляемые СУБД',
    icon: Database,
    soon: true,
    items: [
      { name: 'PostgreSQL', description: 'CloudNativePG', icon: Database, soon: true },
      { name: 'Redis', description: 'Кэш и очереди', icon: Lightning, soon: true },
      { name: 'ClickHouse', description: 'Аналитика', icon: ChartBar, soon: true },
      { name: 'Kafka', description: 'Стриминг', icon: Lightning, soon: true },
      { name: 'Container Registry', description: 'Образы контейнеров', icon: Package, soon: true },
    ],
  },
];

const SoonTag = () => (
  <StatusPill variant="idle" hideDot className="shrink-0 px-1.5 py-0 text-[9.5px] font-normal">
    скоро
  </StatusPill>
);

function MetaCard({ hub, onNavigate }: { hub: MetaHub; onNavigate: () => void }) {
  const HubIcon = hub.icon;
  const card = (
    <Card
      size="sm"
      className={cn(
        'flex-row items-center gap-2.5 px-3 transition-colors',
        hub.to ? 'hover:bg-muted/50' : 'opacity-60',
      )}
    >
      <span className="flex size-8 shrink-0 items-center justify-center rounded-md bg-muted text-foreground">
        <HubIcon className="size-4" />
      </span>
      <div className="min-w-0">
        <div className="flex items-center gap-1.5">
          <span className="truncate text-xs font-medium">{hub.name}</span>
          {hub.soon && <SoonTag />}
        </div>
        <div className="truncate text-[10.5px] text-muted-foreground">{hub.caption}</div>
      </div>
    </Card>
  );
  return hub.to ? (
    <Link to={hub.to} onClick={onNavigate} className="block">
      {card}
    </Link>
  ) : (
    card
  );
}

function FnButton({ item, onNavigate }: { item: Service; onNavigate: () => void }) {
  const ItemIcon = item.icon;
  const inner = (
    <>
      <span className="flex size-7 shrink-0 items-center justify-center rounded-md border border-border bg-background text-foreground">
        <ItemIcon className="size-4" />
      </span>
      <span className="min-w-0 flex-1 text-left">
        <span className="block truncate text-xs font-medium">{item.name}</span>
        <span className="block truncate text-[10px] font-normal text-muted-foreground">{item.description}</span>
      </span>
      {item.soon && <SoonTag />}
    </>
  );
  const cls = 'h-auto w-full justify-start gap-2.5 rounded-lg p-2 font-normal';
  if (item.to) {
    return (
      <Button variant="ghost" className={cls} render={<Link to={item.to} onClick={onNavigate} />}>
        {inner}
      </Button>
    );
  }
  return (
    <Button variant="ghost" disabled className={cls}>
      {inner}
    </Button>
  );
}

function BlockCard({ block, onNavigate }: { block: Block; onNavigate: () => void }) {
  const BlockIcon = block.icon;
  return (
    <Card className="gap-0 py-0" data-od-id={`launcher-block-${block.title}`}>
      <CardHeader className="flex flex-row items-center gap-2.5 p-3">
        <span className="flex size-8 shrink-0 items-center justify-center rounded-md bg-muted text-foreground">
          <BlockIcon className="size-[18px]" />
        </span>
        <div className="min-w-0 flex-1">
          <CardTitle className="text-[13px]">{block.title}</CardTitle>
          <CardDescription className="text-[11px]">{block.caption}</CardDescription>
        </div>
        {block.soon && <SoonTag />}
      </CardHeader>
      <CardContent className="grid grid-cols-1 gap-0.5 border-t p-2 sm:grid-cols-2">
        {block.items.map((item) => (
          <FnButton key={item.name} item={item} onNavigate={onNavigate} />
        ))}
      </CardContent>
    </Card>
  );
}

/**
 * App launcher — "Центр управления". Docks to the RIGHT of the collapsed
 * sidebar rail (left-12 = --sidebar-width-icon 3rem) so the rail stays visible.
 * Native shadcn cards, monochrome. Opened from the sidebar's apps button.
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
    if (!q) return BLOCKS;
    return BLOCKS.map((block) => ({
      ...block,
      items: block.items.filter(
        (it) => it.name.toLowerCase().includes(q) || it.description.toLowerCase().includes(q),
      ),
    })).filter((block) => block.items.length > 0 || block.title.toLowerCase().includes(q));
  }, [q]);

  const close = () => onOpenChange(false);

  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        {/* Backdrop starts after the rail (left-12) so the sidebar is never covered. */}
        <Dialog.Backdrop className="fixed inset-y-0 right-0 left-12 z-40 bg-foreground/10 transition-opacity duration-200 data-ending-style:opacity-0 data-starting-style:opacity-0" />
        <Dialog.Popup
          data-od-id="launcher"
          className="fixed inset-y-0 left-12 z-40 flex h-full w-[min(820px,78vw)] flex-col border-r border-border bg-background shadow-xl outline-none transition duration-200 ease-out data-ending-style:-translate-x-4 data-ending-style:opacity-0 data-starting-style:-translate-x-4 data-starting-style:opacity-0"
        >
          <div className="flex items-start justify-between px-5 pt-5 pb-3">
            <div>
              <Dialog.Title className="text-sm font-semibold tracking-tight">Центр управления</Dialog.Title>
              <Dialog.Description className="mt-0.5 text-xs text-muted-foreground">
                Все сервисы и разделы проекта <span className="font-mono">prod-cluster</span>
              </Dialog.Description>
            </div>
            <Dialog.Close
              render={<Button variant="ghost" size="icon-sm" aria-label="Закрыть" />}
            >
              <X className="size-4" />
            </Dialog.Close>
          </div>

          <div className="min-h-0 flex-1 overflow-y-auto px-5 pb-6">
            <div className="grid grid-cols-2 gap-2.5 pb-4 lg:grid-cols-4">
              {META.map((hub) => (
                <MetaCard key={hub.name} hub={hub} onNavigate={close} />
              ))}
            </div>

            <div className="sticky top-0 z-10 -mx-5 mb-4 border-b border-border bg-background px-5 py-2.5">
              <div className="relative">
                <MagnifyingGlass className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  placeholder="Поиск по сервисам и страницам"
                  autoComplete="off"
                  className="h-9 pl-8"
                />
              </div>
            </div>

            <div className="grid grid-cols-1 items-start gap-3 xl:grid-cols-2">
              {filtered.map((block) => (
                <BlockCard key={block.title} block={block} onNavigate={close} />
              ))}
              {filtered.length === 0 && (
                <p className="col-span-full py-10 text-center text-xs text-muted-foreground">
                  Ничего не найдено по запросу «{query}».
                </p>
              )}
            </div>
          </div>
        </Dialog.Popup>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

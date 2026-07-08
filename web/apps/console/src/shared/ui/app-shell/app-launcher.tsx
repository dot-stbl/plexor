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

type Block = {
  title: string;
  caption: string;
  icon: Icon;
  /** Muted accent hue — tints the header gradient + icon-button hover. */
  accent: string;
  gradient: string;
  soon?: boolean;
  items: Service[];
};

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
 * `to` = route shipped today; `soon` = roadmap (Phase 2/3), honest + non-clickable.
 * Muted gradients keep colour as a calm category signal, not a spotlight.
 */
const BLOCKS: Block[] = [
  {
    title: 'Вычисления',
    caption: 'ВМ, образы, снапшоты',
    icon: Cube,
    accent: 'oklch(55% 0.09 262)',
    gradient: 'linear-gradient(135deg, oklch(58% 0.075 260), oklch(47% 0.085 280))',
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
    accent: 'oklch(56% 0.075 205)',
    gradient: 'linear-gradient(135deg, oklch(60% 0.06 198), oklch(49% 0.07 222))',
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
    accent: 'oklch(55% 0.09 300)',
    gradient: 'linear-gradient(135deg, oklch(58% 0.075 305), oklch(48% 0.085 288))',
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
    accent: 'oklch(60% 0.09 55)',
    gradient: 'linear-gradient(135deg, oklch(63% 0.07 66), oklch(55% 0.08 42))',
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
    accent: 'oklch(55% 0.085 165)',
    gradient: 'linear-gradient(135deg, oklch(59% 0.07 160), oklch(49% 0.075 182))',
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
    accent: 'oklch(57% 0.10 10)',
    gradient: 'linear-gradient(135deg, oklch(59% 0.08 15), oklch(49% 0.09 352))',
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

const glassCard = 'border border-border/60 bg-card/60 shadow-sm backdrop-blur-xl';

function MetaCard({ hub, onNavigate }: { hub: MetaHub; onNavigate: () => void }) {
  const HubIcon = hub.icon;
  const body = (
    <>
      <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-muted text-foreground">
        <HubIcon className="size-4" />
      </span>
      <span className="min-w-0">
        <span className="flex items-center gap-1.5">
          <span className="truncate text-xs font-medium leading-tight">{hub.name}</span>
          {hub.soon && (
            <span className="rounded-full bg-muted px-1.5 py-0.5 text-[9.5px] text-muted-foreground">скоро</span>
          )}
        </span>
        <span className="mt-0.5 block truncate text-[10.5px] leading-tight text-muted-foreground">
          {hub.caption}
        </span>
      </span>
    </>
  );
  if (hub.to) {
    return (
      <Link
        to={hub.to}
        onClick={onNavigate}
        className={cn('flex items-center gap-2.5 rounded-xl p-3 transition-all', glassCard, 'hover:-translate-y-0.5 hover:bg-card hover:shadow-md')}
      >
        {body}
      </Link>
    );
  }
  return (
    <div className={cn('flex cursor-default items-center gap-2.5 rounded-xl p-3 opacity-70', glassCard)}>{body}</div>
  );
}

function FnButton({ item, onNavigate }: { item: Service; onNavigate: () => void }) {
  const ItemIcon = item.icon;
  const body = (
    <>
      <span className="flex size-7 shrink-0 items-center justify-center rounded-lg border border-border/50 bg-card/70 text-foreground transition-colors group-hover/fn:border-transparent group-hover/fn:bg-[var(--a)] group-hover/fn:text-white">
        <ItemIcon className="size-[17px]" />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block truncate text-xs font-medium leading-tight">{item.name}</span>
        <span className="block truncate text-[10px] leading-tight text-muted-foreground">{item.description}</span>
      </span>
      {item.soon && (
        <span className="ml-auto shrink-0 rounded-full bg-muted px-1.5 py-0.5 text-[9.5px] text-muted-foreground">
          скоро
        </span>
      )}
    </>
  );
  if (item.to) {
    return (
      <Link to={item.to} onClick={onNavigate} className="group/fn flex items-center gap-2.5 rounded-xl p-2 transition-colors hover:bg-foreground/5">
        {body}
      </Link>
    );
  }
  return <div className="group/fn flex cursor-default items-center gap-2.5 rounded-xl p-2 opacity-60">{body}</div>;
}

function BlockCard({ block, onNavigate }: { block: Block; onNavigate: () => void }) {
  const BlockIcon = block.icon;
  return (
    <div
      className="overflow-hidden rounded-2xl border border-border/60 shadow-xl backdrop-blur-xl"
      style={{ '--a': block.accent } as React.CSSProperties}
      data-od-id={`launcher-block-${block.title}`}
    >
      <div className="flex items-center gap-2.5 px-4 py-3.5 text-white" style={{ background: block.gradient }}>
        <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-white/20 text-white">
          <BlockIcon className="size-[18px]" />
        </span>
        <span className="min-w-0 flex-1">
          <span className="block text-[13px] leading-tight font-semibold [text-shadow:0_1px_2px_rgb(20_20_40/0.25)]">
            {block.title}
          </span>
          <span className="block text-[11px] leading-tight text-white/80">{block.caption}</span>
        </span>
        {block.soon && (
          <span className="rounded-full bg-white/20 px-2 py-0.5 text-[10px] font-medium text-white">скоро</span>
        )}
      </div>
      <div className="grid grid-cols-1 gap-1 border-t border-white/40 bg-card/60 p-2 sm:grid-cols-2">
        {block.items.map((item) => (
          <FnButton key={item.name} item={item} onNavigate={onNavigate} />
        ))}
      </div>
    </div>
  );
}

/**
 * App launcher — "Центр управления". A left, full-height overlay with a blurred
 * backdrop (the real app frosts behind it) and floating glass cards. Opened from
 * the sidebar's apps button.
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
        <Dialog.Backdrop className="fixed inset-0 z-50 bg-background/55 backdrop-blur-lg transition-opacity duration-200 data-ending-style:opacity-0 data-starting-style:opacity-0" />
        <Dialog.Popup
          data-od-id="launcher"
          className="fixed inset-y-0 left-0 z-50 flex h-full w-[min(920px,94vw)] flex-col outline-none transition duration-200 ease-out data-ending-style:-translate-x-6 data-ending-style:opacity-0 data-starting-style:-translate-x-6 data-starting-style:opacity-0"
        >
          <div className="flex items-start justify-between px-6 pt-6 pb-3">
            <div>
              <Dialog.Title className="text-sm font-semibold tracking-tight">Центр управления</Dialog.Title>
              <Dialog.Description className="mt-0.5 text-xs text-muted-foreground">
                Все сервисы и разделы проекта <span className="font-mono">prod-cluster</span>
              </Dialog.Description>
            </div>
            <Dialog.Close
              aria-label="Закрыть"
              className={cn('inline-flex size-8 items-center justify-center rounded-lg text-muted-foreground transition-colors hover:text-foreground', glassCard, 'hover:bg-card')}
            >
              <X className="size-4" />
            </Dialog.Close>
          </div>

          <div className="min-h-0 flex-1 overflow-y-auto px-6 pb-8">
            <div className="grid grid-cols-2 gap-2.5 pb-4 sm:grid-cols-4">
              {META.map((hub) => (
                <MetaCard key={hub.name} hub={hub} onNavigate={close} />
              ))}
            </div>

            <div className="sticky top-0 z-10 -mx-6 mb-4 bg-background/60 px-6 py-2.5 backdrop-blur-md">
              <div className="relative">
                <MagnifyingGlass className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
                <input
                  type="text"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  placeholder="Поиск по сервисам и страницам"
                  autoComplete="off"
                  className="h-9 w-full rounded-xl border border-border/60 bg-card/70 pr-3 pl-9 text-xs text-foreground outline-none backdrop-blur-xl transition-colors placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/25"
                />
              </div>
            </div>

            <div className="grid grid-cols-1 items-start gap-4 lg:grid-cols-2">
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

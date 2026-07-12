import { useNavigate } from '@tanstack/react-router';
import { toast } from 'sonner';
import { Plus, CaretDown, Image, Camera, Key, Lan, HardDrive, type Icon } from '@/shared/ui/icon';
import { Button } from '@/shared/ui/primitives/button';
import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuItem,
  DropdownMenuSeparator,
} from '@/shared/ui/primitives/dropdown-menu';
import type { AppRoute } from './nav-config';

/**
 * Глобальная кнопка «Создать» в верхнем баре (эталон YC «Создать ресурс»):
 * создаёт **глобальные, кросс-секционные ресурсы** — образы, снапшоты,
 * ключи, сети, диски. ВМ и кластеры БД создаются со своих страниц
 * (контекстные CTA), поэтому здесь их нет.
 *
 * Навигация — через `useNavigate` (render-prop `<Link>` внутри Base UI
 * `Menu.Item` не срабатывает как навигация). Пункты без готового маршрута
 * пока показывают toast «скоро». Каталог захардкожен — app-shell не зависит
 * от features/*.
 */
interface CreateItem {
  label: string;
  icon: Icon;
  /** Готовый маршрут создания; без него — toast «скоро». */
  to?: AppRoute;
}

interface CreateGroup {
  label: string;
  items: CreateItem[];
}

const GROUPS: CreateGroup[] = [
  {
    label: 'Infrastructure',
    items: [
      { label: 'Image', icon: Image, to: '/images' },
      { label: 'Snapshot', icon: Camera },
      { label: 'SSH key', icon: Key },
    ],
  },
  {
    label: 'Network and storage',
    items: [
      { label: 'Network (VPC)', icon: Lan, to: '/networks' },
      { label: 'Disk', icon: HardDrive },
    ],
  },
];

export function GlobalCreateMenu() {
  const navigate = useNavigate();

  const onSelect = (item: CreateItem) => {
    if (item.to) void navigate({ to: item.to });
    else toast(`Creating "${item.label}" — coming soon`);
  };

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button size="sm" data-od-id="global-create">
            <Plus />
            Create
            <CaretDown className="opacity-70" />
          </Button>
        }
      />
      <DropdownMenuContent align="end" className="w-60">
        {GROUPS.map((group, gi) => (
          <DropdownMenuGroup key={group.label}>
            {gi > 0 && <DropdownMenuSeparator />}
            <DropdownMenuLabel>{group.label}</DropdownMenuLabel>
            {group.items.map((item) => {
              const IconCmp = item.icon;
              return (
                <DropdownMenuItem key={item.label} onClick={() => onSelect(item)}>
                  <IconCmp />
                  {item.label}
                </DropdownMenuItem>
              );
            })}
          </DropdownMenuGroup>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

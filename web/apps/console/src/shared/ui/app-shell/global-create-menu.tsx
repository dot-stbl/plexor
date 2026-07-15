import { useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  Add,
  Camera,
  HardDrive,
  Image,
  Key,
  KeyboardArrowDown,
  Lan
} from '@nine-thirty-five/material-symbols-react/rounded/700';
import type { Icon } from '@nine-thirty-five/material-symbols-react';
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
  labelKey: string;
  icon: Icon;
  /** Готовый маршрут создания; без него — toast «скоро». */
  to?: AppRoute;
}

interface CreateGroup {
  labelKey: string;
  items: CreateItem[];
}

const GROUPS: CreateGroup[] = [
  {
    labelKey: 'createMenu.infrastructure',
    items: [
      { labelKey: 'createMenu.image', icon: Image, to: '/images' },
      { labelKey: 'createMenu.snapshot', icon: Camera },
      { labelKey: 'createMenu.sshKey', icon: Key },
    ],
  },
  {
    labelKey: 'createMenu.networkStorage',
    items: [
      { labelKey: 'createMenu.network', icon: Lan, to: '/networks' },
      { labelKey: 'createMenu.disk', icon: HardDrive },
    ],
  },
];

export function GlobalCreateMenu() {
  const { t } = useTranslation();
  const navigate = useNavigate();

  const onSelect = (item: CreateItem) => {
    if (item.to) void navigate({ to: item.to });
    else toast(t('common.soon'));
  };

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button size="sm" data-od-id="global-create">
            <Add />
            Create
            <KeyboardArrowDown className="opacity-70" />
          </Button>
        }
      />
      <DropdownMenuContent align="end" className="w-60">
        {GROUPS.map((group, gi) => (
          <DropdownMenuGroup key={group.labelKey}>
            {gi > 0 && <DropdownMenuSeparator />}
            <DropdownMenuLabel>{t(group.labelKey)}</DropdownMenuLabel>
            {group.items.map((item) => {
              const IconCmp = item.icon;
              return (
                <DropdownMenuItem key={item.labelKey} onClick={() => onSelect(item)}>
                  <IconCmp />
                  {t(item.labelKey)}
                </DropdownMenuItem>
              );
            })}
          </DropdownMenuGroup>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

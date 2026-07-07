import type { Icon } from '@phosphor-icons/react';
import {
  SquaresFour,
  Cube,
  TreeStructure,
  Receipt,
  ClockCounterClockwise,
} from '@phosphor-icons/react';

/** Registered product routes the shell navigates between. */
export type AppRoute = '/' | '/vms' | '/networks' | '/billing' | '/audit';

export type NavItem = {
  title: string;
  to: AppRoute;
  icon: Icon;
  description: string;
};

export type NavSection = {
  label: string;
  items: NavItem[];
};

/** Single source of truth for sidebar groups, breadcrumb, and ⌘K palette. */
export const navSections: NavSection[] = [
  {
    label: 'Обзор',
    items: [
      { title: 'Обзор', to: '/', icon: SquaresFour, description: 'Сводка по ресурсам и состоянию' },
    ],
  },
  {
    label: 'Ресурсы',
    items: [
      { title: 'Виртуальные машины', to: '/vms', icon: Cube, description: 'Инстансы, статусы и действия' },
      { title: 'Сети и VPC', to: '/networks', icon: TreeStructure, description: 'VPC, подсети, security groups' },
    ],
  },
  {
    label: 'Управление',
    items: [
      { title: 'Расходы', to: '/billing', icon: Receipt, description: 'Биллинг и потребление ресурсов' },
      { title: 'Журнал аудита', to: '/audit', icon: ClockCounterClockwise, description: 'История действий в проекте' },
    ],
  },
];

export const navItems: NavItem[] = navSections.flatMap((section) => section.items);

export function isActiveRoute(pathname: string, to: AppRoute): boolean {
  return to === '/' ? pathname === '/' : pathname.startsWith(to);
}

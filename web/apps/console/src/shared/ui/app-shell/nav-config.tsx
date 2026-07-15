import type { Icon } from '@nine-thirty-five/material-symbols-react';
import {
  AccountTree,
  Add,
  Archive,
  Balance,
  BarChart,
  Bolt,
  Camera,
  Database,
  DeployedCode,
  Globe,
  Groups,
  HardDisk,
  Hexagon,
  History,
  Image,
  Key,
  List,
  Package,
  ShowChart,
  Stacks,
  Token,
  VerifiedUser
} from '@nine-thirty-five/material-symbols-react/rounded/700';

/** Registered product routes the shell navigates between. */
export type AppRoute = '/' | '/vms' | '/networks' | '/audit' | '/clusters';

export type NavPage = {
  title: string;
  description: string;
  icon: Icon;
  /** Present only for shipped routes; absent = roadmap («скоро»). */
  to?: AppRoute;
};

export type Section = {
  id: string;
  label: string;
  caption: string;
  icon: Icon;
  /** Whole section is roadmap. */
  soon?: boolean;
  pages: NavPage[];
};

/**
 * Single source of truth for BOTH the contextual sidebar (pages of the current
 * section) and the app launcher catalog. Grounded in the architecture docs;
 * self-hosted (no billing). Only pages with `to` are shipped today.
 */
export const SECTIONS: Section[] = [
  {
    id: 'compute',
    label: 'Вычисления',
    caption: 'Кластеры, ВМ, образы',
    icon: DeployedCode,
    pages: [
      { title: 'Виртуальные машины', description: 'Инстансы и статусы', icon: DeployedCode, to: '/vms' },
      { title: 'Вычислительные кластеры', description: 'Пул нод и ресурсы', icon: Stacks, to: '/clusters' },
      { title: 'Создать ВМ', description: 'Мастер, 6 шагов', icon: Add },
      { title: 'Образы', description: 'ОС и свои образы', icon: Image },
      { title: 'Снапшоты ВМ', description: 'Копии дисков', icon: Camera },
      { title: 'K8s-кластеры', description: 'Managed K3s', icon: Hexagon },
    ],
  },
  {
    id: 'network',
    label: 'Сеть',
    caption: 'VPC, доступ, трафик',
    icon: AccountTree,
    pages: [
      { title: 'VPC и подсети', description: 'Изолированные сети', icon: AccountTree, to: '/networks' },
      { title: 'Security Groups', description: 'Правила доступа', icon: VerifiedUser },
      { title: 'Floating IP', description: 'Внешние адреса', icon: Globe },
      { title: 'Балансировщики', description: 'HAProxy L4/L7', icon: Balance },
      { title: 'DNS-зоны', description: 'PowerDNS', icon: Globe },
    ],
  },
  {
    id: 'storage',
    label: 'Хранилище',
    caption: 'Блочное и объектное',
    icon: HardDisk,
    pages: [
      { title: 'Диски', description: 'Block volumes (SSD/HDD)', icon: HardDisk },
      { title: 'Бакеты', description: 'S3-совместимые', icon: Archive },
      { title: 'Снапшоты', description: 'Копии томов', icon: Camera },
    ],
  },
  {
    id: 'iam',
    label: 'Доступы · IAM',
    caption: 'Пользователи и ключи',
    icon: Groups,
    pages: [
      { title: 'Пользователи', description: 'Учётные записи', icon: Groups },
      { title: 'Роли', description: 'RBAC-права', icon: VerifiedUser },
      { title: 'SSH-ключи', description: 'Доступ к ВМ', icon: Key },
      { title: 'API-ключи', description: 'Сервисные аккаунты', icon: Token },
    ],
  },
  {
    id: 'observability',
    label: 'Наблюдаемость',
    caption: 'Метрики, логи, аудит',
    icon: ShowChart,
    pages: [
      { title: 'Метрики', description: 'Prometheus', icon: ShowChart },
      { title: 'Логи', description: 'Поиск по логам', icon: List },
      { title: 'Журнал аудита', description: 'История действий', icon: History, to: '/audit' },
    ],
  },
  {
    id: 'data',
    label: 'Платформа данных',
    caption: 'Управляемые СУБД',
    icon: Database,
    soon: true,
    pages: [
      { title: 'PostgreSQL', description: 'CloudNativePG', icon: Database },
      { title: 'Redis', description: 'Кэш и очереди', icon: Bolt },
      { title: 'ClickHouse', description: 'Аналитика', icon: BarChart },
      { title: 'Kafka', description: 'Стриминг', icon: Bolt },
      { title: 'Container Registry', description: 'Образы контейнеров', icon: Package },
    ],
  },
];

export function isActiveRoute(pathname: string, to: AppRoute): boolean {
  return to === '/' ? pathname === '/' : pathname.startsWith(to);
}

/** First shipped route of a section (used as its entry point), if any. */
export function sectionPrimaryRoute(section: Section): AppRoute | undefined {
  return section.pages.find((p) => p.to)?.to;
}

/** Which section the current route belongs to (null on the overview/home). */
export function sectionIdForPathname(pathname: string): string | null {
  for (const section of SECTIONS) {
    if (section.pages.some((p) => p.to && isActiveRoute(pathname, p.to))) return section.id;
  }
  return null;
}

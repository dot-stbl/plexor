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
  /** i18n key for the title (resolved at render time with t()). */
  title: string;
  /** i18n key for the description. */
  description: string;
  icon: Icon;
  /** Present only for shipped routes; absent = roadmap. */
  to?: AppRoute;
};

export type Section = {
  id: string;
  /** i18n key for the section label. */
  label: string;
  /** i18n key for the section caption. */
  caption: string;
  icon: Icon;
  /** Whole section is roadmap. */
  soon?: boolean;
  pages: NavPage[];
};

/**
 * Single source of truth for BOTH the contextual sidebar (pages of the current
 * section) and the app launcher catalog. All `label`/`title`/`description`/
 * `caption` fields are i18n KEYS (e.g. 'nav.vmsTitle'), resolved with t() at
 * render time. Grounded in the architecture docs; self-hosted (no billing).
 */
export const SECTIONS: Section[] = [
  {
    id: 'compute',
    label: 'nav.sections.compute',
    caption: 'nav.sections.computeCaption',
    icon: DeployedCode,
    pages: [
      { title: 'nav.vmsTitle', description: 'nav.vmsDesc', icon: DeployedCode, to: '/vms' },
      { title: 'nav.clustersTitle', description: 'nav.clustersDesc', icon: Stacks, to: '/clusters' },
      { title: 'nav.createVmTitle', description: 'nav.createVmDesc', icon: Add },
      { title: 'nav.imagesTitle', description: 'nav.imagesDesc', icon: Image },
      { title: 'nav.snapshotsTitle', description: 'nav.snapshotsDesc', icon: Camera },
      { title: 'nav.k8sTitle', description: 'nav.k8sDesc', icon: Hexagon },
    ],
  },
  {
    id: 'network',
    label: 'nav.sections.network',
    caption: 'nav.sections.networkCaption',
    icon: AccountTree,
    pages: [
      { title: 'nav.vpcTitle', description: 'nav.vpcDesc', icon: AccountTree, to: '/networks' },
      { title: 'nav.sgTitle', description: 'nav.sgDesc', icon: VerifiedUser },
      { title: 'nav.fipTitle', description: 'nav.fipDesc', icon: Globe },
      { title: 'nav.lbTitle', description: 'nav.lbDesc', icon: Balance },
      { title: 'nav.dnsTitle', description: 'nav.dnsDesc', icon: Globe },
    ],
  },
  {
    id: 'storage',
    label: 'nav.sections.storage',
    caption: 'nav.sections.storageCaption',
    icon: HardDisk,
    pages: [
      { title: 'nav.disksTitle', description: 'nav.disksDesc', icon: HardDisk },
      { title: 'nav.bucketsTitle', description: 'nav.bucketsDesc', icon: Archive },
      { title: 'nav.volSnapshotsTitle', description: 'nav.volSnapshotsDesc', icon: Camera },
    ],
  },
  {
    id: 'iam',
    label: 'nav.sections.access',
    caption: 'nav.sections.accessCaption',
    icon: Groups,
    pages: [
      { title: 'nav.usersTitle', description: 'nav.usersDesc', icon: Groups },
      { title: 'nav.rolesTitle', description: 'nav.rolesDesc', icon: VerifiedUser },
      { title: 'nav.sshKeysTitle', description: 'nav.sshKeysDesc', icon: Key },
      { title: 'nav.apiKeysTitle', description: 'nav.apiKeysDesc', icon: Token },
    ],
  },
  {
    id: 'observability',
    label: 'nav.sections.observability',
    caption: 'nav.sections.observabilityCaption',
    icon: ShowChart,
    pages: [
      { title: 'nav.metricsTitle', description: 'nav.metricsDesc', icon: ShowChart },
      { title: 'nav.logsTitle', description: 'nav.logsDesc', icon: List },
      { title: 'nav.auditTitle', description: 'nav.auditDesc', icon: History, to: '/audit' },
    ],
  },
  {
    id: 'data',
    label: 'nav.sections.databases',
    caption: 'nav.sections.databasesCaption',
    icon: Database,
    soon: true,
    pages: [
      { title: 'nav.postgresTitle', description: 'nav.postgresDesc', icon: Database },
      { title: 'nav.redisTitle', description: 'nav.redisDesc', icon: Bolt },
      { title: 'nav.clickhouseTitle', description: 'nav.clickhouseDesc', icon: BarChart },
      { title: 'nav.kafkaTitle', description: 'nav.kafkaDesc', icon: Bolt },
      { title: 'nav.registryTitle', description: 'nav.registryDesc', icon: Package },
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

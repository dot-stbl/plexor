import {
  BookOpenText,
  Box,
  Building2,
  CircleHelp,
  Cloud,
  Database,
  FolderTree,
  Globe,
  HardDrive,
  Info,
  KeyRound,
  Languages,
  Layers,
  LifeBuoy,
  type LucideIcon,
  Network,
  Plug,
  Rocket,
  ScrollText,
  Server,
  ShieldCheck,
  ShoppingBag,
  Terminal,
  Wrench,
} from 'lucide-react';

const iconMap: Record<string, LucideIcon> = {
  BookOpenText,
  Box,
  Building2,
  CircleHelp,
  Cloud,
  Database,
  FolderTree,
  Globe,
  HardDrive,
  Info,
  KeyRound,
  Languages,
  Layers,
  LifeBuoy,
  Network,
  Plug,
  Rocket,
  ScrollText,
  Server,
  ShieldCheck,
  ShoppingBag,
  Terminal,
  Wrench,
};

/**
 * Maps `icon:` names from frontmatter / meta.json to components for the
 * sidebar page tree. Unknown lucide names render no icon (loader contract).
 */
export function getIcon(icon: string | undefined): React.ReactNode {
  const IconComponent = icon === undefined ? undefined : iconMap[icon];

  if (IconComponent === undefined) {
    return undefined;
  }

  return <IconComponent className="size-4" aria-hidden />;
}
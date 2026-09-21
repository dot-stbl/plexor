import type { Icon } from '@nine-thirty-five/material-symbols-react';
import {
  AccountTree,
  Archive,
  ArrowOutward,
  AvTimer,
  Block,
  Build,
  Check,
  Cloud,
  DeployedCode,
  Forum,
  Globe,
  Group,
  HardDisk,
  Help,
  History,
  Hub,
  Image,
  Key,
  Layers,
  List,
  Lock,
  Memory,
  MenuBook,
  Package,
  Rocket,
  Router,
  Settings,
  ShoppingBag,
  ShowChart,
  Stack,
  Storefront,
  Terminal,
  Token,
  VerifiedUser,
  Webhook,
} from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * Maps `icon:` names from frontmatter / meta.json to Material Symbols Rounded 700
 * components. Material Symbols is the Plexor app's icon system — using the
 * same weight/family keeps docs visually consistent with console.
 *
 * `iconMap` covers every glyph the docs sidebar and landing page reference
 * from MDX frontmatter. Unknown names render no icon (loader contract).
 */
const iconMap: Record<string, Icon> = {
  AccountTree,
  Archive,
  ArrowOutward,
  AvTimer,
  Block,
  Build,
  Check,
  Cloud,
  DeployedCode,
  Forum,
  Globe,
  Group,
  HardDisk,
  Help,
  History,
  Hub,
  Image,
  Key,
  Layers,
  List,
  Lock,
  Memory,
  MenuBook,
  Package,
  Rocket,
  Router,
  Settings,
  ShoppingBag,
  ShowChart,
  Stack,
  Storefront,
  Terminal,
  Token,
  VerifiedUser,
  Webhook,
};

export function getIcon(icon: string | undefined): React.ReactNode {
  const IconComponent = icon === undefined ? undefined : iconMap[icon];

  if (IconComponent === undefined) {
    return undefined;
  }

  return <IconComponent className="size-4" aria-hidden />;
}
import type { Icon } from '@nine-thirty-five/material-symbols-react';
import { Hub } from '@nine-thirty-five/material-symbols-react/rounded/700';

/**
 * Maps `icon:` names from frontmatter / meta.json to Material Symbols Rounded 700
 * components. Material Symbols is the Plexor app's icon system — using the
 * same weight/family keeps docs visually consistent with console.
 *
 * Only the glyphs the docs sidebar + landing actually reference from MDX
 * frontmatter are registered here. Unknown names render no icon (loader
 * contract). Add new entries when a new frontmatter `icon:` field needs
 * one — don't pre-import.
 */
const iconMap: Record<string, Icon> = {
  Hub,
};

export function getIcon(icon: string | undefined): React.ReactNode {
  const IconComponent = icon === undefined ? undefined : iconMap[icon];

  if (IconComponent === undefined) {
    return undefined;
  }

  return <IconComponent className="size-4" aria-hidden />;
}
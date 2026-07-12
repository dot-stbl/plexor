import { useTheme } from '@/shared/lib/preferences-provider';
import { Button } from '@/shared/ui/primitives/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/shared/ui/primitives/dropdown-menu';
import { Check, Moon, Sun } from '@/shared/ui/icon';
import { cn } from '@/lib/utils';

const ICONS = {
  light: Sun,
  dark: Moon,
  system: Sun, // Sun re-used for system (filled) — no half-circle icon available
} as const;

const LABELS = {
  light: 'Светлая',
  dark: 'Тёмная',
  system: 'Системная',
} as const;

const ORDER: Array<'light' | 'dark' | 'system'> = ['light', 'dark', 'system'];

/**
 * Theme switcher. Trigger shows the icon for the CURRENT theme value
 * (one icon, not a swap). DropdownMenu offers the three options with
 * a check on the active one. No CSS animations, no duplicate icons —
 * the previous version was clever but visually confusing.
 *
 * Material Symbols Rounded don't expose a `weight` prop (they're filled,
 * not stroked). Selected state is signalled by the check icon and a
 * background change instead.
 */
export function ModeToggle() {
  const { theme, setTheme } = useTheme();
  const Icon = ICONS[theme];

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="outline" size="icon" aria-label="Сменить тему">
            <Icon className="size-4" />
            <span className="sr-only">{LABELS[theme]}</span>
          </Button>
        }
      />
      <DropdownMenuContent align="end">
        {ORDER.map((value) => {
          const ItemIcon = ICONS[value];
          const isActive = theme === value;
          return (
            <DropdownMenuItem
              key={value}
              onClick={() => setTheme(value)}
              className={cn('gap-2', isActive && 'bg-foreground/5 font-medium')}
            >
              <ItemIcon className="size-3.5" />
              <span>{LABELS[value]}</span>
              {isActive && <Check className="ml-auto size-3.5" />}
            </DropdownMenuItem>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

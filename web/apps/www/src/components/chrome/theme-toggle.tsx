import { Check, LightMode } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { useThemePicker } from '@plexor/ui';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

/**
 * Theme toggle — a `DropdownMenu` anchored on a ghost icon button, listing
 * the three built-in presets with a checkmark on the active one (spec
 * §2.2, fixing diagnosis point #5: the old `ThemePickerButton` cycled
 * blind with no visible state). Preset data + persistence come from
 * `@plexor/ui`'s `useThemePicker()` — no local preset logic duplicated.
 */
const PRESET_LABELS: Readonly<Record<string, string>> = {
  'plexor-default-light': 'Light',
  'plexor-default-dark': 'Dark',
  'plexor-noir': 'Noir',
};

export function ThemeToggle({ className }: { className?: string }) {
  const { presets, activePresetId, setPreset } = useThemePicker();

  return (
    <DropdownMenu>
      {/* NB: the icon must be DropdownMenuTrigger's own child, not nested
          inside the `render` Button — a pre-existing quirk in
          dropdown-menu-trigger.tsx drops the render element's own children
          when cloning it (see console's app-sidebar.test.tsx for the same
          note on its own account-menu trigger). */}
      <DropdownMenuTrigger
        render={<Button variant="ghost" size="icon" aria-label="Switch theme" className={className} />}
      >
        <LightMode />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {presets.map((preset) => (
          <DropdownMenuItem key={preset.id} onClick={() => setPreset(preset.id)}>
            <span className="flex-1">{PRESET_LABELS[preset.id] ?? preset.name}</span>
            {preset.id === activePresetId ? <Check className="size-3.5" /> : null}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

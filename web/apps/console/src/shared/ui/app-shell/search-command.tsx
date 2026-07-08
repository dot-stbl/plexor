import { useNavigate } from '@tanstack/react-router';
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/shared/ui/primitives/command';
import { navSections, type AppRoute } from './nav-config';

/**
 * Search modal opened from the sidebar's top group (and ⌘K/Ctrl+K).
 * Controlled by the sidebar; navigates to a section on select.
 */
export function SearchCommand({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const navigate = useNavigate();

  const go = (to: AppRoute) => {
    onOpenChange(false);
    void navigate({ to });
  };

  return (
    <CommandDialog
      open={open}
      onOpenChange={onOpenChange}
      title="Поиск"
      description="Поиск по разделам и ресурсам консоли"
    >
      <CommandInput placeholder="Поиск по разделам, ВМ, сетям…" />
      <CommandList>
        <CommandEmpty>Ничего не найдено.</CommandEmpty>
        {navSections.map((section) => (
          <CommandGroup key={section.label} heading={section.label}>
            {section.items.map((item) => {
              const ItemIcon = item.icon;
              return (
                <CommandItem
                  key={item.to}
                  value={`${item.title} ${item.description}`}
                  onSelect={() => go(item.to)}
                >
                  <ItemIcon weight="bold" />
                  <span>{item.title}</span>
                  <span className="ml-auto truncate text-xs text-muted-foreground">
                    {item.description}
                  </span>
                </CommandItem>
              );
            })}
          </CommandGroup>
        ))}
      </CommandList>
    </CommandDialog>
  );
}

import { useNavigate } from '@tanstack/react-router';
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/shared/ui/primitives/command';
import { navSections } from './nav-config';

/** ⌘K palette — jumps between product sections. State lives in AppShell. */
export function CommandMenu({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const navigate = useNavigate();

  return (
    <CommandDialog
      open={open}
      onOpenChange={onOpenChange}
      title="Командная палитра"
      description="Быстрый переход по разделам консоли"
    >
      <CommandInput placeholder="Перейти к разделу…" />
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
                  onSelect={() => {
                    onOpenChange(false);
                    void navigate({ to: item.to });
                  }}
                >
                  <ItemIcon className="size-4" />
                  <span>{item.title}</span>
                </CommandItem>
              );
            })}
          </CommandGroup>
        ))}
      </CommandList>
    </CommandDialog>
  );
}

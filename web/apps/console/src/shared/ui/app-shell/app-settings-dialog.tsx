import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/shared/ui/primitives/dialog';
import { ModeToggle } from '@/shared/ui/primitives/theme-toggle';

/**
 * Settings modal — opened from the sidebar's user menu. Houses "Оформление"
 * (appearance) with the theme switch, which used to live in the top bar.
 */
export function AppSettingsDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md" data-od-id="settings-dialog">
        <DialogHeader>
          <DialogTitle className="text-sm">Настройки</DialogTitle>
          <DialogDescription>Оформление и параметры аккаунта</DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <section className="space-y-2" data-od-id="settings-appearance">
            <h3 className="text-[11px] font-medium tracking-[0.06em] text-muted-foreground uppercase">
              Оформление
            </h3>
            <div className="flex items-center justify-between rounded-lg border border-border p-3">
              <div className="min-w-0">
                <div className="text-xs font-medium">Тема</div>
                <div className="text-[11px] text-muted-foreground">Светлая, тёмная или системная</div>
              </div>
              <ModeToggle />
            </div>
          </section>
        </div>
      </DialogContent>
    </Dialog>
  );
}

import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { Alert, AlertTitle, AlertDescription, AlertAction } from '@/shared/ui/primitives/alert';
import { Button } from '@/shared/ui/primitives/button';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { DeployedCode, Refresh, Search } from '@nine-thirty-five/material-symbols-react/rounded/700';

/** Loading skeleton — 5 placeholder rows shaped like the VM table. */
export function VmSkeleton() {
  return (
    <div data-od-id="vms-skeleton" className="flex flex-col gap-2">
      {Array.from({ length: 5 }).map((_, i) => (
        <Skeleton key={i} className="h-10 w-full" />
      ))}
    </div>
  );
}

interface VmErrorBannerProps {
  error: unknown;
  onRetry: () => void;
}

/** Error banner with retry button. */
export function VmErrorBanner({ error, onRetry }: VmErrorBannerProps) {
  const message = error instanceof Error ? error.message : 'Неизвестная ошибка';
  return (
    <Alert variant="destructive" data-od-id="vms-error">
      <div>
        <AlertTitle>Не удалось загрузить список VM</AlertTitle>
        <AlertDescription>{message}</AlertDescription>
      </div>
      <AlertAction>
        <Button variant="outline" size="sm" onClick={onRetry}>
          <Refresh />
          Повторить
        </Button>
      </AlertAction>
    </Alert>
  );
}

interface VmEmptyStateProps {
  onCreate?: () => void;
}

/** Empty state — zero VMs in the project (before any filter). */
export function VmEmptyState({ onCreate }: VmEmptyStateProps) {
  return (
    <EmptyState
      data-od-id="vms-empty"
      icon={DeployedCode}
      title="Виртуальных машин пока нет"
      description="Создайте первую ВМ, чтобы начать. Доступны Ubuntu, Debian, Alpine и свои образы."
      action={onCreate ? <Button onClick={onCreate}>Создать ВМ</Button> : undefined}
    />
  );
}

interface VmNoResultsStateProps {
  onReset: () => void;
}

/** Empty state — filters returned nothing (VMs exist but none match). */
export function VmNoResultsState({ onReset }: VmNoResultsStateProps) {
  return (
    <EmptyState
      data-od-id="vms-no-results"
      icon={Search}
      title="Ничего не найдено"
      description="Попробуйте изменить фильтры или сбросить их."
      action={<Button variant="outline" size="sm" onClick={onReset}>Сбросить фильтры</Button>}
    />
  );
}
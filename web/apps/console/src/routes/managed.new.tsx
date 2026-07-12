import { useMemo, useState } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { ArrowLeft, FloppyDisk } from '@phosphor-icons/react';
import { toast } from 'sonner';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { Switch } from '@/shared/ui/primitives/switch';
import { Badge } from '@/shared/ui/primitives/badge';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/primitives/select';
import { Field, FieldDescription, FieldGroup, FieldLabel } from '@/shared/ui/primitives/field';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbSeparator,
} from '@/shared/ui/primitives/breadcrumb';
import { PageHeader } from '@/shared/ui/app-shell';
import type { Runtime } from '@/features/databases';
import {
  DB_KIND_LABEL,
  RUNTIME_META,
  defaultRuntime,
  managedRoute,
  runtimeOptions,
  RuntimePicker,
  useEngines,
  useRuntimeHosts,
} from '@/features/databases';

export const Route = createFileRoute('/managed/new')({
  validateSearch: (search: Record<string, unknown>): { engine?: string } => ({
    engine: typeof search.engine === 'string' ? search.engine : undefined,
  }),
  component: CreateClusterPage,
});

const STORAGE_OPTIONS = [10, 20, 50, 100, 200, 500];

/**
 * Создание кластера БД — отдельная СТРАНИЦА (не модалка). Движок (предвыбран
 * из ?engine), параметры + бэкапы, runtime-picker (valid ∩ available), review
 * с занятым источником и binding-контрактом. Создание — заглушка (toast).
 */
function CreateClusterPage() {
  const { engine: engineParam } = Route.useSearch();
  const navigate = useNavigate();
  const { engines } = useEngines();
  const { hosts } = useRuntimeHosts();

  const [engineId, setEngineId] = useState<string>(engineParam ?? engines[0]?.id ?? '');
  const [name, setName] = useState('');
  const [storageGb, setStorageGb] = useState<string>('50');
  const [backups, setBackups] = useState(true);
  const [picked, setPicked] = useState<Runtime | undefined>(undefined);

  const engine = useMemo(() => engines.find((e) => e.id === engineId), [engines, engineId]);
  const options = useMemo(() => (engine ? runtimeOptions(engine, hosts) : []), [engine, hosts]);

  // Effective runtime без useEffect: сохраняем выбор, если валиден; иначе дефолт.
  const runtime = useMemo<Runtime | undefined>(() => {
    if (picked && options.some((o) => o.runtime === picked && o.enabled)) return picked;
    return defaultRuntime(options);
  }, [picked, options]);

  const targetNode = options.find((o) => o.runtime === runtime)?.nodes[0];
  const effectiveName = name.trim() || `${engine?.id ?? 'db'}-1`;
  const backupsAllowed = engine?.supportsBackups ?? false;
  const canCreate = Boolean(engine && runtime && targetNode);
  const backRoute = managedRoute(engineId);

  const handleCreate = () => {
    if (!engine || !runtime) return;
    toast(`Создаю кластер ${effectiveName}`, {
      description: `${engine.name} · ${RUNTIME_META[runtime].label} · ${targetNode ?? '—'}`,
    });
    void navigate({ to: backRoute });
  };

  return (
    <main data-od-id="managed-new">
      <Breadcrumb className="mx-auto w-full max-w-3xl px-6 pt-6 lg:px-8">
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink render={<Link to={backRoute} />}>{engine?.name ?? 'Платформа данных'}</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink render={<Link to="/managed/new" search={{ engine: engineId }} />}>Новый кластер</BreadcrumbLink>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      <PageHeader
        title={engine ? `Создать кластер ${engine.name}` : 'Создать кластер'}
        description="Движок отвязан от рантайма — выберите, где его поднять."
        actions={
          <Button variant="ghost" nativeButton={false} render={<Link to={backRoute} />}>
            <ArrowLeft />
            Назад
          </Button>
        }
      />

      <div className="mx-auto w-full max-w-3xl space-y-3 px-6 py-6 lg:px-8">
        {/* Движок */}
        <Card className="gap-0 p-0">
          <CardHeader className="gap-0.5 border-b border-border p-4">
            <CardTitle className="text-sm">Движок</CardTitle>
            <CardDescription>Предустановленный managed-движок. Plexor занимает готовый артефакт.</CardDescription>
          </CardHeader>
          <CardContent className="p-4">
            <Select
              items={engines.map((e) => ({ value: e.id, label: e.name }))}
              value={engineId}
              onValueChange={(v) => setEngineId(v ?? '')}
            >
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Выберите движок" />
              </SelectTrigger>
              <SelectContent>
                {engines.map((e) => (
                  <SelectItem key={e.id} value={e.id}>
                    {e.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {engine && (
              <div className="mt-2 flex flex-wrap items-center gap-2">
                <Badge variant="secondary">{DB_KIND_LABEL[engine.kind]}</Badge>
                <Badge variant="outline">v{engine.version}</Badge>
                <span className="font-mono text-[11px] text-muted-foreground">{engine.source}</span>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Параметры */}
        <Card className="gap-0 p-0">
          <CardHeader className="gap-0.5 border-b border-border p-4">
            <CardTitle className="text-sm">Параметры</CardTitle>
            <CardDescription>Имя, диск, бэкапы и рантайм.</CardDescription>
          </CardHeader>
          <CardContent className="p-4">
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="db-name">Имя</FieldLabel>
                <Input
                  id="db-name"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder={`${engine?.id ?? 'db'}-1`}
                />
                <FieldDescription>Используется как имя кластера и в DNS.</FieldDescription>
              </Field>

              <Field>
                <FieldLabel htmlFor="db-storage">Диск</FieldLabel>
                <Select
                  items={STORAGE_OPTIONS.map((g) => ({ value: String(g), label: `${g} GB` }))}
                  value={storageGb}
                  onValueChange={(v) => setStorageGb(v ?? '50')}
                >
                  <SelectTrigger id="db-storage">
                    <SelectValue placeholder="Размер диска" />
                  </SelectTrigger>
                  <SelectContent>
                    {STORAGE_OPTIONS.map((g) => (
                      <SelectItem key={g} value={String(g)}>
                        {g} GB
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>

              <Field orientation="horizontal">
                <FieldLabel htmlFor="db-backups">Автобэкапы</FieldLabel>
                <Switch
                  id="db-backups"
                  checked={backupsAllowed && backups}
                  onCheckedChange={setBackups}
                  disabled={!backupsAllowed}
                />
              </Field>
              {!backupsAllowed && (
                <FieldDescription>Этот движок пока без встроенных бэкапов.</FieldDescription>
              )}

              <Field>
                <FieldLabel>Рантайм</FieldLabel>
                <FieldDescription>
                  Показаны все рантаймы; недоступные — с причиной. Дефолт — direct.
                </FieldDescription>
                <RuntimePicker options={options} value={runtime} onChange={setPicked} />
              </Field>
            </FieldGroup>
          </CardContent>
        </Card>

        {/* Review */}
        {engine && (
          <div className="rounded-lg border border-border bg-surface-2 p-3 text-xs">
            <div className="mb-1.5 font-medium text-foreground">Что произойдёт</div>
            <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-muted-foreground">
              <dt>Источник</dt>
              <dd className="font-mono text-foreground">{engine.source}</dd>
              <dt>Размещение</dt>
              <dd className="text-foreground">
                {runtime ? RUNTIME_META[runtime].label : '—'}
                {targetNode ? <> · <MonoNum muted>{targetNode}</MonoNum></> : null}
              </dd>
              <dt>Отдаёт (binding)</dt>
              <dd className="font-mono text-foreground">{engine.connstring}</dd>
            </dl>
          </div>
        )}

        <div className="flex items-center justify-between">
          <Button variant="outline" nativeButton={false} render={<Link to={backRoute} />}>
            Отмена
          </Button>
          <Button onClick={handleCreate} disabled={!canCreate}>
            <FloppyDisk />
            Создать кластер
          </Button>
        </div>
      </div>
    </main>
  );
}

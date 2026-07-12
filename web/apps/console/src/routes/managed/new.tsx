import { useMemo, useState } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { ArrowLeft, FloppyDisk } from '@/shared/ui/icon';
import { toast } from 'sonner';
import { Button } from '@/shared/ui/primitives/button';
import { Input } from '@/shared/ui/primitives/input';
import { Switch } from '@/shared/ui/primitives/switch';
import { Badge } from '@/shared/ui/primitives/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/primitives/select';
import { FieldRow } from '@/shared/ui/primitives/field-row';
import { SegmentedControl } from '@/shared/ui/primitives/segmented-control';
import { SelectableCardGrid } from '@/shared/ui/primitives/selectable-card-grid';
import { SummaryPanel, SummaryRow } from '@/shared/ui/primitives/summary-panel';
import { Stepper } from '@/shared/ui/primitives/stepper';
import { SizeField } from '@/shared/ui/primitives/size-field';
import { PasswordInput } from '@/shared/ui/primitives/password-input';
import { RepeatableRows } from '@/shared/ui/primitives/repeatable-rows';
import { Slider } from '@/shared/ui/primitives/slider';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Size, SizeUtils } from '@/shared/ui/primitives/size';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { PageTemplate } from '@/shared/ui/app-shell';
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
  staticData: { crumb: 'New cluster' },
  validateSearch: (search: Record<string, unknown>): { engine?: string } => ({
    engine: typeof search.engine === 'string' ? search.engine : undefined,
  }),
  component: CreateClusterPage,
});

/**
 * Пресеты — быстрый старт; «Своя конфигурация» — точные ресурсы (self-hosted:
 * ограничены доступными ресурсами ноды рантайма). Размеры бинарные (GiB,
 * степени двойки) — как физически бьётся диск/память, а не круглые 500.
 */
const PRESETS = [
  { value: 'small', title: '2 vCPU · 8 GiB', subtitle: 'disk 64 GiB', cpu: 2, ram: 8, disk: 64 },
  { value: 'medium', title: '4 vCPU · 16 GiB', subtitle: 'disk 256 GiB', cpu: 4, ram: 16, disk: 256 },
  { value: 'large', title: '8 vCPU · 32 GiB', subtitle: 'disk 512 GiB', cpu: 8, ram: 32, disk: 512 },
];

/**
 * Создание кластера БД — эталонная YC-форма: `FieldRow` + `SegmentedControl`
 * (Пресеты/Своя конфигурация) + `SelectableCardGrid` + `RuntimePicker`, справа —
 * липкая `SummaryPanel`. Крошки — в баре (staticData). Создание — заглушка (toast).
 */
function CreateClusterPage() {
  const { engine: engineParam } = Route.useSearch();
  const navigate = useNavigate();
  const { engines } = useEngines();
  const { hosts } = useRuntimeHosts();

  const [engineId, setEngineId] = useState<string>(engineParam ?? engines[0]?.id ?? '');
  const [name, setName] = useState('');
  const [resMode, setResMode] = useState<string>('preset');
  const [preset, setPreset] = useState<string>('small');
  const [cpu, setCpu] = useState<string>('2');
  const [ramBytes, setRamBytes] = useState<number>(SizeUtils.gibToBytes(8));
  const [diskBytes, setDiskBytes] = useState<number>(SizeUtils.gibToBytes(64));
  const [backups, setBackups] = useState(true);
  const [backupDays, setBackupDays] = useState(7);
  const [pwMode, setPwMode] = useState<string>('manual');
  const [password, setPassword] = useState('');
  const [labels, setLabels] = useState<{ key: string; value: string }[]>([]);
  const [picked, setPicked] = useState<Runtime | undefined>(undefined);

  const engine = useMemo(() => engines.find((e) => e.id === engineId), [engines, engineId]);
  const options = useMemo(() => (engine ? runtimeOptions(engine, hosts) : []), [engine, hosts]);

  // Effective runtime без useEffect: сохраняем выбор, если валиден; иначе дефолт.
  const runtime = useMemo<Runtime | undefined>(() => {
    if (picked && options.some((o) => o.runtime === picked && o.enabled)) return picked;
    return defaultRuntime(options);
  }, [picked, options]);

  const selPreset = PRESETS.find((p) => p.value === preset) ?? PRESETS[0];
  const resources =
    resMode === 'custom'
      ? { cpu: Number(cpu), ramBytes, diskBytes }
      : {
          cpu: selPreset.cpu,
          ramBytes: SizeUtils.gibToBytes(selPreset.ram),
          diskBytes: SizeUtils.gibToBytes(selPreset.disk),
        };

  const targetNode = options.find((o) => o.runtime === runtime)?.nodes[0];
  const effectiveName = name.trim() || `${engine?.id ?? 'db'}-1`;
  const backupsAllowed = engine?.supportsBackups ?? false;
  const canCreate = Boolean(engine && runtime && targetNode);
  const backRoute = managedRoute(engineId);

  const handleCreate = () => {
    if (!engine || !runtime) return;
    toast(`Creating cluster ${effectiveName}`, {
      description: `${engine.name} · ${resources.cpu} vCPU / ${SizeUtils.format(resources.ramBytes)} / ${SizeUtils.format(resources.diskBytes)} · ${RUNTIME_META[runtime].label} · ${targetNode ?? '—'}`,
    });
    void navigate({ to: backRoute });
  };

  return (
    <PageTemplate
      data-od-id="managed-new"
      width="full"
      title={engine ? `Create ${engine.name} cluster` : 'Create cluster'}
      description="The engine is decoupled from the runtime — choose where to bring it up."
      actions={
        <Button variant="ghost" nativeButton={false} render={<Link to={backRoute} />}>
          <ArrowLeft />
          Back
        </Button>
      }
    >
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
        {/* Форма */}
        <div className="min-w-0 space-y-3">
          <Card>
            <CardHeader className="border-b border-border">
              <CardTitle className="text-sm">Engine</CardTitle>
              <CardDescription>Preinstalled managed engine. Plexor takes over the ready-made artifact.</CardDescription>
            </CardHeader>
            <CardContent>
              <FieldRow label="Engine" htmlFor="db-engine" required>
                <Select
                  items={engines.map((e) => ({ value: e.id, label: e.name }))}
                  value={engineId}
                  onValueChange={(v) => setEngineId(v ?? '')}
                >
                  <SelectTrigger id="db-engine" className="w-full">
                    <SelectValue placeholder="Select an engine" />
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
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant="secondary">{DB_KIND_LABEL[engine.kind]}</Badge>
                    <Badge variant="outline">v{engine.version}</Badge>
                  </div>
                )}
              </FieldRow>
            </CardContent>
          </Card>

          {/* Ресурсы */}
          <Card>
            <CardHeader className="border-b border-border">
              <CardTitle className="text-sm">Compute resources</CardTitle>
              <CardDescription>A preset or an exact configuration matched to the node's resources.</CardDescription>
            </CardHeader>
            <CardContent className="divide-y divide-border">
              <FieldRow label="Configuration">
                <SegmentedControl
                  aria-label="Resource configuration mode"
                  value={resMode}
                  onValueChange={setResMode}
                  options={[
                    { value: 'preset', label: 'Presets' },
                    { value: 'custom', label: 'Custom' },
                  ]}
                />
              </FieldRow>

              {resMode === 'preset' ? (
                <FieldRow label="Preset" required>
                  <SelectableCardGrid value={preset} onValueChange={setPreset} options={PRESETS} columns={3} />
                </FieldRow>
              ) : (
                <>
                  <FieldRow label="vCPU" htmlFor="res-cpu" required help="Number of cores. Limited by the runtime node's available resources.">
                    <Stepper id="res-cpu" value={Number(cpu)} onValueChange={(n) => setCpu(String(n))} min={1} max={64} suffix="vCPU" />
                  </FieldRow>
                  <FieldRow label="RAM" htmlFor="res-ram" required help="Exact size, down to the MiB (like in Proxmox). Limited by the runtime node's available memory.">
                    <SizeField
                      id="res-ram"
                      bytes={ramBytes}
                      onValueChange={setRamBytes}
                      units={['MiB', 'GiB']}
                      min={SizeUtils.gibToBytes(1)}
                      max={SizeUtils.gibToBytes(1024)}
                    />
                  </FieldRow>
                  <FieldRow label="Disk" htmlFor="res-disk" required help="Exact size, down to the MiB — how the volume is physically partitioned. Limited by the node's capacity.">
                    <SizeField
                      id="res-disk"
                      bytes={diskBytes}
                      onValueChange={setDiskBytes}
                      units={['MiB', 'GiB', 'TiB']}
                      min={SizeUtils.gibToBytes(1)}
                      max={SizeUtils.gibToBytes(16384)}
                    />
                  </FieldRow>
                </>
              )}
            </CardContent>
          </Card>

          {/* Параметры */}
          <Card>
            <CardHeader className="border-b border-border">
              <CardTitle className="text-sm">Parameters</CardTitle>
              <CardDescription>Name, access, backups, and runtime.</CardDescription>
            </CardHeader>
            <CardContent className="divide-y divide-border">
              <FieldRow
                label="Name"
                htmlFor="db-name"
                required
                help="Latin letters, digits, and hyphens. Used as the cluster name and in DNS."
              >
                <Input
                  id="db-name"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder={`${engine?.id ?? 'db'}-1`}
                />
              </FieldRow>

              <FieldRow
                label="Automatic backups"
                htmlFor="db-backups"
                help="Scheduled backups (WAL/snapshots) retained on a schedule."
                description={!backupsAllowed ? 'This engine has no built-in backups yet.' : undefined}
              >
                <Switch
                  id="db-backups"
                  checked={backupsAllowed && backups}
                  onCheckedChange={setBackups}
                  disabled={!backupsAllowed}
                />
              </FieldRow>

              {backupsAllowed && backups && (
                <FieldRow label="Backup retention" help="How long automatic backups are kept, in days.">
                  <div className="flex items-center gap-3">
                    <Slider
                      value={backupDays}
                      min={1}
                      max={60}
                      onValueChange={(v) => setBackupDays(Array.isArray(v) ? (v[0] ?? 7) : v)}
                      className="max-w-[240px]"
                    />
                    <span className="shrink-0 text-xs text-muted-foreground">
                      <MonoNum>{backupDays}</MonoNum> days
                    </span>
                  </div>
                </FieldRow>
              )}

              <FieldRow label="Password" htmlFor="db-pw" required help="Database superuser password.">
                <SegmentedControl
                  aria-label="Password entry method"
                  value={pwMode}
                  onValueChange={setPwMode}
                  options={[
                    { value: 'manual', label: 'Enter manually' },
                    { value: 'generate', label: 'Generate' },
                  ]}
                />
                {pwMode === 'manual' ? (
                  <PasswordInput
                    id="db-pw"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder="Password"
                  />
                ) : (
                  <p className="text-xs text-muted-foreground">
                    A strong password will be generated and shown once after creation.
                  </p>
                )}
              </FieldRow>

              <FieldRow label="Labels" help="key=value pairs for grouping and search.">
                <RepeatableRows
                  rows={labels}
                  onChange={setLabels}
                  newRow={() => ({ key: '', value: '' })}
                  addLabel="Add label"
                  renderRow={(row, update) => (
                    <div className="flex gap-2">
                      <Input
                        value={row.key}
                        onChange={(e) => update({ ...row, key: e.target.value })}
                        placeholder="key"
                        className="flex-1"
                      />
                      <Input
                        value={row.value}
                        onChange={(e) => update({ ...row, value: e.target.value })}
                        placeholder="value"
                        className="flex-1"
                      />
                    </div>
                  )}
                />
              </FieldRow>

              <FieldRow
                label="Runtime"
                description="All runtimes are shown; unavailable ones list the reason. Default is direct."
              >
                <RuntimePicker options={options} value={runtime} onChange={setPicked} />
              </FieldRow>
            </CardContent>
          </Card>

          <div className="flex items-center justify-between">
            <Button variant="outline" nativeButton={false} render={<Link to={backRoute} />}>
              Cancel
            </Button>
            <Button onClick={handleCreate} disabled={!canCreate}>
              <FloppyDisk />
              Create cluster
            </Button>
          </div>
        </div>

        {/* Сводка (липкая) */}
        {engine && (
          <SummaryPanel title="What will be deployed">
            <div>
              <SummaryRow label="Engine">
                {engine.name} · v{engine.version}
              </SummaryRow>
              <SummaryRow label="Resources">
                <MonoNum>{resources.cpu}</MonoNum> vCPU · <Size bytes={resources.ramBytes} />
              </SummaryRow>
              <SummaryRow label="Disk">
                <Size bytes={resources.diskBytes} />
              </SummaryRow>
              <SummaryRow label="Placement">
                {runtime ? RUNTIME_META[runtime].label : '—'}
                {targetNode ? ` · ${targetNode}` : ''}
              </SummaryRow>
              <SummaryRow label="Backups">
                {backupsAllowed && backups ? (
                  <>
                    on · <MonoNum>{backupDays}</MonoNum> days
                  </>
                ) : (
                  '—'
                )}
              </SummaryRow>
            </div>
            <div className="mt-2 space-y-1.5 border-t border-border pt-2">
              <div>
                <div className="mb-0.5 text-[11px] text-muted-foreground">Source</div>
                <code className="block break-all font-mono text-[11px] text-foreground">{engine.source}</code>
              </div>
              <div>
                <div className="mb-0.5 text-[11px] text-muted-foreground">Provides (binding)</div>
                <code className="block break-all font-mono text-[11px] text-foreground">{engine.connstring}</code>
              </div>
            </div>
          </SummaryPanel>
        )}
      </div>
    </PageTemplate>
  );
}

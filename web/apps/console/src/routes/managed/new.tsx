import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { ArrowBack, Save } from '@nine-thirty-five/material-symbols-react/rounded/700';
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
  const { t } = useTranslation();
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
    toast(t('managed.new.toast', { name: effectiveName }), {
      description: `${engine.name} · ${resources.cpu} vCPU / ${SizeUtils.format(resources.ramBytes)} / ${SizeUtils.format(resources.diskBytes)} · ${RUNTIME_META[runtime].label} · ${targetNode ?? '—'}`,
    });
    void navigate({ to: backRoute });
  };

  return (
    <PageTemplate
      data-od-id="managed-new"
      width="full"
      title={engine ? t('managed.new.title', { engine: engine.name }) : t('managed.new.fallbackTitle')}
      description={t('managed.new.form.description')}
      actions={
        <Button variant="ghost" nativeButton={false} render={<Link to={backRoute} />}>
          <ArrowBack />
          {t('common.back')}
        </Button>
      }
    >
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
        {/* Форма */}
        <div className="min-w-0 space-y-3">
          <Card>
            <CardHeader className="border-b border-border">
              <CardTitle className="text-sm">{t('managed.new.form.engineTitle')}</CardTitle>
              <CardDescription>{t('managed.new.form.engineCardDescription')}</CardDescription>
            </CardHeader>
            <CardContent className="flex flex-col gap-2">
              <FieldRow label={t('managed.new.form.engineLabel')} htmlFor="db-engine" required>
                <Select
                  items={engines.map((e) => ({ value: e.id, label: e.name }))}
                  value={engineId}
                  onValueChange={(v) => setEngineId(v ?? '')}
                >
                  <SelectTrigger id="db-engine" className="w-full">
                    <SelectValue placeholder={t('managed.new.form.enginePlaceholder')} />
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
              <CardTitle className="text-sm">{t('managed.new.form.resourcesTitle')}</CardTitle>
              <CardDescription>{t('managed.new.form.resourcesCardDescription')}</CardDescription>
            </CardHeader>
            <CardContent className="flex flex-col gap-2">
              <FieldRow label={t('managed.new.form.configurationLabel')}>
                <SegmentedControl
                  aria-label={t('managed.new.form.resModeAria')}
                  value={resMode}
                  onValueChange={setResMode}
                  options={[
                    { value: 'preset', label: t('managed.new.form.presetsOption') },
                    { value: 'custom', label: t('managed.new.form.customOption') },
                  ]}
                />
              </FieldRow>

              {resMode === 'preset' ? (
                <FieldRow label={t('managed.new.form.presetLabel')} required>
                  <SelectableCardGrid value={preset} onValueChange={setPreset} options={PRESETS} columns={3} />
                </FieldRow>
              ) : (
                <>
                  <FieldRow label="vCPU" htmlFor="res-cpu" required help={t('managed.new.form.cpuHelp')}>
                    <Stepper id="res-cpu" value={Number(cpu)} onValueChange={(n) => setCpu(String(n))} min={1} max={64} suffix="vCPU" />
                  </FieldRow>
                  <FieldRow label={t('managed.new.form.ramLabel')} htmlFor="res-ram" required help={t('managed.new.form.ramHelp')}>
                    <SizeField
                      id="res-ram"
                      bytes={ramBytes}
                      onValueChange={setRamBytes}
                      units={['MiB', 'GiB']}
                      min={SizeUtils.gibToBytes(1)}
                      max={SizeUtils.gibToBytes(1024)}
                    />
                  </FieldRow>
                  <FieldRow label={t('managed.new.form.diskLabel')} htmlFor="res-disk" required help={t('managed.new.form.diskHelp')}>
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
              <CardTitle className="text-sm">{t('managed.new.form.parametersTitle')}</CardTitle>
              <CardDescription>{t('managed.new.form.parametersCardDescription')}</CardDescription>
            </CardHeader>
            <CardContent className="flex flex-col gap-2">
              <FieldRow
                label={t('managed.new.form.nameLabel')}
                htmlFor="db-name"
                required
                help={t('managed.new.form.nameHelp')}
              >
                <Input
                  id="db-name"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder={`${engine?.id ?? 'db'}-1`}
                />
              </FieldRow>

              <FieldRow
                label={t('managed.new.form.backupsLabel')}
                htmlFor="db-backups"
                help={t('managed.new.form.backupsHelp')}
                description={!backupsAllowed ? t('managed.new.form.backupsUnsupported') : undefined}
              >
                <Switch
                  id="db-backups"
                  checked={backupsAllowed && backups}
                  onCheckedChange={setBackups}
                  disabled={!backupsAllowed}
                />
              </FieldRow>

              {backupsAllowed && backups && (
                <FieldRow label={t('managed.new.form.retentionLabel')} help={t('managed.new.form.retentionHelp')}>
                  <div className="flex items-center gap-3">
                    <Slider
                      value={backupDays}
                      min={1}
                      max={60}
                      onValueChange={(v) => setBackupDays(Array.isArray(v) ? (v[0] ?? 7) : v)}
                      className="max-w-[240px]"
                    />
                    <span className="shrink-0 text-xs text-muted-foreground">
                      <MonoNum>{backupDays}</MonoNum> {t('managed.new.form.days')}
                    </span>
                  </div>
                </FieldRow>
              )}

              <FieldRow label={t('managed.new.form.passwordLabel')} htmlFor="db-pw" required help={t('managed.new.form.passwordHelp')}>
                <SegmentedControl
                  aria-label={t('managed.new.form.pwModeAria')}
                  value={pwMode}
                  onValueChange={setPwMode}
                  options={[
                    { value: 'manual', label: t('managed.new.form.pwManualOption') },
                    { value: 'generate', label: t('managed.new.form.pwGenerateOption') },
                  ]}
                />
                {pwMode === 'manual' ? (
                  <PasswordInput
                    id="db-pw"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder={t('managed.new.form.passwordPlaceholder')}
                  />
                ) : (
                  <p className="text-xs text-muted-foreground">
                    {t('managed.new.form.pwGenerateHint')}
                  </p>
                )}
              </FieldRow>

              <FieldRow label={t('managed.new.form.labelsLabel')} help={t('managed.new.form.labelsHelp')}>
                <RepeatableRows
                  rows={labels}
                  onChange={setLabels}
                  newRow={() => ({ key: '', value: '' })}
                  addLabel={t('managed.new.form.addLabel')}
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
                label={t('managed.new.form.runtimeLabel')}
                description={t('managed.new.form.runtimeDescription')}
              >
                <RuntimePicker options={options} value={runtime} onChange={setPicked} />
              </FieldRow>
            </CardContent>
          </Card>

          <div className="flex items-center justify-between">
            <Button variant="outline" nativeButton={false} render={<Link to={backRoute} />}>
              {t('common.cancel')}
            </Button>
            <Button onClick={handleCreate} disabled={!canCreate}>
              <Save />
              {t('managed.new.form.createButton')}
            </Button>
          </div>
        </div>

        {/* Сводка (липкая) */}
        {engine && (
          <SummaryPanel
            title={t('managed.new.form.summaryTitle')}
            footer={
              <div className="w-full space-y-1.5">
                <div>
                  <div className="mb-0.5 text-[11px] text-muted-foreground">{t('managed.new.form.sourceLabel')}</div>
                  <code className="block break-all font-mono text-[11px] text-foreground">{engine.source}</code>
                </div>
                <div>
                  <div className="mb-0.5 text-[11px] text-muted-foreground">{t('managed.new.form.providesLabel')}</div>
                  <code className="block break-all font-mono text-[11px] text-foreground">{engine.connstring}</code>
                </div>
              </div>
            }
          >
            <div>
              <SummaryRow label={t('managed.new.form.summaryEngine')}>
                {engine.name} · v{engine.version}
              </SummaryRow>
              <SummaryRow label={t('managed.new.form.summaryResources')}>
                <MonoNum>{resources.cpu}</MonoNum> vCPU · <Size bytes={resources.ramBytes} />
              </SummaryRow>
              <SummaryRow label={t('managed.new.form.summaryDisk')}>
                <Size bytes={resources.diskBytes} />
              </SummaryRow>
              <SummaryRow label={t('managed.new.form.summaryPlacement')}>
                {runtime ? RUNTIME_META[runtime].label : '—'}
                {targetNode ? ` · ${targetNode}` : ''}
              </SummaryRow>
              <SummaryRow label={t('managed.new.form.summaryBackups')}>
                {backupsAllowed && backups ? (
                  t('managed.new.form.summaryBackupsOn', { count: backupDays })
                ) : (
                  '—'
                )}
              </SummaryRow>
            </div>
          </SummaryPanel>
        )}
      </div>
    </PageTemplate>
  );
}

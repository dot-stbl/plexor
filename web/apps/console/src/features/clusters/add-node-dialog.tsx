import { useMemo, useState } from 'react';
import { Close, Download, Key, Terminal } from '@nine-thirty-five/material-symbols-react/rounded/700';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/shared/ui/primitives/dialog';
import { Button } from '@/shared/ui/primitives/button';
import { Field, FieldDescription, FieldGroup, FieldLabel } from '@/shared/ui/primitives/field';
import { Input } from '@/shared/ui/primitives/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/shared/ui/primitives/select';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { issueJoinToken, useGetCluster } from './use-clusters';
import type { JoinToken, NodeRole } from './cluster-types';

const TTL_OPTIONS = [
  { value: '1', label: '1 день' },
  { value: '7', label: '7 дней' },
  { value: '30', label: '30 дней' },
];

interface AddNodeDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  clusterId: string;
}

/**
 * Self-hosted onboarding flow for adding a Plexor.NodeAgent to a cluster.
 * Generates a join token, prints the `plx node join <token>` command,
 * links to the ISO download, and to docs.plexor.dev.
 *
 * The token is shown once. After the user clicks "Создать", the dialog
 * reveals the token + command and offers a copy button.
 */
export function AddNodeDialog({ open, onOpenChange, clusterId }: AddNodeDialogProps) {
  const { cluster } = useGetCluster(clusterId);
  const [label, setLabel] = useState('');
  const [role, setRole] = useState<NodeRole>('compute');
  const [ttlDays, setTtlDays] = useState('7');
  const [issued, setIssued] = useState<JoinToken | null>(null);

  const command = useMemo(() => {
    if (!issued || !cluster) return null;
    // Note: real plx CLI reads the token from a flag, not argv. Shown as-is.
    return `plx node join --token ${issued.token} --endpoint ${cluster.endpoint}`;
  }, [issued, cluster]);

  const reset = () => {
    setLabel('');
    setRole('compute');
    setTtlDays('7');
    setIssued(null);
  };

  const close = (next: boolean) => {
    if (!next) reset();
    onOpenChange(next);
  };

  if (!cluster) return null;

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Key className="size-4" />
            Подключить нод к кластеру
          </DialogTitle>
          <DialogDescription>
            Сгенерируйте join-токен. Установите Plexor ISO на целевой машине,
            затем запустите команду <code className="rounded bg-muted px-1 font-mono text-[11px]">plx node join</code>{' '}
            с этим токеном. Нод появится в списке после первого heartbeat (≤ 2 мин).
          </DialogDescription>
        </DialogHeader>

        {!issued ? (
          <>
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="add-node-label">Метка токена</FieldLabel>
                <Input
                  id="add-node-label"
                  value={label}
                  onChange={(e) => setLabel(e.target.value)}
                  placeholder="например, edge-pop-amsterdam"
                />
                <FieldDescription>
                  Только для идентификации. Не влияет на сам нод.
                </FieldDescription>
              </Field>
              <Field>
                <FieldLabel>Роль ноды</FieldLabel>
                <Select
                  items={[
                    { value: 'compute', label: 'Compute' },
                    { value: 'control', label: 'Control-plane' },
                  ]}
                  value={role}
                  onValueChange={(v) => setRole((v as NodeRole) ?? 'compute')}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="compute">Compute</SelectItem>
                    <SelectItem value="control">Control-plane (HA)</SelectItem>
                  </SelectContent>
                </Select>
                <FieldDescription>
                  Control-plane только если вы строите отказоустойчивый control HA.
                </FieldDescription>
              </Field>
              <Field>
                <FieldLabel>Срок жизни</FieldLabel>
                <Select
                  items={TTL_OPTIONS}
                  value={ttlDays}
                  onValueChange={(v) => setTtlDays(v ?? '7')}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {TTL_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FieldDescription>
                  По истечении токен становится недействительным и нод не подключится.
                </FieldDescription>
              </Field>
            </FieldGroup>
            <DialogFooter>
              <Button variant="outline" onClick={() => close(false)}>
                Отмена
              </Button>
              <Button onClick={() => setIssued(issueJoinToken(cluster.id, { label: label || 'node', intendedRole: role, ttlDays: Number(ttlDays) }))} disabled={!label}>
                <Key />
                Сгенерировать токен
              </Button>
            </DialogFooter>
          </>
        ) : (
          <div className="space-y-3">
            <FieldGroup>
              <Field>
                <FieldLabel>Токен (показывается один раз)</FieldLabel>
                <div className="flex items-center gap-1">
                  <Input
                    readOnly
                    value={issued.token}
                    className="font-mono text-[11px]"
                    onClick={(e) => e.currentTarget.select()}
                  />
                  <Button
                    variant="outline"
                    size="icon"
                    aria-label="Копировать токен"
                    onClick={() => {
                      void navigator.clipboard.writeText(issued.token);
                    }}
                  >
                    <Download className="size-4" />
                  </Button>
                </div>
                <FieldDescription>
                  Действует до {new Date(issued.expiresAt).toLocaleString('ru-RU')}.{' '}
                  После подключения токен остаётся в списке, но повторно использовать его нельзя.
                </FieldDescription>
              </Field>
              <Field>
                <FieldLabel>Команда на ноде</FieldLabel>
                <pre className="overflow-x-auto rounded-md border border-border bg-muted/50 p-2 font-mono text-[11px] leading-relaxed">
                  {command}
                </pre>
                <FieldDescription>
                  Запускается на машине, загружённой с Plexor ISO (см. ниже).
                </FieldDescription>
              </Field>
            </FieldGroup>

            <div className="rounded-md border border-border bg-muted/30 p-3 text-xs">
              <div className="mb-1.5 font-medium">Шаги на ноде</div>
              <ol className="ml-4 list-decimal space-y-1 text-muted-foreground">
                <li>
                  Скачайте Plexor ISO:{' '}
                  <a
                    href="https://plexor.dev/download/iso"
                    target="_blank"
                    rel="noreferrer"
                    className="font-mono text-foreground underline underline-offset-2"
                  >
                    plexor.dev/download/iso
                  </a>
                </li>
                <li>Загрузите целевой сервер с этого ISO.</li>
                <li>После загрузки нода появится в списке (status: <MonoNum>pending</MonoNum>).</li>
                <li>
                  Выполните команду выше. Нод появится как <MonoNum>ready</MonoNum> после первого heartbeat.
                </li>
              </ol>
            </div>

            <DialogFooter>
              <Button
                variant="outline"
                onClick={() => {
                  if (command) void navigator.clipboard.writeText(command);
                }}
              >
                <Terminal />
                Копировать команду
              </Button>
              <Button onClick={() => close(false)}>
                <Close />
                Готово
              </Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation();
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

  const ttlOptions = [
    { value: '1', label: t('clusters.addNode.ttl.day1') },
    { value: '7', label: t('clusters.addNode.ttl.day7') },
    { value: '30', label: t('clusters.addNode.ttl.day30') },
  ];

  if (!cluster) return null;

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Key className="size-4" />
            {t('clusters.addNode.title')}
          </DialogTitle>
          <DialogDescription>
            {t('clusters.addNode.descriptionBefore')}{' '}
            <code className="rounded bg-muted px-1 font-mono text-[11px]">plx node join</code>{' '}
            {t('clusters.addNode.descriptionAfter')}
          </DialogDescription>
        </DialogHeader>

        {!issued ? (
          <>
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="add-node-label">{t('clusters.addNode.labelField')}</FieldLabel>
                <Input
                  id="add-node-label"
                  value={label}
                  onChange={(e) => setLabel(e.target.value)}
                  placeholder={t('clusters.addNode.labelPlaceholder')}
                />
                <FieldDescription>
                  {t('clusters.addNode.labelHint')}
                </FieldDescription>
              </Field>
              <Field>
                <FieldLabel>{t('clusters.addNode.roleField')}</FieldLabel>
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
                  {t('clusters.addNode.roleHint')}
                </FieldDescription>
              </Field>
              <Field>
                <FieldLabel>{t('clusters.addNode.ttlField')}</FieldLabel>
                <Select
                  items={ttlOptions}
                  value={ttlDays}
                  onValueChange={(v) => setTtlDays(v ?? '7')}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {ttlOptions.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <FieldDescription>
                  {t('clusters.addNode.ttlHint')}
                </FieldDescription>
              </Field>
            </FieldGroup>
            <DialogFooter>
              <Button variant="outline" onClick={() => close(false)}>
                {t('common.cancel')}
              </Button>
              <Button onClick={() => setIssued(issueJoinToken(cluster.id, { label: label || 'node', intendedRole: role, ttlDays: Number(ttlDays) }))} disabled={!label}>
                <Key />
                {t('clusters.addNode.generateToken')}
              </Button>
            </DialogFooter>
          </>
        ) : (
          <div className="space-y-3">
            <FieldGroup>
              <Field>
                <FieldLabel>{t('clusters.addNode.tokenLabel')}</FieldLabel>
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
                    aria-label={t('clusters.addNode.copyToken')}
                    onClick={() => {
                      void navigator.clipboard.writeText(issued.token);
                    }}
                  >
                    <Download className="size-4" />
                  </Button>
                </div>
                <FieldDescription>
                  {t('clusters.addNode.tokenValidUntil', { date: new Date(issued.expiresAt).toLocaleString('ru-RU') })}
                </FieldDescription>
              </Field>
              <Field>
                <FieldLabel>{t('clusters.addNode.commandField')}</FieldLabel>
                <pre className="overflow-x-auto rounded-md border border-border bg-muted/50 p-2 font-mono text-[11px] leading-relaxed">
                  {command}
                </pre>
                <FieldDescription>
                  {t('clusters.addNode.commandHint')}
                </FieldDescription>
              </Field>
            </FieldGroup>

            <div className="rounded-md border border-border bg-muted/30 p-3 text-xs">
              <div className="mb-1.5 font-medium">{t('clusters.addNode.steps.title')}</div>
              <ol className="ml-4 list-decimal space-y-1 text-muted-foreground">
                <li>
                  {t('clusters.addNode.steps.download')}{' '}
                  <a
                    href="https://plexor.dev/download/iso"
                    target="_blank"
                    rel="noreferrer"
                    className="font-mono text-foreground underline underline-offset-2"
                  >
                    plexor.dev/download/iso
                  </a>
                </li>
                <li>{t('clusters.addNode.steps.boot')}</li>
                <li>{t('clusters.addNode.steps.appear')} (status: <MonoNum>pending</MonoNum>).</li>
                <li>
                  {t('clusters.addNode.steps.runBefore')} <MonoNum>ready</MonoNum> {t('clusters.addNode.steps.runAfter')}
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
                {t('clusters.addNode.copyCommand')}
              </Button>
              <Button onClick={() => close(false)}>
                <Close />
                {t('common.done')}
              </Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
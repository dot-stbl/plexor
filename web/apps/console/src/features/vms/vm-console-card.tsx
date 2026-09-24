import { useTranslation } from 'react-i18next';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/shared/ui/primitives/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/ui/primitives/tabs';
import { ConsolePlaceholder } from '@/features/vms/vm-console-placeholder';
import { TerminalPlaceholder } from '@/features/vms/vm-terminal-placeholder';

interface VmConsoleCardProps {
  /** VM id — threaded into both placeholders. */
  vmId: string;
  /** Which tab to show on first render. Defaults to "console". */
  defaultTab?: 'console' | 'terminal';
}

/**
 * "Console & terminal" card on the VM detail page. Hosts two tabs —
 * Console (noVNC-style VNC stream) and Terminal (xterm.js SSH shell).
 *
 * Both tabs render inert placeholders until the Plexor.NodeAgent ships
 * the corresponding endpoints; the placeholders are honest about that
 * (a styled "preview unavailable" panel and a real xterm.js surface
 * with a "would connect to ws://nodeagent/..." banner). The card is
 * the integration point a future backend slot will fill — when the
 * agent lands, swap each placeholder for a real client; nothing else
 * on the page changes.
 */
export function VmConsoleCard({ vmId, defaultTab = 'console' }: VmConsoleCardProps) {
  const { t } = useTranslation();

  return (
    <Card data-od-id="vm-console-card">
      <CardHeader className="border-b border-border">
        <CardTitle className="text-sm">{t('vms.detail.console.title')}</CardTitle>
        <CardDescription>{t('vms.detail.console.description')}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <Tabs defaultValue={defaultTab}>
          <TabsList>
            <TabsTrigger value="console">{t('vms.detail.console.tabConsole')}</TabsTrigger>
            <TabsTrigger value="terminal">{t('vms.detail.console.tabTerminal')}</TabsTrigger>
          </TabsList>
          <TabsContent value="console" className="mt-3">
            <ConsolePlaceholder vmId={vmId} />
          </TabsContent>
          <TabsContent value="terminal" className="mt-3">
            <TerminalPlaceholder vmId={vmId} />
          </TabsContent>
        </Tabs>
      </CardContent>
    </Card>
  );
}
import { createFileRoute, Link } from '@tanstack/react-router';
import {
  Alert,
  AlertAction,
  AlertDescription,
  AlertTitle,
} from '@/shared/ui/primitives/alert';
import { Avatar, AvatarFallback } from '@/shared/ui/primitives/avatar';
import { Badge } from '@/shared/ui/primitives/badge';
import { Button } from '@/shared/ui/primitives/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/shared/ui/primitives/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/shared/ui/primitives/dialog';
import { Input } from '@/shared/ui/primitives/input';
import { Label } from '@/shared/ui/primitives/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/shared/ui/primitives/table';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '@/shared/ui/primitives/tabs';
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from '@/shared/ui/primitives/tooltip';

export const Route = createFileRoute('/')({
  component: ComponentShowcase,
});

const SAMPLE_TENANTS = [
  { id: 'tnt_8c2a', name: 'acme-prod', projects: 4, vms: 38, status: 'running' as const },
  { id: 'tnt_1f7b', name: 'beta-eval', projects: 1, vms: 3, status: 'pending' as const },
  { id: 'tnt_44de', name: 'internal-tools', projects: 2, vms: 12, status: 'ok' as const },
  { id: 'tnt_a912', name: 'legacy-poc', projects: 0, vms: 0, status: 'stopped' as const },
  { id: 'tnt_0e55', name: 'demo-broken', projects: 1, vms: 0, status: 'failed' as const },
];

const STATUS_LABEL: Record<string, string> = {
  running: 'running',
  pending: 'pending',
  ok: 'active',
  stopped: 'stopped',
  failed: 'failed',
};

function ComponentShowcase() {
  return (
    <main className="mx-auto max-w-5xl space-y-10 p-8">
      <header className="space-y-2 border-b border-border pb-6">
        <div className="eyebrow">Plexor Portal · design system showcase</div>
        <h1 className="text-2xl font-semibold tracking-tight">
          Component library · Base UI registry
        </h1>
        <p className="text-muted-foreground text-sm">
          60 shadcn primitives + 2 custom (StatusPill, MonoNum). Tokens from{' '}
          <span className="font-mono text-xs">.agents/docs/design/styles.css</span>.
        </p>
      </header>

      {/* ── Buttons ─────────────────────────────── */}
      <Section title="Button" caption="xs · sm · md · lg · xl — flat, no translate on active">
        <Demo label="Variants">
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="default">Primary</Button>
            <Button variant="outline">Outline</Button>
            <Button variant="secondary">Secondary</Button>
            <Button variant="ghost">Ghost</Button>
            <Button variant="destructive">Destructive</Button>
            <Button variant="link">Link</Button>
            <Button disabled>Disabled</Button>
          </div>
        </Demo>
        <Demo label="Sizes">
          <div className="flex flex-wrap items-center gap-2">
            <Button size="xs">xs · 24px</Button>
            <Button size="sm">sm · 28px</Button>
            <Button size="md">md · 32px</Button>
            <Button size="lg">lg · 40px</Button>
            <Button size="xl">xl · 48px</Button>
          </div>
        </Demo>
        <Demo label="Icon-only">
          <div className="flex flex-wrap items-center gap-2">
            <Button size="icon-xs" variant="ghost" aria-label="More">
              ⋯
            </Button>
            <Button size="icon-sm" variant="outline" aria-label="Edit">
              ✎
            </Button>
            <Button size="icon" variant="outline" aria-label="Settings">
              ⚙
            </Button>
            <Button size="icon-lg" variant="outline" aria-label="Delete">
              ✕
            </Button>
          </div>
        </Demo>
      </Section>

      {/* ── Status pills + badges ──────────────── */}
      <Section title="StatusPill + Badge" caption="ok/err/warn/idle — dot + label, density for table columns">
        <Demo label="StatusPill variants">
          <div className="flex flex-wrap items-center gap-2">
            <StatusPill variant="running">running</StatusPill>
            <StatusPill variant="ok">active</StatusPill>
            <StatusPill variant="pending">pending</StatusPill>
            <StatusPill variant="warn">degraded</StatusPill>
            <StatusPill variant="err">failed</StatusPill>
            <StatusPill variant="idle">stopped</StatusPill>
            <StatusPill variant="idle" hideDot>no-dot</StatusPill>
          </div>
        </Demo>
        <Demo label="Badge">
          <div className="flex flex-wrap items-center gap-2">
            <Badge>Default</Badge>
            <Badge variant="secondary">Secondary</Badge>
            <Badge variant="outline">Outline</Badge>
            <Badge variant="destructive">Destructive</Badge>
          </div>
        </Demo>
        <Demo label="MonoNum — tabular for dense columns">
          <div className="text-sm space-y-1">
            <div className="flex items-center justify-between gap-4">
              <span className="text-muted-foreground">IP</span>
              <MonoNum>10.128.42.17</MonoNum>
            </div>
            <div className="flex items-center justify-between gap-4">
              <span className="text-muted-foreground">ID</span>
              <MonoNum muted>tnt_8c2a4ef9</MonoNum>
            </div>
            <div className="flex items-center justify-between gap-4">
              <span className="text-muted-foreground">Size</span>
              <MonoNum>128.45 GB</MonoNum>
            </div>
            <div className="flex items-center justify-between gap-4">
              <span className="text-muted-foreground">Duration</span>
              <MonoNum muted>2h 47m</MonoNum>
            </div>
          </div>
        </Demo>
      </Section>

      {/* ── Cards ───────────────────────────────── */}
      <Section title="Card" caption="surface, border, no shadow by default">
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <Card>
            <CardHeader>
              <CardTitle>Cluster prod-eu-1</CardTitle>
              <CardDescription>3 nodes · 14 VMs · 38 IPs</CardDescription>
            </CardHeader>
            <CardContent className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-muted-foreground">Status</span>
                <StatusPill variant="running">healthy</StatusPill>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">CPU</span>
                <MonoNum>42 %</MonoNum>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">RAM</span>
                <MonoNum>128.4 / 256 GB</MonoNum>
              </div>
            </CardContent>
            <CardFooter>
              <Button variant="outline" size="sm">
                View
              </Button>
              <Button size="sm">Manage</Button>
            </CardFooter>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Cluster beta-eval</CardTitle>
              <CardDescription>1 node · 3 VMs · pending auth</CardDescription>
            </CardHeader>
            <CardContent className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-muted-foreground">Status</span>
                <StatusPill variant="pending">pending</StatusPill>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Pending</span>
                <MonoNum>3 VMs</MonoNum>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Awaiting</span>
                <MonoNum muted>Node #2</MonoNum>
              </div>
            </CardContent>
            <CardFooter>
              <Button variant="outline" size="sm">
                Cancel
              </Button>
              <Button size="sm" variant="destructive">
                Force start
              </Button>
            </CardFooter>
          </Card>
        </div>
      </Section>

      {/* ── Form ───────────────────────────────── */}
      <Section title="Form" caption="Label + Input + Button — semantic spacing">
        <Card className="max-w-md">
          <CardHeader>
            <CardTitle>Create tenant</CardTitle>
            <CardDescription>One project is created automatically.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="tenant-name">Name</Label>
              <Input id="tenant-name" placeholder="acme-prod" />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="tenant-slug">Slug</Label>
              <Input id="tenant-slug" placeholder="acme-prod" />
              <div className="text-muted-foreground text-xs">
                Used in URLs. Lowercase, dashes only.
              </div>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="tenant-region">Region</Label>
              <Input id="tenant-region" defaultValue="eu-central-1" />
            </div>
          </CardContent>
          <CardFooter>
            <Button variant="outline">Cancel</Button>
            <Button>Create</Button>
          </CardFooter>
        </Card>
      </Section>

      {/* ── Table — density demo with StatusPill + MonoNum ─ */}
      <Section
        title="Table"
        caption="Dense rows · status + mono columns · row-h 30px"
      >
        <Card>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="eyebrow">ID</TableHead>
                <TableHead className="eyebrow">Name</TableHead>
                <TableHead className="eyebrow text-right">Projects</TableHead>
                <TableHead className="eyebrow text-right">VMs</TableHead>
                <TableHead className="eyebrow">Status</TableHead>
                <TableHead className="w-10"></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {SAMPLE_TENANTS.map((tenant) => (
                <TableRow key={tenant.id}>
                  <TableCell>
                    <MonoNum muted>{tenant.id}</MonoNum>
                  </TableCell>
                  <TableCell className="font-medium">{tenant.name}</TableCell>
                  <TableCell className="text-right">
                    <MonoNum>{tenant.projects}</MonoNum>
                  </TableCell>
                  <TableCell className="text-right">
                    <MonoNum>{tenant.vms}</MonoNum>
                  </TableCell>
                  <TableCell>
                    <StatusPill variant={tenant.status}>
                      {STATUS_LABEL[tenant.status]}
                    </StatusPill>
                  </TableCell>
                  <TableCell>
                    <Button size="icon-xs" variant="ghost" aria-label="Open">
                      →
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Card>
      </Section>

      {/* ── Tabs ───────────────────────────────── */}
      <Section title="Tabs" caption="underlined · Plexor DS">
        <Tabs defaultValue="overview">
          <TabsList>
            <TabsTrigger value="overview">Overview</TabsTrigger>
            <TabsTrigger value="nodes">Nodes (3)</TabsTrigger>
            <TabsTrigger value="vms">VMs (14)</TabsTrigger>
            <TabsTrigger value="audit">Audit</TabsTrigger>
          </TabsList>
          <TabsContent value="overview" className="text-sm">
            Cluster healthy. No active alerts.
          </TabsContent>
          <TabsContent value="nodes" className="text-sm">
            3 nodes online.
          </TabsContent>
          <TabsContent value="vms" className="text-sm">
            14 VMs across 3 nodes.
          </TabsContent>
          <TabsContent value="audit" className="text-sm">
            3 audit events in the last hour.
          </TabsContent>
        </Tabs>
      </Section>

      {/* ── Dialog + Tooltip + Alert ───────────── */}
      <Section title="Dialog, Tooltip, Alert" caption="Floating UI uses --sh-1 / --sh-2 shadows">
        <Demo label="Dialog">
          <Dialog>
            <DialogTrigger
              render={
                <Button variant="destructive">Delete cluster</Button>
              }
            />
            <DialogContent>
              <DialogHeader>
                <DialogTitle>Delete cluster prod-eu-1?</DialogTitle>
                <DialogDescription>
                  All 14 VMs and 38 floating IPs will be removed. This action
                  cannot be undone.
                </DialogDescription>
              </DialogHeader>
              <DialogFooter>
                <Button variant="outline">Cancel</Button>
                <Button variant="destructive">Confirm delete</Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </Demo>
        <Demo label="Tooltip">
          <Tooltip>
            <TooltipTrigger
              render={<Button variant="outline">Hover me</Button>}
            />
            <TooltipContent>Plexor DS tooltip</TooltipContent>
          </Tooltip>
        </Demo>
        <Demo label="Alert">
          <Alert>
            <AlertTitle>Quota exceeded</AlertTitle>
            <AlertDescription>
              Tenant <MonoNum>acme-prod</MonoNum> reached its VM limit (38/38).
            </AlertDescription>
            <AlertAction>
              <Button size="sm" variant="outline">
                Increase limit
              </Button>
            </AlertAction>
          </Alert>
        </Demo>
      </Section>

      {/* ── Avatar ──────────────────────────────── */}
      <Section title="Avatar" caption="Initials + sizes">
        <div className="flex items-center gap-3">
          {['SM', 'CL', 'VW', 'DA'].map((initials, i) => (
            <Avatar key={initials} size={['sm', 'md', 'lg', 'xl'][i] as 'sm' | 'md' | 'lg' | 'xl'}>
              <AvatarFallback>{initials}</AvatarFallback>
            </Avatar>
          ))}
        </div>
      </Section>

      <footer className="border-t border-border pt-6">
        <p className="text-muted-foreground text-xs">
          <span className="font-mono">.agents/docs/ui/architecture.md</span> ·{' '}
          <span className="font-mono">.agents/docs/design/styles.css</span> ·{' '}
          <Link to="/" className="underline">
            refresh
          </Link>
        </p>
      </footer>
    </main>
  );
}

// ── helpers ────────────────────────────────────────────────

function Section({
  title,
  caption,
  children,
}: {
  title: string;
  caption?: string;
  children: React.ReactNode;
}) {
  return (
    <section className="space-y-4">
      <header className="space-y-0.5">
        <h2 className="text-sm font-semibold tracking-tight">{title}</h2>
        {caption ? (
          <p className="text-muted-foreground text-xs">{caption}</p>
        ) : null}
      </header>
      <div className="space-y-3">{children}</div>
    </section>
  );
}

function Demo({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="border-border bg-card space-y-2 rounded-md border p-4">
      <div className="eyebrow">{label}</div>
      {children}
    </div>
  );
}

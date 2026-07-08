import { createFileRoute } from '@tanstack/react-router';
import { useState } from 'react';
import { Gear, Trash, ArrowClockwise, Pause, Pencil, Copy, Clipboard, Check, Plus } from '@phosphor-icons/react';

import { ModeToggle } from '@/shared/ui/primitives/theme-toggle';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { IP } from '@/shared/ui/primitives/ip';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { Stat } from '@/shared/ui/primitives/stat';
import { Console, ConsoleLine } from '@/shared/ui/primitives/console';
import { Toolbar, ToolbarHeader, ToolbarTitle, ToolbarActions, ToolbarContent, ToolbarSection, ToolbarSectionLabel, ToolbarItems, ToolbarItem, ToolbarLabel, ToolbarDescription, ToolbarSeparator } from '@/shared/ui/primitives/toolbar';
import { BulkActionToolbar, type BulkActionAction } from '@/shared/ui/primitives/bulk-action-toolbar';
import { Button } from '@/shared/ui/primitives/button';
import { ButtonGroup } from '@/shared/ui/primitives/button-group';
import { Input } from '@/shared/ui/primitives/input';
import { Textarea } from '@/shared/ui/primitives/textarea';
import { Label } from '@/shared/ui/primitives/label';
import { Checkbox } from '@/shared/ui/primitives/checkbox';
import { RadioGroup, RadioGroupItem } from '@/shared/ui/primitives/radio-group';
import { Switch } from '@/shared/ui/primitives/switch';
import { Slider } from '@/shared/ui/primitives/slider';
import { InputOTP, InputOTPGroup, InputOTPSlot } from '@/shared/ui/primitives/input-otp';
import { Select, SelectContent, SelectGroup, SelectItem, SelectLabel, SelectSeparator, SelectTrigger, SelectValue } from '@/shared/ui/primitives/select';

import { Field, FieldDescription, FieldGroup, FieldLabel, FieldSet, FieldLegend } from '@/shared/ui/primitives/field';
import { Combobox, ComboboxInput, ComboboxContent, ComboboxEmpty, ComboboxItem, ComboboxList } from '@/shared/ui/primitives/combobox';
import { InputGroup, InputGroupAddon, InputGroupInput } from '@/shared/ui/primitives/input-group';

import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { Badge } from '@/shared/ui/primitives/badge';
import { Avatar, AvatarFallback, AvatarImage } from '@/shared/ui/primitives/avatar';
import { Separator } from '@/shared/ui/primitives/separator';
import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { Spinner } from '@/shared/ui/primitives/spinner';
import { Progress } from '@/shared/ui/primitives/progress';
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from '@/shared/ui/primitives/accordion';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/shared/ui/primitives/collapsible';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/shared/ui/primitives/tabs';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/shared/ui/primitives/breadcrumb';
import { Pagination, PaginationContent, PaginationItem, PaginationLink, PaginationNext, PaginationPrevious } from '@/shared/ui/primitives/pagination';
import { ScrollArea, ScrollBar } from '@/shared/ui/primitives/scroll-area';
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from '@/shared/ui/primitives/resizable';
import { Item, ItemContent, ItemDescription, ItemTitle } from '@/shared/ui/primitives/item';
import { Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyTitle } from '@/shared/ui/primitives/empty';

import { Alert, AlertAction, AlertDescription, AlertTitle } from '@/shared/ui/primitives/alert';
import { Toaster } from "@/shared/ui/primitives/sonner";
import { toast } from "sonner";
import { Tooltip, TooltipContent, TooltipTrigger } from '@/shared/ui/primitives/tooltip';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/shared/ui/primitives/dialog';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/shared/ui/primitives/alert-dialog';
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle, SheetTrigger } from '@/shared/ui/primitives/sheet';
import { Drawer, DrawerContent, DrawerDescription, DrawerHeader, DrawerTitle, DrawerTrigger } from '@/shared/ui/primitives/drawer';
import { Popover, PopoverContent, PopoverTrigger } from '@/shared/ui/primitives/popover';
import { HoverCard, HoverCardContent, HoverCardTrigger } from '@/shared/ui/primitives/hover-card';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/shared/ui/primitives/dropdown-menu';
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuSeparator,
  ContextMenuShortcut,
  ContextMenuTrigger,
} from '@/shared/ui/primitives/context-menu';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList, CommandSeparator, CommandShortcut } from '@/shared/ui/primitives/command';
import { Toggle } from '@/shared/ui/primitives/toggle';
import { ToggleGroup, ToggleGroupItem } from '@/shared/ui/primitives/toggle-group';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/shared/ui/primitives/table';
import { Carousel, CarouselContent, CarouselItem, CarouselNext, CarouselPrevious } from '@/shared/ui/primitives/carousel';
import { Calendar } from '@/shared/ui/primitives/calendar';
import { ChartContainer } from '@/shared/ui/primitives/chart';

export const Route = createFileRoute('/components')({
  component: ComponentsPage,
});

interface NavGroup {
  id: string;
  label: string;
  items: { id: string; label: string }[];
}

const NAV: NavGroup[] = [
  {
    id: 'foundations',
    label: 'Foundations',
    items: [
      { id: 'theme-toggle', label: 'ThemeToggle' },
      { id: 'status-pill', label: 'StatusPill' },
      { id: 'mono-num', label: 'MonoNum' },
      { id: 'stat', label: 'Stat' },
      { id: 'console', label: 'Console' },
      { id: 'toolbar', label: 'Toolbar' },
      { id: 'bulk-action-toolbar', label: 'BulkActionToolbar' },
      { id: 'ip', label: 'IP' },
    ],
  },
  {
    id: 'shadcn-buttons',
    label: 'Buttons & actions',
    items: [
      { id: 'button', label: 'Button' },
      { id: 'button-group', label: 'Button group' },
      { id: 'toggle', label: 'Toggle' },
      { id: 'toggle-group', label: 'Toggle group' },
      { id: 'dropdown-menu', label: 'Dropdown menu' },
      { id: 'context-menu', label: 'Context menu' },
    ],
  },
  {
    id: 'shadcn-forms',
    label: 'Forms & inputs',
    items: [
      { id: 'field', label: 'Field / FieldGroup' },
      { id: 'input', label: 'Input' },
      { id: 'input-group', label: 'Input group' },
      { id: 'textarea', label: 'Textarea' },
      { id: 'select', label: 'Select' },
      { id: 'combobox', label: 'Combobox' },
      { id: 'input-otp', label: 'Input OTP' },
      { id: 'checkbox', label: 'Checkbox' },
      { id: 'radio-group', label: 'Radio group' },
      { id: 'switch', label: 'Switch' },
      { id: 'slider', label: 'Slider' },
    ],
  },
  {
    id: 'shadcn-layout',
    label: 'Layout & display',
    items: [
      { id: 'card', label: 'Card' },
      { id: 'badge', label: 'Badge' },
      { id: 'avatar', label: 'Avatar' },
      { id: 'separator', label: 'Separator' },
      { id: 'tabs', label: 'Tabs' },
      { id: 'accordion', label: 'Accordion' },
      { id: 'collapsible', label: 'Collapsible' },
      { id: 'breadcrumb', label: 'Breadcrumb' },
      { id: 'pagination', label: 'Pagination' },
      { id: 'scroll-area', label: 'Scroll area' },
      { id: 'resizable', label: 'Resizable' },
      { id: 'aspect-ratio', label: 'Aspect ratio' },
      { id: 'item', label: 'Item' },
      { id: 'empty', label: 'Empty' },
      { id: 'skeleton', label: 'Skeleton' },
      { id: 'spinner', label: 'Spinner' },
      { id: 'table', label: 'Table' },
    ],
  },
  {
    id: 'shadcn-overlays',
    label: 'Overlays & feedback',
    items: [
      { id: 'tooltip', label: 'Tooltip' },
      { id: 'hover-card', label: 'Hover card' },
      { id: 'popover', label: 'Popover' },
      { id: 'dialog', label: 'Dialog' },
      { id: 'alert-dialog', label: 'Alert dialog' },
      { id: 'sheet', label: 'Sheet' },
      { id: 'drawer', label: 'Drawer' },
      { id: 'command', label: 'Command palette' },
      { id: 'alert', label: 'Alert' },
      { id: 'sonner', label: 'Sonner toast' },
      { id: 'progress', label: 'Progress' },
    ],
  },
  {
    id: 'shadcn-data',
    label: 'Data display',
    items: [
      { id: 'calendar', label: 'Calendar' },
      { id: 'chart', label: 'Chart' },
      { id: 'carousel', label: 'Carousel' },
    ],
  },
];

function ComponentsPage() {
  return (
    <div className="min-h-screen">
      <main className="mx-auto max-w-5xl space-y-16 px-8 py-10">
        {NAV.map((group) => (
          <NavSection key={group.id} group={group} />
        ))}
      </main>
      <Toaster position="bottom-right" />
    </div>
  );
}

function BulkActionToolbarDemo() {
  const [selected, setSelected] = useState<string[]>(['vm-001', 'vm-014']);
  const items = [
    { id: 'vm-001', name: 'vm-prod-01', status: 'running' },
    { id: 'vm-002', name: 'vm-prod-02', status: 'running' },
    { id: 'vm-014', name: 'vm-stage-01', status: 'stopped' },
    { id: 'vm-022', name: 'vm-dev-01', status: 'failed' },
  ];

  const toggle = (id: string) => {
    setSelected((prev) =>
      prev.includes(id) ? prev.filter((s) => s !== id) : [...prev, id]
    );
  };

  const actions: BulkActionAction[] = [
    { label: 'Suspend', onClick: () => {}, variant: 'outline', icon: <Pause className="size-4" /> },
    { label: 'Restart', onClick: () => {}, variant: 'outline', icon: <ArrowClockwise className="size-4" /> },
  ];

  return (
    <div className="flex flex-col gap-3">
      <div className="text-muted-foreground text-sm">
        Floating bottom panel — slides up when rows selected, doesn't
        compete with sticky header. Click rows to select.
      </div>
      <div className="relative min-h-80 overflow-hidden rounded-md border border-border bg-card">
        <div className="divide-y divide-border">
          {items.map((item) => (
            <label key={item.id} className="flex items-center gap-3 px-4 py-2 hover:bg-muted/50">
              <Checkbox
                checked={selected.includes(item.id)}
                onCheckedChange={() => toggle(item.id)}
              />
              <span className="text-sm">{item.name}</span>
              <StatusPill variant={item.status as 'running' | 'stopped' | 'failed'}>
                {item.status}
              </StatusPill>
              <MonoNum muted className="ml-auto">
                {item.id}
              </MonoNum>
            </label>
          ))}
        </div>
        <BulkActionToolbar
          count={selected.length}
          onClear={() => setSelected([])}
          actions={[
            ...actions,
            // Destructive: icon-only red button (clear visual signal)
            { label: '', onClick: () => {}, variant: 'destructive', icon: <Trash className="size-4" /> },
          ]}
          entityLabel="vms selected"
          bottomClass="bottom-3"
        />
      </div>
    </div>
  );
}

function NavSection({ group }: { group: NavGroup }) {
  return (
    <section className="space-y-6">
      <h2 className="text-eyebrow text-muted-foreground border-b border-border pb-1.5" id={group.id}>
        {group.label}
      </h2>
      <div className="space-y-12">
        {group.items.map((item) => (
          <ComponentSection key={item.id} id={item.id} label={item.label} />
        ))}
      </div>
    </section>
  );
}

function ComponentSection({ id, label }: { id: string; label: string }) {
  return (
    <article id={id} data-section={id} className="scroll-mt-20 space-y-3">
      <header className="flex items-baseline justify-between gap-3">
        <h3 className="text-base font-semibold tracking-tight">
          <a href={`#${id}`} className="hover:underline">
            {label}
          </a>
        </h3>
        <code className="font-mono text-[11px] bg-muted text-fg-2 px-1 py-px rounded">
          @/shared/ui/primitives/{id}
        </code>
      </header>
      <Component id={id} />
    </article>
  );
}

function Component({ id }: { id: string }) {
  switch (id) {
    // ── Foundations (Plexor custom primitives) ──
    case 'theme-toggle':
      return (
        <Demo label="Light/dark switch — persisted in localStorage">
          <ModeToggle />
        </Demo>
      );
    case 'status-pill':
      return (
        <Demo label="13 status variants + 2 sizes — token-driven, 3-layer (bg-*-soft + text-*-ink + dot)">
          <div className="space-y-6">
            <div className="space-y-2">
              <div className="text-eyebrow text-muted-foreground">Core (4 colors)</div>
              <div className="flex flex-wrap gap-2">
                <StatusPill variant="ok">healthy</StatusPill>
                <StatusPill variant="err">error</StatusPill>
                <StatusPill variant="warn">warning</StatusPill>
                <StatusPill variant="idle">idle</StatusPill>
              </div>
            </div>
            <div className="space-y-2">
              <div className="text-eyebrow text-muted-foreground">Sync aliases (resource states)</div>
              <div className="flex flex-wrap gap-2">
                <StatusPill variant="running">running</StatusPill>
                <StatusPill variant="pending">pending</StatusPill>
                <StatusPill variant="failed">failed</StatusPill>
                <StatusPill variant="stopped">stopped</StatusPill>
              </div>
            </div>
            <div className="space-y-2">
              <div className="text-eyebrow text-muted-foreground">Lifecycle states</div>
              <div className="flex flex-wrap gap-2">
                <StatusPill variant="new">new</StatusPill>
                <StatusPill variant="beta">beta</StatusPill>
                <StatusPill variant="draft">draft</StatusPill>
                <StatusPill variant="archived">archived</StatusPill>
                <StatusPill variant="deprecated">deprecated</StatusPill>
                <StatusPill variant="info">info</StatusPill>
              </div>
            </div>
            <div className="space-y-2">
              <div className="text-eyebrow text-muted-foreground">Size variants (sm = h-5 / md = h-6)</div>
              <div className="flex flex-wrap items-center gap-2">
                <StatusPill variant="ok" size="sm">sm healthy</StatusPill>
                <StatusPill variant="ok" size="md">md healthy</StatusPill>
                <StatusPill variant="running" size="sm" hideDot>sm no-dot</StatusPill>
                <StatusPill variant="err" size="md" hideDot>md no-dot</StatusPill>
              </div>
            </div>
            <div className="space-y-2">
              <div className="text-eyebrow text-muted-foreground">In context — table column</div>
              <div className="rounded-md border border-border bg-card">
                <div className="grid grid-cols-3 border-b border-border px-3 py-2 text-xs text-muted-foreground">
                  <span>Resource</span>
                  <span>Status</span>
                  <span>Uptime</span>
                </div>
                <div className="grid grid-cols-3 items-center px-3 py-2 text-sm">
                  <span className="font-medium">vm-prod-01</span>
                  <span><StatusPill variant="running" size="sm">running</StatusPill></span>
                  <span className="text-muted-foreground font-mono tabular-nums">12d 4h</span>
                </div>
                <div className="grid grid-cols-3 items-center border-t border-border px-3 py-2 text-sm">
                  <span className="font-medium">vm-prod-02</span>
                  <span><StatusPill variant="failed" size="sm">failed</StatusPill></span>
                  <span className="text-muted-foreground font-mono tabular-nums">—</span>
                </div>
                <div className="grid grid-cols-3 items-center border-t border-border px-3 py-2 text-sm">
                  <span className="font-medium">vm-stage-01</span>
                  <span><StatusPill variant="pending" size="sm">pending</StatusPill></span>
                  <span className="text-muted-foreground font-mono tabular-nums">—</span>
                </div>
                <div className="grid grid-cols-3 items-center border-t border-border px-3 py-2 text-sm">
                  <span className="font-medium">old-vm-22</span>
                  <span><StatusPill variant="deprecated" size="sm">deprecated</StatusPill></span>
                  <span className="text-muted-foreground font-mono tabular-nums">—</span>
                </div>
              </div>
            </div>
          </div>
        </Demo>
      );
    case 'mono-num':
      return (
        <Demo label="Mono font + tabular numerals for dense numeric columns">
          <div className="space-y-1 text-sm">
            <div className="flex justify-between gap-4">
              <span className="text-muted-foreground">IP</span>
              <MonoNum>10.128.42.17</MonoNum>
            </div>
            <div className="flex justify-between gap-4">
              <span className="text-muted-foreground">ID</span>
              <MonoNum muted>tnt_8c2a4ef9</MonoNum>
            </div>
            <div className="flex justify-between gap-4">
              <span className="text-muted-foreground">Size</span>
              <MonoNum>128.45 GB</MonoNum>
            </div>
          </div>
        </Demo>
      );
    case 'stat':
      return (
        <Demo label="Single-metric card (label + value + trend)">
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <Stat label="Active VMs" value="38" trend="up" context="+3 this week" />
            <Stat label="CPU avg" value="42 %" trend="down" context="-1.2 % vs last" />
            <Stat label="Spend (MTD)" value="$ 12,847" trend="up" context="+8.4 %" />
          </div>
        </Demo>
      );
    case 'console':
      return (
        <Demo label="Terminal / serial-log panel — abstract for any mono log output">
          <Console prompt="$">
            <ConsoleLine>Reached target Multi-User System.</ConsoleLine>
            {'\n'}
            <ConsoleLine>Started Plexor Node Agent v0.1.0.</ConsoleLine>
            {'\n'}
            <ConsoleLine variant="muted">Network interface eth0: DHCP lease in 38s.</ConsoleLine>
            {'\n'}
            <ConsoleLine>Mounted /var/lib/plexor.</ConsoleLine>
          </Console>
        </Demo>
      );
    case 'toolbar':
      return (
        <Demo label="Settings panel primitive: header + sections + items">
          <Toolbar className="max-w-md">
            <ToolbarHeader>
              <ToolbarTitle>Display</ToolbarTitle>
              <ToolbarActions>
                <Button size="sm" variant="outline">Reset</Button>
              </ToolbarActions>
            </ToolbarHeader>
            <ToolbarContent>
              <ToolbarSection>
                <ToolbarSectionLabel>Theme</ToolbarSectionLabel>
                <ToolbarItems>
                  <ToolbarItem>
                    <ToolbarLabel>Mode</ToolbarLabel>
                    <Select items={[
                      { label: 'Light', value: 'light' },
                      { label: 'Dark', value: 'dark' },
                      { label: 'System', value: 'system' },
                    ]}>
                      <SelectTrigger size="sm">
                        <SelectValue placeholder="Theme" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectGroup>
                          {[
                            { label: 'Light', value: 'light' },
                            { label: 'Dark', value: 'dark' },
                            { label: 'System', value: 'system' },
                          ].map((item) => (
                            <SelectItem key={item.value} value={item.value}>
                              {item.label}
                            </SelectItem>
                          ))}
                        </SelectGroup>
                      </SelectContent>
                    </Select>
                  </ToolbarItem>
                  <ToolbarItem>
                    <div className="flex flex-col gap-0.5">
                      <ToolbarLabel>Compact density</ToolbarLabel>
                      <ToolbarDescription>Reduce row height and padding</ToolbarDescription>
                    </div>
                    <Switch />
                  </ToolbarItem>
                  <ToolbarItem>
                    <ToolbarLabel>Show line numbers</ToolbarLabel>
                    <Switch />
                  </ToolbarItem>
                </ToolbarItems>
              </ToolbarSection>
              <ToolbarSeparator />
              <ToolbarSection>
                <ToolbarSectionLabel>Notifications</ToolbarSectionLabel>
                <ToolbarItems>
                  <ToolbarItem>
                    <ToolbarLabel>Email alerts</ToolbarLabel>
                    <Switch defaultChecked />
                  </ToolbarItem>
                  <ToolbarItem>
                    <ToolbarLabel>Sound</ToolbarLabel>
                    <Switch />
                  </ToolbarItem>
                </ToolbarItems>
              </ToolbarSection>
              <ToolbarSeparator />
              <ToolbarSection>
                <ToolbarSectionLabel>Account</ToolbarSectionLabel>
                <ToolbarItems>
                  <ToolbarItem>
                    <ToolbarLabel>Storage used</ToolbarLabel>
                    <MonoNum muted>42.3 GB</MonoNum>
                  </ToolbarItem>
                  <ToolbarItem>
                    <ToolbarLabel>Plan</ToolbarLabel>
                    <span className="text-muted-foreground text-sm">Pro</span>
                  </ToolbarItem>
                </ToolbarItems>
              </ToolbarSection>
            </ToolbarContent>
          </Toolbar>
        </Demo>
      );

    case 'bulk-action-toolbar':
      return <BulkActionToolbarDemo />;

    case 'ip':
      return (
        <Demo label="IP address primitive: IPv4 / IPv6 / muted / link / grouped">
          <div className="flex flex-col gap-3">
            <div className="flex flex-col gap-1 text-sm">
              <div className="flex items-center gap-3">
                <span className="text-muted-foreground w-24">IPv4</span>
                <IP value="192.168.1.10" />
              </div>
              <div className="flex items-center gap-3">
                <span className="text-muted-foreground w-24">muted</span>
                <IP value="192.168.1.10" muted />
              </div>
              <div className="flex items-center gap-3">
                <span className="text-muted-foreground w-24">link</span>
                <IP value="10.0.0.1" link />
              </div>
              <div className="flex items-center gap-3">
                <span className="text-muted-foreground w-24">no group</span>
                <IP value="192.168.1.10" group={false} />
              </div>
              <div className="flex items-center gap-3">
                <span className="text-muted-foreground w-24">IPv6</span>
                <IP value="2001:db8::1" muted />
              </div>
            </div>
            <div className="border-t border-border pt-3">
              <div className="text-muted-foreground mb-2 text-xs uppercase tracking-[0.06em]">
                Column alignment (grouped octets)
              </div>
              <div className="font-mono text-sm tabular-nums">
                <div className="flex gap-6">
                  <span>10.1.2.3</span>
                  <span>192.168.100.10</span>
                </div>
                <div className="flex gap-6">
                  <span>10.0.0.1</span>
                  <span>172.16.0.254</span>
                </div>
              </div>
            </div>
          </div>
        </Demo>
      );

    // ── shadcn-ui Buttons & actions ──
    case 'button':
      return (
        <Demo label="Variants × Sizes">
          <div className="flex flex-col gap-4">
            <div className="flex flex-wrap items-center gap-2">
              <Button>Default</Button>
              <Button variant="outline">Outline</Button>
              <Button variant="secondary">Secondary</Button>
              <Button variant="ghost">Ghost</Button>
              <Button variant="destructive">Destructive</Button>
              <Button variant="link">Link</Button>
              <Button disabled>Disabled</Button>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Button size="sm">sm</Button>
              <Button>default</Button>
              <Button size="lg">lg</Button>
              <Button size="icon" aria-label="settings">
                <Gear />
              </Button>
            </div>
          </div>
        </Demo>
      );
    case 'button-group':
      return (
        <Demo label="Grouped buttons (segmented)">
          <ButtonGroup>
            <Button variant="outline">List</Button>
            <Button variant="outline">Grid</Button>
            <Button variant="outline">Map</Button>
          </ButtonGroup>
        </Demo>
      );
    case 'toggle':
      return (
        <Demo label="On/off toggle">
          <div className="flex items-center gap-3">
            <Toggle aria-label="Toggle">Default</Toggle>
            <Toggle aria-label="Toggle outline" variant="outline">
              Outline
            </Toggle>
            <Toggle aria-label="Toggle sm" size="sm">
              Small
            </Toggle>
            <Toggle aria-label="Toggle lg" size="lg">
              Large
            </Toggle>
          </div>
        </Demo>
      );
    case 'toggle-group':
      return (
        <Demo label="Single + multiple selection">
          <div className="flex flex-col gap-3">
            <ToggleGroup defaultValue={["left"]}>
              <ToggleGroupItem value="left">Left</ToggleGroupItem>
              <ToggleGroupItem value="center">Center</ToggleGroupItem>
              <ToggleGroupItem value="right">Right</ToggleGroupItem>
            </ToggleGroup>
            <ToggleGroup defaultValue={["bold"]}>
              <ToggleGroupItem value="bold">Bold</ToggleGroupItem>
              <ToggleGroupItem value="italic">Italic</ToggleGroupItem>
              <ToggleGroupItem value="underline">Underline</ToggleGroupItem>
            </ToggleGroup>
          </div>
        </Demo>
      );
    case 'dropdown-menu':
      return (
        <Demo label="Action menu (overflow actions)">
          <DropdownMenu>
            <DropdownMenuTrigger render={<Button variant="outline">Actions ▾</Button>} />
            <DropdownMenuContent>
              <DropdownMenuItem>Edit</DropdownMenuItem>
              <DropdownMenuItem>Duplicate</DropdownMenuItem>
              <DropdownMenuItem>Move…</DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem variant="destructive">Delete</DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </Demo>
      );
    case 'context-menu':
      return (
        <Demo label="Right-click menu: icons + shortcut + separator + destructive">
          <ContextMenu>
            <ContextMenuTrigger
              render={
                <div className="rounded-md border border-dashed border-border p-4 text-center text-sm text-muted-foreground">
                  Right-click here
                </div>
              }
            />
            <ContextMenuContent>
              <ContextMenuItem>
                <Pencil />
                Edit
              </ContextMenuItem>
              <ContextMenuItem>
                <Copy />
                Copy ID
                <ContextMenuShortcut>⌘C</ContextMenuShortcut>
              </ContextMenuItem>
              <ContextMenuItem>
                <Clipboard />
                Inspect
                <ContextMenuShortcut>⌘I</ContextMenuShortcut>
              </ContextMenuItem>
              <ContextMenuSeparator />
              <ContextMenuItem variant="destructive">
                <Trash />
                Delete
                <ContextMenuShortcut>⌫</ContextMenuShortcut>
              </ContextMenuItem>
            </ContextMenuContent>
          </ContextMenu>
        </Demo>
      );

    // ── shadcn-ui Forms & inputs ──
    case 'field':
      return (
        <Demo label="FieldGroup + Field + FieldLabel + FieldDescription (canonical form pattern)">
          <FieldGroup className="max-w-md">
            <Field>
              <FieldLabel htmlFor="email">Email</FieldLabel>
              <Input id="email" type="email" placeholder="dev@plexor.cloud" />
              <FieldDescription>Used for sign-in and alerts.</FieldDescription>
            </Field>
            <Field>
              <FieldLabel htmlFor="password">Password</FieldLabel>
              <Input id="password" type="password" />
            </Field>
          </FieldGroup>
        </Demo>
      );
    case 'input':
      return (
        <Demo label="Text input">
          <Input placeholder="Search tenants…" className="max-w-md" />
        </Demo>
      );
    case 'input-group':
      return (
        <Demo label="With leading/trailing addons">
          <InputGroup className="max-w-md">
            <InputGroupAddon>https://</InputGroupAddon>
            <InputGroupInput placeholder="example.com" />
          </InputGroup>
        </Demo>
      );
    case 'textarea':
      return (
        <Demo label="Multi-line">
          <Textarea placeholder="Описание…" rows={3} className="max-w-md" />
        </Demo>
      );
    case 'select':
      return (
        <Demo label="Select: trigger content-sized + items tight + check on selected">
          <div className="flex flex-col gap-6">
            {/* Simple — fits content, no empty space */}
            <div className="space-y-2">
              <div className="text-eyebrow text-muted-foreground">simple</div>
              <Select defaultValue="dark">
                <SelectTrigger>
                  <SelectValue placeholder="Theme" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="light">Light</SelectItem>
                  <SelectItem value="dark">Dark</SelectItem>
                  <SelectItem value="system">System</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {/* With groups + separators + longer label */}
            <div className="space-y-2">
              <div className="text-eyebrow text-muted-foreground">grouped + separator</div>
              <Select defaultValue="production">
                <SelectTrigger>
                  <SelectValue placeholder="Region" />
                </SelectTrigger>
                <SelectContent>
                  <SelectGroup>
                    <SelectLabel>Europe</SelectLabel>
                    <SelectItem value="production">eu-central-1 (production)</SelectItem>
                    <SelectItem value="staging">eu-west-1 (staging)</SelectItem>
                  </SelectGroup>
                  <SelectSeparator />
                  <SelectGroup>
                    <SelectLabel>Americas</SelectLabel>
                    <SelectItem value="us-prod">us-east-1 (production)</SelectItem>
                  </SelectGroup>
                </SelectContent>
              </Select>
            </div>

            {/* Disabled state */}
            <div className="space-y-2">
              <div className="text-eyebrow text-muted-foreground">disabled</div>
              <Select disabled>
                <SelectTrigger>
                  <SelectValue placeholder="Unavailable" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="x">X</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </Demo>
      );
    case 'combobox':
      return (
        <Demo label="Searchable combobox with Phosphor magnifying-glass icon">
          <Combobox items={['Next.js', 'SvelteKit', 'Nuxt.js', 'Remix', 'Astro']}>
            <ComboboxInput placeholder="Select a framework" />
            <ComboboxContent>
              <ComboboxEmpty>No items found.</ComboboxEmpty>
              <ComboboxList>
                {(item) => (
                  <ComboboxItem key={item} value={item}>
                    {item}
                  </ComboboxItem>
                )}
              </ComboboxList>
            </ComboboxContent>
          </Combobox>
        </Demo>
      );
    case 'input-otp':
      return (
        <Demo label="6-digit code">
          <InputOTP maxLength={6}>
            <InputOTPGroup>
              <InputOTPSlot index={0} />
              <InputOTPSlot index={1} />
              <InputOTPSlot index={2} />
              <InputOTPSlot index={3} />
              <InputOTPSlot index={4} />
              <InputOTPSlot index={5} />
            </InputOTPGroup>
          </InputOTP>
        </Demo>
      );
    case 'checkbox':
      return (
        <Demo label="Checkbox + FieldSet (multiple checkboxes grouped)">
          <FieldSet className="max-w-md">
            <FieldLegend>Notifications</FieldLegend>
            <div className="space-y-2">
              <label className="flex items-center gap-2 text-sm">
                <Checkbox defaultChecked />
                <span>Email notifications</span>
              </label>
              <label className="flex items-center gap-2 text-sm">
                <Checkbox />
                <span>SMS notifications</span>
              </label>
            </div>
          </FieldSet>
        </Demo>
      );
    case 'radio-group':
      return (
        <Demo label="RadioGroup (exclusive selection)">
          <RadioGroup defaultValue="hourly" className="flex flex-col gap-2">
            <div className="flex items-center gap-2">
              <RadioGroupItem value="hourly" id="r1" />
              <Label htmlFor="r1">Hourly billing</Label>
            </div>
            <div className="flex items-center gap-2">
              <RadioGroupItem value="monthly" id="r2" />
              <Label htmlFor="r2">Monthly billing</Label>
            </div>
          </RadioGroup>
        </Demo>
      );
    case 'switch':
      return (
        <Demo label="On/off switch">
          <div className="flex items-center gap-3">
            <Switch id="sw1" defaultChecked />
            <Label htmlFor="sw1">Enable notifications</Label>
          </div>
        </Demo>
      );
    case 'slider':
      return (
        <Demo label="Single value slider">
          <Slider defaultValue={[50]} max={100} step={1} className="max-w-sm" />
        </Demo>
      );

    // ── shadcn-ui Layout & display ──
    case 'card':
      return (
        <Demo label="Card composition (header / content / footer)">
          <Card className="max-w-sm">
            <CardHeader>
              <CardTitle>Cluster prod-eu-1</CardTitle>
              <CardDescription>3 nodes · 14 VMs</CardDescription>
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
            </CardContent>
            <CardFooter>
              <Button variant="outline" size="sm">View</Button>
              <Button size="sm">Manage</Button>
            </CardFooter>
          </Card>
        </Demo>
      );
    case 'badge':
      return (
        <Demo label="Generic badges — for tags/labels/counts (not status)">
          <div className="space-y-6">
            <div className="space-y-2">
              <div className="text-eyebrow text-muted-foreground">Variants</div>
              <div className="flex flex-wrap items-center gap-2">
                <Badge>Default</Badge>
                <Badge variant="secondary">Secondary</Badge>
                <Badge variant="outline">Outline</Badge>
                <Badge variant="destructive">Destructive</Badge>
              </div>
            </div>
            <div className="space-y-2">
              <div className="text-eyebrow text-muted-foreground">As link</div>
              <div className="flex flex-wrap items-center gap-2">
                <Badge>v2.4.0</Badge>
                <Badge variant="secondary">stable</Badge>
                <Badge variant="outline">docs</Badge>
              </div>
            </div>
            <div className="space-y-2">
              <div className="text-eyebrow text-muted-foreground">With icon</div>
              <div className="flex flex-wrap items-center gap-2">
                <Badge>
                  <Check className="size-3" />
                  verified
                </Badge>
                <Badge variant="secondary">
                  <Plus className="size-3" />
                  new
                </Badge>
                <Badge variant="outline">+12</Badge>
                <Badge variant="destructive">3 errors</Badge>
              </div>
            </div>
            <div className="space-y-2">
              <div className="text-eyebrow text-muted-foreground">In context — feature flags row</div>
              <div className="flex flex-wrap items-center gap-1.5 rounded-md border border-border bg-card p-3">
                <span className="text-sm">Multi-region:</span>
                <Badge variant="secondary">enabled</Badge>
                <span className="text-sm ml-3">Audit log:</span>
                <Badge>on</Badge>
                <span className="text-sm ml-3">Beta features:</span>
                <Badge variant="outline">off</Badge>
                <span className="text-sm ml-3">Issues:</span>
                <Badge variant="destructive">3</Badge>
              </div>
            </div>
            <div className="rounded-md border border-border/50 bg-muted/30 p-3 text-xs text-muted-foreground">
              <strong className="text-foreground">StatusPill vs Badge:</strong> use StatusPill for runtime
              resource state (running/failed/pending). Use Badge for static labels (versions,
              tags, counts, feature flags). StatusPill has a colored dot; Badge is a plain pill.
            </div>
          </div>
        </Demo>
      );
    case 'avatar':
      return (
        <Demo label="Initials + image (4 sizes)">
          <div className="flex items-center gap-3">
            <Avatar>
              <AvatarImage src="https://github.com/shadcn.png" alt="@shadcn" />
              <AvatarFallback>CN</AvatarFallback>
            </Avatar>
            <Avatar>
              <AvatarFallback>SM</AvatarFallback>
            </Avatar>
            <Avatar size="sm">
              <AvatarFallback>SM</AvatarFallback>
            </Avatar>
            <Avatar size="lg">
              <AvatarFallback>LG</AvatarFallback>
            </Avatar>
          </div>
        </Demo>
      );
    case 'separator':
      return (
        <Demo label="Horizontal + vertical separator">
          <div className="space-y-2">
            <div className="text-sm">Section A</div>
            <Separator />
            <div className="text-sm">Section B</div>
          </div>
        </Demo>
      );
    case 'tabs':
      return (
        <Demo label="Tabbed content sections">
          <Tabs defaultValue="overview">
            <TabsList>
              <TabsTrigger value="overview">Overview</TabsTrigger>
              <TabsTrigger value="nodes">Nodes (3)</TabsTrigger>
              <TabsTrigger value="vms">VMs (14)</TabsTrigger>
            </TabsList>
            <TabsContent value="overview" className="text-sm">
              Cluster healthy. No active alerts.
            </TabsContent>
            <TabsContent value="nodes" className="text-sm">
              <MonoNum>3</MonoNum> nodes online.
            </TabsContent>
            <TabsContent value="vms" className="text-sm">
              <MonoNum>14</MonoNum> VMs across 3 nodes.
            </TabsContent>
          </Tabs>
        </Demo>
      );
    case 'accordion':
      return (
        <Demo label="Disclosure sections">
          <Accordion className="max-w-md">
            <AccordionItem value="1">
              <AccordionTrigger>What is Plexor?</AccordionTrigger>
              <AccordionContent>Self-hosted cloud platform.</AccordionContent>
            </AccordionItem>
            <AccordionItem value="2">
              <AccordionTrigger>How is it installed?</AccordionTrigger>
              <AccordionContent>plx init — single binary, 3 install modes.</AccordionContent>
            </AccordionItem>
          </Accordion>
        </Demo>
      );
    case 'collapsible':
      return (
        <Demo label="Disclosure (simpler than accordion)">
          <Collapsible className="max-w-md">
            <CollapsibleTrigger render={<Button variant="outline">Toggle details</Button>} />
            <CollapsibleContent className="mt-2 text-sm">
              Hidden content revealed on click.
            </CollapsibleContent>
          </Collapsible>
        </Demo>
      );
    case 'breadcrumb':
      return (
        <Demo label="Page hierarchy">
          <Breadcrumb>
            <BreadcrumbList>
              <BreadcrumbItem>
                <BreadcrumbLink href="/">Plexor</BreadcrumbLink>
              </BreadcrumbItem>
              <BreadcrumbSeparator />
              <BreadcrumbItem>
                <BreadcrumbLink href="/tenants">Tenants</BreadcrumbLink>
              </BreadcrumbItem>
              <BreadcrumbSeparator />
              <BreadcrumbItem>
                <BreadcrumbPage>acme-prod</BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
        </Demo>
      );
    case 'pagination':
      return (
        <Demo label="Page navigation">
          <Pagination>
            <PaginationContent>
              <PaginationItem>
                <PaginationPrevious href="#" />
              </PaginationItem>
              <PaginationItem>
                <PaginationLink href="#" isActive>1</PaginationLink>
              </PaginationItem>
              <PaginationItem>
                <PaginationLink href="#">2</PaginationLink>
              </PaginationItem>
              <PaginationItem>
                <PaginationLink href="#">3</PaginationLink>
              </PaginationItem>
              <PaginationItem>
                <PaginationNext href="#" />
              </PaginationItem>
            </PaginationContent>
          </Pagination>
        </Demo>
      );
    case 'scroll-area':
      return (
        <Demo label="Vertical scroll area">
          <ScrollArea className="h-32 w-72 rounded-md border border-border">
            <div className="p-3 text-sm">
              {Array.from({ length: 30 }).map((_, i) => (
                <div key={i} className="py-1">Item {i + 1}</div>
              ))}
            </div>
            <ScrollBar orientation="vertical" />
          </ScrollArea>
        </Demo>
      );
    case 'resizable':
      return (
        <Demo label="Horizontal split">
          <ResizablePanelGroup
            orientation="horizontal"
            className="h-32 w-72 rounded-md border"
          >
            <ResizablePanel defaultSize={50}>
              <div className="p-3 text-sm">Left</div>
            </ResizablePanel>
            <ResizableHandle />
            <ResizablePanel defaultSize={50}>
              <div className="p-3 text-sm">Right</div>
            </ResizablePanel>
          </ResizablePanelGroup>
        </Demo>
      );
    case 'aspect-ratio':
      return (
        <Demo label="16:9 ratio box">
          <div className="aspect-video w-72 rounded-md bg-muted" />
        </Demo>
      );
    case 'item':
      return (
        <Demo label="List item with title/description">
          <Item className="max-w-sm">
            <ItemContent>
              <ItemTitle>System update</ItemTitle>
              <ItemDescription>Plexor v0.2 is available</ItemDescription>
            </ItemContent>
          </Item>
        </Demo>
      );
    case 'empty':
      return (
        <Demo label="Empty-state placeholder">
          <Empty className="border border-dashed border-border rounded-md">
            <EmptyHeader>
              <EmptyTitle>No tenants</EmptyTitle>
              <EmptyDescription>Create your first tenant to get started.</EmptyDescription>
            </EmptyHeader>
            <EmptyContent>
              <Button>Create tenant</Button>
            </EmptyContent>
          </Empty>
        </Demo>
      );
    case 'skeleton':
      return (
        <Demo label="Loading placeholder">
          <div className="space-y-2 max-w-sm">
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-4 w-1/2" />
            <Skeleton className="h-4 w-5/6" />
          </div>
        </Demo>
      );
    case 'spinner':
      return (
        <Demo label="Loading indicator">
          <Spinner className="size-6" />
        </Demo>
      );
    case 'table':
      return (
        <Demo label="Shadcn Table — primitives, add density via Tailwind">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>ID</TableHead>
                <TableHead>Name</TableHead>
                <TableHead className="text-right">VMs</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableRow>
                <TableCell>
                  <MonoNum muted>tnt_8c2a</MonoNum>
                </TableCell>
                <TableCell className="font-medium">acme-prod</TableCell>
                <TableCell className="text-right">
                  <MonoNum>38</MonoNum>
                </TableCell>
                <TableCell>
                  <StatusPill variant="running">running</StatusPill>
                </TableCell>
              </TableRow>
              <TableRow>
                <TableCell>
                  <MonoNum muted>tnt_a912</MonoNum>
                </TableCell>
                <TableCell className="font-medium">legacy-poc</TableCell>
                <TableCell className="text-right">
                  <MonoNum>0</MonoNum>
                </TableCell>
                <TableCell>
                  <StatusPill variant="idle">stopped</StatusPill>
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </Demo>
      );

    // ── shadcn-ui Overlays & feedback ──
    case 'tooltip':
      return (
        <Demo label="Hover-only label (or CSS-only via [data-tooltip])">
          <Tooltip>
            <TooltipTrigger render={<Button variant="outline">Hover me</Button>} />
            <TooltipContent>Plexor DS tooltip</TooltipContent>
          </Tooltip>
        </Demo>
      );
    case 'hover-card':
      return (
        <Demo label="Hover-triggered preview">
          <HoverCard>
            <HoverCardTrigger render={<a href="#" className="underline">@bradw</a>} />
            <HoverCardContent>
              <div className="text-sm">
                <div className="font-medium">bradw</div>
                <div className="text-muted-foreground text-xs">Senior C# developer</div>
              </div>
            </HoverCardContent>
          </HoverCard>
        </Demo>
      );
    case 'popover':
      return (
        <Demo label="Floating menu with arrow">
          <Popover>
            <PopoverTrigger render={<Button variant="outline">Open popover</Button>} />
            <PopoverContent className="w-64">
              <div className="text-sm">
                <div className="mb-1 font-medium">Quick info</div>
                <div className="text-muted-foreground">
                  Popovers are floating UI panels triggered by click.
                </div>
              </div>
            </PopoverContent>
          </Popover>
        </Demo>
      );
    case 'dialog':
      return (
        <Demo label="Modal — confirm action">
          <Dialog>
            <DialogTrigger render={<Button variant="destructive">Delete cluster</Button>} />
            <DialogContent>
              <DialogHeader>
                <DialogTitle>Delete cluster prod-eu-1?</DialogTitle>
                <DialogDescription>
                  All 14 VMs and 38 floating IPs will be removed. This action cannot be undone.
                </DialogDescription>
              </DialogHeader>
              <DialogFooter>
                <Button variant="outline">Cancel</Button>
                <Button variant="destructive">Confirm</Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </Demo>
      );
    case 'alert-dialog':
      return (
        <Demo label="Confirmation required for destructive action">
          <AlertDialog>
            <AlertDialogTrigger render={<Button variant="destructive">Delete project</Button>} />
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Are you absolutely sure?</AlertDialogTitle>
                <AlertDialogDescription>
                  This action cannot be undone. This will permanently delete the project.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction>Yes, delete</AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </Demo>
      );
    case 'sheet':
      return (
        <Demo label="Side-anchored modal">
          <Sheet>
            <SheetTrigger render={<Button variant="outline">Open sheet</Button>} />
            <SheetContent side="right">
              <SheetHeader>
                <SheetTitle>Edit tenant</SheetTitle>
                <SheetDescription>Make changes to the tenant settings.</SheetDescription>
              </SheetHeader>
              <div className="p-4 text-sm">Sheet content area.</div>
            </SheetContent>
          </Sheet>
        </Demo>
      );
    case 'drawer':
      return (
        <Demo label="Bottom-anchored modal (mobile-friendly)">
          <Drawer>
            <DrawerTrigger render={<Button variant="outline">Open drawer</Button>} />
            <DrawerContent>
              <DrawerHeader>
                <DrawerTitle>Drawer</DrawerTitle>
                <DrawerDescription>Slides up from the bottom.</DrawerDescription>
              </DrawerHeader>
              <div className="p-4 text-sm">Drawer content area.</div>
            </DrawerContent>
          </Drawer>
        </Demo>
      );
    case 'command':
      return (
        <Demo label="⌘K command palette">
          <Command className="max-w-sm rounded-md border bg-card text-foreground">
            <CommandInput placeholder="Type a command…" />
            <CommandList>
              <CommandEmpty>No results found.</CommandEmpty>
              <CommandGroup heading="Actions">
                <CommandItem value="create-tenant">
                  <span>Create tenant</span>
                  <CommandShortcut>⌘N</CommandShortcut>
                </CommandItem>
                <CommandItem value="open-settings">
                  <span>Open settings</span>
                  <CommandShortcut>⌘,</CommandShortcut>
                </CommandItem>
              </CommandGroup>
              <CommandSeparator />
              <CommandGroup heading="Navigate">
                <CommandItem value="go-vms">VMs</CommandItem>
                <CommandItem value="go-volumes">Volumes</CommandItem>
              </CommandGroup>
            </CommandList>
          </Command>
        </Demo>
      );
    case 'alert':
      return (
        <Demo label="Info + Destructive variants">
          <div className="space-y-2 max-w-md">
            <Alert>
              <AlertTitle>Heads up!</AlertTitle>
              <AlertDescription>You can add components to your app using the CLI.</AlertDescription>
              <AlertAction>
                <Button size="sm" variant="outline">Read more</Button>
              </AlertAction>
            </Alert>
            <Alert variant="destructive">
              <AlertTitle>Error</AlertTitle>
              <AlertDescription>Your session has expired. Please log in again.</AlertDescription>
            </Alert>
          </div>
        </Demo>
      );
    case 'sonner':
      return (
        <Demo label="Toast notifications">
          <div className="flex gap-2">
            <Button
              onClick={() => toast('Tenant created', { description: 'New tenant acme-prod' })}
            >
              Default
            </Button>
            <Button onClick={() => toast.success('VM started')}>Success</Button>
            <Button onClick={() => toast.error('Failed to connect')} variant="destructive">
              Error
            </Button>
          </div>
        </Demo>
      );
    case 'progress':
      return (
        <Demo label="Linear progress">
          <Progress value={66} className="max-w-sm" />
        </Demo>
      );

    // ── shadcn-ui Data display ──
    case 'calendar':
      return (
        <Demo label="Date picker (single)">
          <Calendar mode="single" className="rounded-md border" />
        </Demo>
      );
    case 'chart':
      return (
        <Demo label="Chart container (Recharts wrapper)">
          <ChartContainer config={{}} className="h-32 w-full">
            <div className="flex h-full items-center justify-center text-sm text-muted-foreground">
              ChartContainer — wrap your recharts here
            </div>
          </ChartContainer>
        </Demo>
      );
    case 'carousel':
      return (
        <Demo label="Image carousel (Embla wrapper)">
          <Carousel className="w-full max-w-xs">
            <CarouselContent>
              {[1, 2, 3].map((i) => (
                <CarouselItem key={i}>
                  <div className="bg-muted text-muted-foreground flex h-32 items-center justify-center rounded-md text-sm">
                    Slide {i}
                  </div>
                </CarouselItem>
              ))}
            </CarouselContent>
            <CarouselPrevious />
            <CarouselNext />
          </Carousel>
        </Demo>
      );

    default:
      return null;
  }
}

function Demo({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="rounded-md border border-border bg-card p-4">
      <div className="text-[11px] uppercase tracking-[0.06em] text-muted-foreground font-medium mb-2">
        {label}
      </div>
      {children}
    </div>
  );
}

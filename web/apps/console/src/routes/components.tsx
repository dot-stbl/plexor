import { createFileRoute, Link } from '@tanstack/react-router';
import { useEffect, useState } from 'react';
import { ThemeToggle } from '@/shared/ui/primitives/theme-toggle';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { Button } from '@/shared/ui/primitives/button';
import { ButtonGroup } from '@/shared/ui/primitives/button-group';
import { Switch } from '@/shared/ui/primitives/switch';
import { ToggleGroup, ToggleGroupItem } from '@/shared/ui/primitives/toggle-group';
import {
  DropdownMenu, DropdownMenuTrigger, DropdownMenuContent,
  DropdownMenuItem, DropdownMenuSeparator,
} from '@/shared/ui/primitives/dropdown-menu';
import {
  ContextMenu, ContextMenuTrigger, ContextMenuContent, ContextMenuItem,
} from '@/shared/ui/primitives/context-menu';
import { Input } from '@/shared/ui/primitives/input';
import { Textarea } from '@/shared/ui/primitives/textarea';
import {
  Select as SelectUi, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/shared/ui/primitives/select';
import {
  Combobox, ComboboxInput, ComboboxContent, ComboboxItem,
} from '@/shared/ui/primitives/combobox';
import { Checkbox } from '@/shared/ui/primitives/checkbox';
import { RadioGroup, RadioGroupItem } from '@/shared/ui/primitives/radio-group';
import { Slider } from '@/shared/ui/primitives/slider';
import { InputOTP, InputOTPGroup, InputOTPSlot } from '@/shared/ui/primitives/input-otp';
import { Label } from '@/shared/ui/primitives/label';
import { InputGroup, InputGroupAddon, InputGroupInput } from '@/shared/ui/primitives/input-group';
import {
  Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle,
} from '@/shared/ui/primitives/card';
import { Separator } from '@/shared/ui/primitives/separator';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/shared/ui/primitives/tabs';
import { Accordion, AccordionItem, AccordionTrigger, AccordionContent } from '@/shared/ui/primitives/accordion';
import { Collapsible, CollapsibleTrigger, CollapsibleContent } from '@/shared/ui/primitives/collapsible';
import {
  Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList,
  BreadcrumbPage, BreadcrumbSeparator,
} from '@/shared/ui/primitives/breadcrumb';
import {
  Pagination, PaginationContent, PaginationItem, PaginationLink,
  PaginationNext, PaginationPrevious,
} from '@/shared/ui/primitives/pagination';
import { ScrollArea, ScrollBar } from '@/shared/ui/primitives/scroll-area';
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from '@/shared/ui/primitives/resizable';
import {
  Sidebar as SidebarUi, SidebarContent as SidebarContentUi,
  SidebarGroup as SidebarGroupUi, SidebarGroupContent as SidebarGroupContentUi,
  SidebarGroupLabel as SidebarGroupLabelUi, SidebarHeader as SidebarHeaderUi,
  SidebarMenu as SidebarMenuUi, SidebarMenuButton as SidebarMenuButtonUi,
  SidebarMenuItem as SidebarMenuItemUi, SidebarProvider as SidebarProviderUi,
} from '@/shared/ui/primitives/sidebar';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter,
  DialogHeader, DialogTitle, DialogTrigger,
} from '@/shared/ui/primitives/dialog';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader,
  AlertDialogTitle, AlertDialogTrigger,
} from '@/shared/ui/primitives/alert-dialog';
import {
  Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle, SheetTrigger,
} from '@/shared/ui/primitives/sheet';
import {
  Drawer, DrawerContent, DrawerDescription, DrawerHeader, DrawerTitle, DrawerTrigger,
} from '@/shared/ui/primitives/drawer';
import { Popover, PopoverContent, PopoverTrigger } from '@/shared/ui/primitives/popover';
import { HoverCard, HoverCardContent, HoverCardTrigger } from '@/shared/ui/primitives/hover-card';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/shared/ui/primitives/tooltip';
import {
  Command, CommandInput, CommandList, CommandItem,
  CommandEmpty, CommandGroup, CommandSeparator, CommandShortcut,
} from '@/shared/ui/primitives/command';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/shared/ui/primitives/table';
import { Badge } from '@/shared/ui/primitives/badge';
import { Avatar, AvatarFallback, AvatarImage } from '@/shared/ui/primitives/avatar';
import { Calendar } from '@/shared/ui/primitives/calendar';
import { Skeleton } from '@/shared/ui/primitives/skeleton';
import { Item, ItemContent, ItemDescription, ItemTitle } from '@/shared/ui/primitives/item';
import { Alert, AlertDescription, AlertTitle } from '@/shared/ui/primitives/alert';
import { Toaster } from "@/shared/ui/primitives/sonner";
import { toast } from "sonner";
import { Progress } from '@/shared/ui/primitives/progress';
import { Spinner } from '@/shared/ui/primitives/spinner';

export const Route = createFileRoute('/components')({
  component: ComponentsShowcase,
});

interface NavGroup {
  title: string;
  items: { id: string; label: string }[];
}

const NAV: NavGroup[] = [
  { title: 'Foundations', items: [
    { id: 'theme-toggle', label: 'ThemeToggle' },
    { id: 'mono-num', label: 'MonoNum' },
    { id: 'status-pill', label: 'StatusPill' },
    { id: 'kbd-code', label: 'Kbd + Code' },
  ]},
  { title: 'Buttons & actions', items: [
    { id: 'button', label: 'Button' },
    { id: 'button-group', label: 'Button group' },
    { id: 'icon-btn', label: 'Icon button' },
    { id: 'toggle', label: 'Toggle' },
    { id: 'toggle-group', label: 'Toggle group' },
    { id: 'dropdown-menu', label: 'Dropdown menu' },
    { id: 'context-menu', label: 'Context menu' },
  ]},
  { title: 'Forms', items: [
    { id: 'input', label: 'Input' },
    { id: 'textarea', label: 'Textarea' },
    { id: 'select', label: 'Select' },
    { id: 'combobox', label: 'Combobox' },
    { id: 'checkbox', label: 'Checkbox' },
    { id: 'radio', label: 'Radio' },
    { id: 'switch', label: 'Switch' },
    { id: 'slider', label: 'Slider' },
    { id: 'input-otp', label: 'Input OTP' },
    { id: 'field', label: 'Field' },
    { id: 'label', label: 'Label' },
    { id: 'input-group', label: 'Input group' },
  ]},
  { title: 'Layout', items: [
    { id: 'card', label: 'Card' },
    { id: 'separator', label: 'Separator' },
    { id: 'tabs', label: 'Tabs' },
    { id: 'accordion', label: 'Accordion' },
    { id: 'collapsible', label: 'Collapsible' },
    { id: 'breadcrumb', label: 'Breadcrumb' },
    { id: 'pagination', label: 'Pagination' },
    { id: 'scroll-area', label: 'Scroll area' },
    { id: 'aspect-ratio', label: 'Aspect ratio' },
    { id: 'resizable', label: 'Resizable' },
    { id: 'sidebar', label: 'Sidebar' },
  ]},
  { title: 'Overlays', items: [
    { id: 'dialog', label: 'Dialog' },
    { id: 'alert-dialog', label: 'Alert dialog' },
    { id: 'sheet', label: 'Sheet' },
    { id: 'drawer', label: 'Drawer' },
    { id: 'popover', label: 'Popover' },
    { id: 'hover-card', label: 'Hover card' },
    { id: 'tooltip', label: 'Tooltip' },
    { id: 'command', label: 'Command palette' },
  ]},
  { title: 'Data display', items: [
    { id: 'table', label: 'Table' },
    { id: 'badge', label: 'Badge' },
    { id: 'avatar', label: 'Avatar' },
    { id: 'calendar', label: 'Calendar' },
    { id: 'chart', label: 'Chart' },
    { id: 'skeleton', label: 'Skeleton' },
    { id: 'empty', label: 'Empty state' },
    { id: 'item', label: 'Item' },
  ]},
  { title: 'Feedback', items: [
    { id: 'alert', label: 'Alert' },
    { id: 'sonner', label: 'Sonner toast' },
    { id: 'progress', label: 'Progress' },
    { id: 'spinner', label: 'Spinner' },
    { id: 'kpi', label: 'KPI card' },
  ]},
];

function ComponentsShowcase() {
  return (
    <div className="grid grid-cols-[240px_1fr] gap-8 px-8 py-8">
      <ComponentsNav />
      <main className="min-w-0 space-y-16 pb-24">
        <Header />
        {NAV.map((group) => (
          <NavSection key={group.title} group={group} />
        ))}
        <Footer />
      </main>
    </div>
  );
}

function ComponentsNav() {
  const [active, setActive] = useState<string | null>(null);
  useEffect(() => {
    const onScroll = () => {
      const headings = Array.from(document.querySelectorAll<HTMLElement>('[data-section]'));
      const offset = 100;
      const top = window.scrollY + offset;
      let current: string | null = null;
      for (const h of headings) {
        if (h.offsetTop <= top) current = h.id;
      }
      setActive(current);
    };
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  return (
    <aside className="sticky top-12 h-[calc(100vh-3rem)] self-start overflow-y-auto">
      <div className="space-y-4 pr-3">
        <Link to="/" className="text-muted-foreground hover:text-foreground block text-xs">
          ← back to showcase
        </Link>
        <nav className="space-y-4 text-sm">
          {NAV.map((group) => (
            <div key={group.title}>
              <div className="eyebrow mb-1.5 px-2">{group.title}</div>
              <ul className="space-y-0.5">
                {group.items.map((item) => (
                  <li key={item.id}>
                    <a
                      href={`#${item.id}`}
                      className={
                        'block rounded-sm px-2 py-1 text-[13px] transition-colors ' +
                        (active === item.id
                          ? 'bg-muted text-foreground font-medium'
                          : 'text-muted-foreground hover:bg-muted/50 hover:text-foreground')
                      }
                    >
                      {item.label}
                    </a>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </nav>
      </div>
    </aside>
  );
}

function Header() {
  return (
    <header className="space-y-2 border-b border-border pb-6">
      <div className="eyebrow">Plexor Portal · reference</div>
      <h1 className="text-2xl font-semibold tracking-tight">Components</h1>
      <p className="text-muted-foreground text-sm">
        Every shadcn primitive (60) + 3 custom (StatusPill, MonoNum, ThemeToggle)
        + Plexor DS flat classes. Each section: live examples + import path.
      </p>
      <div className="mt-3 flex items-center gap-2 text-xs">
        <span className="pill ok"><span className="dot"></span>60 primitives</span>
        <span className="pill tag">+ 3 custom</span>
        <span className="pill tag">auto dark mode</span>
      </div>
    </header>
  );
}

function NavSection({ group }: { group: NavGroup }) {
  return (
    <section className="space-y-6">
      <h2 className="text-eyebrow text-muted-foreground border-b border-border pb-1.5">
        {group.title}
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
        <code className="code text-[11px]">@/shared/ui/primitives/{id}</code>
      </header>
      <Component id={id} />
    </article>
  );
}

function Component({ id }: { id: string }) {
  switch (id) {
    case 'theme-toggle':
      return (
        <Demo label="Light/dark switch — persisted in localStorage">
          <div className="flex items-center gap-3">
            <ThemeToggle />
            <span className="text-muted-foreground text-xs">click to toggle · reload preserves state</span>
          </div>
        </Demo>
      );
    case 'mono-num':
      return (
        <Demo label="Tabular numerics in dense columns">
          <div className="space-y-1 text-sm">
            <div className="flex justify-between gap-4"><span className="text-muted-foreground">IP</span><MonoNum>10.128.42.17</MonoNum></div>
            <div className="flex justify-between gap-4"><span className="text-muted-foreground">ID</span><MonoNum muted>tnt_8c2a4ef9</MonoNum></div>
            <div className="flex justify-between gap-4"><span className="text-muted-foreground">Size</span><MonoNum>128.45 GB</MonoNum></div>
            <div className="flex justify-between gap-4"><span className="text-muted-foreground">Duration</span><MonoNum muted>2h 47m</MonoNum></div>
          </div>
        </Demo>
      );
    case 'status-pill':
      return (
        <Demo label="Status semantics — ok/err/warn/idle + aliases">
          <div className="flex flex-wrap gap-2">
            <StatusPill variant="running">running</StatusPill>
            <StatusPill variant="ok">active</StatusPill>
            <StatusPill variant="pending">pending</StatusPill>
            <StatusPill variant="warn">degraded</StatusPill>
            <StatusPill variant="err">failed</StatusPill>
            <StatusPill variant="idle">stopped</StatusPill>
            <StatusPill variant="ok" hideDot>no-dot</StatusPill>
          </div>
        </Demo>
      );
    case 'kbd-code':
      return (
        <Demo label="Inline display helpers (status-bar microcopy)">
          <div className="space-y-1.5 text-sm">
            <div><span className="eyebrow mr-3 inline-block w-32">Code</span><code className="code">plx tenant create</code></div>
            <div><span className="eyebrow mr-3 inline-block w-32">Kbd (chord)</span><span className="kbd">⌘</span> <span className="kbd">K</span> open palette</div>
            <div><span className="eyebrow mr-3 inline-block w-32">Kbd (single)</span>Press <span className="kbd">Esc</span> to close</div>
          </div>
        </Demo>
      );
    case 'button':
      return (
        <div className="space-y-3">
          <Demo label="Variants">
            <div className="flex flex-wrap items-center gap-2">
              <Button>Default</Button>
              <Button variant="default">Primary</Button>
              <Button variant="outline">Outline</Button>
              <Button variant="secondary">Secondary</Button>
              <Button variant="ghost">Ghost</Button>
              <Button variant="destructive">Destructive</Button>
              <Button variant="link">Link</Button>
              <Button disabled>Disabled</Button>
            </div>
          </Demo>
          <Demo label="Sizes (Plexor DS — xs/sm/md/lg/xl)">
            <div className="flex flex-wrap items-center gap-2">
              <Button size="xs">xs · 24px</Button>
              <Button size="sm">sm · 28px</Button>
              <Button size="md">md · 32px</Button>
              <Button size="lg">lg · 40px</Button>
              <Button size="xl">xl · 48px</Button>
            </div>
          </Demo>
        </div>
      );
    case 'button-group':
      return (
        <Demo label="Segmented control — 3 buttons, active one">
          <ButtonGroup>
            <Button variant="outline">List</Button>
            <Button variant="outline">Grid</Button>
            <Button variant="outline">Map</Button>
          </ButtonGroup>
        </Demo>
      );
    case 'icon-btn':
      return (
        <Demo label="shadcn Button with size=icon-xs/sm/md/lg + data-tooltip">
          <div className="flex items-center gap-2">
            <Button size="icon-sm" variant="ghost" data-tooltip="Settings" aria-label="Settings">⚙</Button>
            <Button size="icon" variant="ghost" data-tooltip="Notifications" aria-label="Notifications">◑</Button>
            <Button size="icon-lg" variant="ghost" data-tooltip="Delete" aria-label="Delete" className="text-[var(--err-ink)] hover:bg-[var(--err-soft)]">✕</Button>
          </div>
        </Demo>
      );
    case 'toggle':
      return (
        <Demo label="On / off">
          <div className="flex items-center gap-3">
            <Switch id="tgl1" defaultChecked />
            <Switch id="tgl2" />
            <label htmlFor="tgl1" className="text-sm">Enable notifications</label>
          </div>
        </Demo>
      );
    case 'toggle-group':
      return (
        <Demo label="Single-select group">
          {/* @ts-expect-error — ToggleGroup type signature mismatch between Base UI and shadcn wrapper */}
          <ToggleGroup type="single" defaultValue={['running']}>
            <ToggleGroupItem value="running">running</ToggleGroupItem>
            <ToggleGroupItem value="stopped">stopped</ToggleGroupItem>
            <ToggleGroupItem value="all">all</ToggleGroupItem>
          </ToggleGroup>
        </Demo>
      );
    case 'dropdown-menu':
      return (
        <Demo label="Action menu">
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
        <Demo label="Right-click menu on a row">
          <ContextMenu>
            <ContextMenuTrigger render={<div className="rounded-md border border-dashed border-border p-4 text-sm text-muted-foreground">Right-click here</div>} />
            <ContextMenuContent>
              <ContextMenuItem>Open</ContextMenuItem>
              <ContextMenuItem>Inspect</ContextMenuItem>
              <ContextMenuItem>Copy ID</ContextMenuItem>
            </ContextMenuContent>
          </ContextMenu>
        </Demo>
      );
    case 'input':
      return (
        <div className="space-y-3">
          <Demo label="Basic"><Input placeholder="Search tenants…" /></Demo>
          <Demo label="With label + hint">
            <div className="field max-w-md">
              <label htmlFor="inp1">Tenant name</label>
              <Input id="inp1" defaultValue="acme-prod" />
              <div className="field-hint">Lowercase, dashes only.</div>
            </div>
          </Demo>
        </div>
      );
    case 'textarea':
      return (
        <Demo label="Multi-line">
          <Textarea placeholder="Описание…" rows={3} className="max-w-md" />
        </Demo>
      );
    case 'select':
      return (
        <Demo label="Base UI Select (popover, a11y, searchable)">
          <SelectUi defaultValue="running">
            <SelectTrigger className="max-w-xs"><SelectValue placeholder="Select status" /></SelectTrigger>
            <SelectContent>
              <SelectItem value="running">running</SelectItem>
              <SelectItem value="stopped">stopped</SelectItem>
              <SelectItem value="failed">failed</SelectItem>
              <SelectItem value="pending">pending</SelectItem>
            </SelectContent>
          </SelectUi>
        </Demo>
      );
    case 'combobox':
      return (
        <Demo label="Searchable combobox">
          <Combobox>
            <ComboboxInput placeholder="Search image…" />
            <ComboboxContent>
              <ComboboxItem value="ubuntu-22">Ubuntu 22.04 LTS</ComboboxItem>
              <ComboboxItem value="debian-12">Debian 12</ComboboxItem>
              <ComboboxItem value="alpine-3">Alpine 3.19</ComboboxItem>
            </ComboboxContent>
          </Combobox>
        </Demo>
      );
    case 'checkbox':
      return (
        <Demo label="Base UI Checkbox (controlled)">
          <div className="flex items-center gap-4">
            <Checkbox id="cb1" defaultChecked />
            <label htmlFor="cb1" className="text-sm">Enable metering</label>
          </div>
        </Demo>
      );
    case 'radio':
      return (
        <Demo label="Base UI RadioGroup">
          <RadioGroup defaultValue="hourly" className="flex flex-col gap-2">
            <div className="flex items-center gap-2">
              <RadioGroupItem value="hourly" id="r1" />
              <label htmlFor="r1" className="text-sm">Hourly billing</label>
            </div>
            <div className="flex items-center gap-2">
              <RadioGroupItem value="monthly" id="r2" />
              <label htmlFor="r2" className="text-sm">Monthly billing</label>
            </div>
          </RadioGroup>
        </Demo>
      );
    case 'switch':
      return (
        <Demo label="On / off">
          <div className="flex items-center gap-3">
            <Switch id="sw1" defaultChecked />
            <Switch id="sw2" />
            <label htmlFor="sw1" className="text-sm">Enable notifications</label>
          </div>
        </Demo>
      );
    case 'slider':
      return (
        <Demo label="Single value">
          <Slider defaultValue={[50]} max={100} step={1} className="max-w-sm" />
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
    case 'field':
      return (
        <Demo label="Form field (label + input + hint)">
          <div className="field max-w-sm">
            <label htmlFor="fld1">Slug</label>
            <Input id="fld1" placeholder="acme-prod" />
            <div className="field-hint">Lowercase, dashes only.</div>
          </div>
        </Demo>
      );
    case 'label':
      return (
        <Demo label="Label (with htmlFor)">
          <div className="flex items-center gap-2">
            <Label htmlFor="lbl1">Email</Label>
            <Input id="lbl1" type="email" placeholder="dev@plexor.cloud" />
          </div>
        </Demo>
      );
    case 'input-group':
      return (
        <Demo label="With leading addons">
          <InputGroup>
            <InputGroupAddon>https://</InputGroupAddon>
            <InputGroupInput placeholder="example.com" />
          </InputGroup>
        </Demo>
      );
    case 'card':
      return (
        <Demo label="Card with header, content, footer">
          <Card className="max-w-sm">
            <CardHeader>
              <CardTitle>Cluster prod-eu-1</CardTitle>
              <CardDescription>3 nodes · 14 VMs</CardDescription>
            </CardHeader>
            <CardContent className="space-y-2 text-sm">
              <div className="flex justify-between"><span className="text-muted-foreground">Status</span><StatusPill variant="running">healthy</StatusPill></div>
              <div className="flex justify-between"><span className="text-muted-foreground">CPU</span><MonoNum>42 %</MonoNum></div>
            </CardContent>
            <CardFooter>
              <Button variant="outline" size="sm">View</Button>
              <Button size="sm">Manage</Button>
            </CardFooter>
          </Card>
        </Demo>
      );
    case 'separator':
      return (
        <Demo label="Horizontal">
          <div className="text-sm">
            <div>Section A</div>
            <Separator className="my-2" />
            <div>Section B</div>
          </div>
        </Demo>
      );
    case 'tabs':
      return (
        <Demo label="Plexor DS underlined tabs">
          <Tabs defaultValue="overview">
            <TabsList>
              <TabsTrigger value="overview">Overview</TabsTrigger>
              <TabsTrigger value="nodes">Nodes (3)</TabsTrigger>
              <TabsTrigger value="vms">VMs (14)</TabsTrigger>
            </TabsList>
            <TabsContent value="overview" className="text-sm">Cluster healthy.</TabsContent>
            <TabsContent value="nodes" className="text-sm">3 nodes online.</TabsContent>
            <TabsContent value="vms" className="text-sm">14 VMs across 3 nodes.</TabsContent>
          </Tabs>
        </Demo>
      );
    case 'accordion':
      return (
        <Demo label="Disclosure sections">
          <Accordion defaultValue={['1']} className="max-w-md">
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
            <CollapsibleContent className="mt-2 text-sm">Hidden content revealed on click.</CollapsibleContent>
          </Collapsible>
        </Demo>
      );
    case 'breadcrumb':
      return (
        <Demo label="Page hierarchy">
          <Breadcrumb>
            <BreadcrumbList>
              <BreadcrumbItem><BreadcrumbLink href="/">Plexor</BreadcrumbLink></BreadcrumbItem>
              <BreadcrumbSeparator />
              <BreadcrumbItem><BreadcrumbLink href="/tenants">Tenants</BreadcrumbLink></BreadcrumbItem>
              <BreadcrumbSeparator />
              <BreadcrumbItem><BreadcrumbPage>acme-prod</BreadcrumbPage></BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
        </Demo>
      );
    case 'pagination':
      return (
        <Demo label="Page navigation">
          <Pagination>
            <PaginationContent>
              <PaginationItem><PaginationPrevious href="#" /></PaginationItem>
              <PaginationItem><PaginationLink href="#" isActive>1</PaginationLink></PaginationItem>
              <PaginationItem><PaginationLink href="#">2</PaginationLink></PaginationItem>
              <PaginationItem><PaginationLink href="#">3</PaginationLink></PaginationItem>
              <PaginationItem><PaginationNext href="#" /></PaginationItem>
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
    case 'aspect-ratio':
      return (
        <Demo label="16:9 ratio box">
          <div className="aspect-video w-72 rounded-md bg-muted" />
        </Demo>
      );
    case 'resizable':
      return (
        <Demo label="Horizontal split">
          <ResizablePanelGroup orientation="horizontal" className="h-32 w-72 rounded-md border">
            <ResizablePanel defaultSize={50}><div className="p-3 text-sm">Left</div></ResizablePanel>
            <ResizableHandle />
            <ResizablePanel defaultSize={50}><div className="p-3 text-sm">Right</div></ResizablePanel>
          </ResizablePanelGroup>
        </Demo>
      );
    case 'sidebar':
      return (
        <Demo label="Collapsible app sidebar (scaffold)">
          <SidebarProviderUi>
            <SidebarUi collapsible="icon" className="border-r">
              <SidebarHeaderUi>
                <div className="px-2 py-1.5 text-sm font-semibold">Plexor</div>
              </SidebarHeaderUi>
              <SidebarContentUi>
                <SidebarGroupUi>
                  <SidebarGroupLabelUi>Compute</SidebarGroupLabelUi>
                  <SidebarGroupContentUi>
                    <SidebarMenuUi>
                      <SidebarMenuItemUi><SidebarMenuButtonUi>VMs</SidebarMenuButtonUi></SidebarMenuItemUi>
                      <SidebarMenuItemUi><SidebarMenuButtonUi>Volumes</SidebarMenuButtonUi></SidebarMenuItemUi>
                    </SidebarMenuUi>
                  </SidebarGroupContentUi>
                </SidebarGroupUi>
              </SidebarContentUi>
            </SidebarUi>
          </SidebarProviderUi>
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
                <DialogDescription>All 14 VMs and 38 floating IPs will be removed. This action cannot be undone.</DialogDescription>
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
                <AlertDialogDescription>This action cannot be undone. This will permanently delete the project.</AlertDialogDescription>
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
        <Demo label="Bottom-anchored modal">
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
    case 'popover':
      return (
        <Demo label="Floating menu with arrow">
          <Popover>
            <PopoverTrigger render={<Button variant="outline">Open popover</Button>} />
            <PopoverContent className="w-64">
              <div className="text-sm">
                <div className="mb-1 font-medium">Quick info</div>
                <div className="text-muted-foreground">Popovers are floating UI panels triggered by click.</div>
              </div>
            </PopoverContent>
          </Popover>
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
    case 'tooltip':
      return (
        <Demo label="Hover-only label">
          <Tooltip>
            <TooltipTrigger render={<Button variant="outline">Hover me</Button>} />
            <TooltipContent>Plexor DS tooltip</TooltipContent>
          </Tooltip>
        </Demo>
      );
    case 'command':
      return (
        <Demo label="⌘K command palette (cmdk)">
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
                <CommandItem value="view-audit">
                  <span>View audit log</span>
                </CommandItem>
              </CommandGroup>
              <CommandSeparator />
              <CommandGroup heading="Navigate">
                <CommandItem value="go-vms">VMs</CommandItem>
                <CommandItem value="go-volumes">Volumes</CommandItem>
                <CommandItem value="go-billing">Billing</CommandItem>
              </CommandGroup>
            </CommandList>
          </Command>
        </Demo>
      );
    case 'table':
      return (
        <Demo label="Dense rows with StatusPill + MonoNum">
          <div className="table-wrap">
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
                  <TableCell><MonoNum muted>tnt_8c2a</MonoNum></TableCell>
                  <TableCell className="font-medium">acme-prod</TableCell>
                  <TableCell className="text-right"><MonoNum>38</MonoNum></TableCell>
                  <TableCell><StatusPill variant="running">running</StatusPill></TableCell>
                </TableRow>
                <TableRow>
                  <TableCell><MonoNum muted>tnt_a912</MonoNum></TableCell>
                  <TableCell className="font-medium">legacy-poc</TableCell>
                  <TableCell className="text-right"><MonoNum>0</MonoNum></TableCell>
                  <TableCell><StatusPill variant="idle">stopped</StatusPill></TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </div>
        </Demo>
      );
    case 'badge':
      return (
        <Demo label="Default + variants">
          <div className="flex flex-wrap items-center gap-2">
            <Badge>Default</Badge>
            <Badge variant="secondary">Secondary</Badge>
            <Badge variant="outline">Outline</Badge>
            <Badge variant="destructive">Destructive</Badge>
          </div>
        </Demo>
      );
    case 'avatar':
      return (
        <Demo label="Initials + image">
          <div className="flex items-center gap-3">
            <Avatar>
              <AvatarImage src="https://github.com/shadcn.png" alt="@shadcn" />
              <AvatarFallback>CN</AvatarFallback>
            </Avatar>
            <Avatar><AvatarFallback>SM</AvatarFallback></Avatar>
            <Avatar size="sm"><AvatarFallback>SM</AvatarFallback></Avatar>
            <Avatar size="lg"><AvatarFallback>LG</AvatarFallback></Avatar>
          </div>
        </Demo>
      );
    case 'calendar':
      return (
        <Demo label="Date picker (single)">
          <Calendar mode="single" className="rounded-md border" />
        </Demo>
      );
    case 'chart':
      return (
        <Demo label="Chart container">
          <div className="rounded-md border p-4 text-sm text-muted-foreground">
            ChartContainer from shadcn/charts. Wrap recharts for theming.
          </div>
        </Demo>
      );
    case 'skeleton':
      return (
        <Demo label="Loading placeholder with shimmer">
          <div className="space-y-2 max-w-sm">
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-4 w-1/2" />
            <Skeleton className="h-4 w-5/6" />
          </div>
        </Demo>
      );
    case 'empty':
      return (
        <Demo label="Empty-state placeholder">
          <div className="empty-state border border-dashed border-border rounded-md">
            <div className="mb-1 text-foreground">No tenants</div>
            <div>Create your first tenant to get started.</div>
          </div>
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
    case 'alert':
      return (
        <Demo label="Info + Destructive variants">
          <div className="space-y-2 max-w-md">
            <Alert>
              <AlertTitle>Heads up!</AlertTitle>
              <AlertDescription>You can add components to your app using the CLI.</AlertDescription>
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
            <Button onClick={() => toast('Tenant created', { description: 'New tenant acme-prod' })}>Default</Button>
            <Button onClick={() => toast.success('VM started')}>Success</Button>
            <Button onClick={() => toast.error('Failed to connect')} variant="destructive">Error</Button>
            <Toaster position="bottom-right" />
          </div>
        </Demo>
      );
    case 'progress':
      return (
        <Demo label="Linear progress">
          <Progress value={66} className="max-w-sm" />
        </Demo>
      );
    case 'spinner':
      return (
        <Demo label="Loading indicator">
          <Spinner className="size-6" />
        </Demo>
      );
    case 'kpi':
      return (
        <Demo label="KPI card (Plexor DS — billing)">
          <div className="kpi max-w-xs">
            <div className="kpi-label">Расход за месяц</div>
            <div className="kpi-value">$ 12,847</div>
            <div className="kpi-trend up">+8.4 % vs прошлый</div>
          </div>
        </Demo>
      );
    default:
      return null;
  }
}

function Demo({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="rounded-md border border-border bg-card p-4">
      <div className="eyebrow mb-2">{label}</div>
      {children}
    </div>
  );
}

function Footer() {
  return (
    <footer className="border-t border-border pt-6 text-muted-foreground text-xs">
      <div className="space-y-1">
        <div><span className="font-mono">.agents/docs/ui/architecture.md</span> · 14 stack decisions</div>
        <div><span className="font-mono">.agents/docs/design/styles.css</span> · 876 lines of Plexor DS tokens</div>
      </div>
    </footer>
  );
}

import { createFileRoute, Link } from '@tanstack/react-router';
import { useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/shared/ui/primitives/dialog';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { ThemeToggle } from '@/shared/ui/primitives/theme-toggle';

export const Route = createFileRoute('/')({
  component: DesignSystemShowcase,
});

const TENANTS = [
  { id: 'tnt_8c2a4ef9', name: 'acme-prod', vms: 38, ip: '10.128.42.17', region: 'eu-central-1', status: 'running' as const },
  { id: 'tnt_1f7be20a', name: 'beta-eval', vms: 3, ip: '10.128.7.4', region: 'eu-central-1', status: 'pending' as const },
  { id: 'tnt_44de8c11', name: 'internal-tools', vms: 12, ip: '10.128.99.1', region: 'eu-west-1', status: 'ok' as const },
  { id: 'tnt_a912f7aa', name: 'legacy-poc', vms: 0, ip: '—', region: 'eu-central-1', status: 'stopped' as const },
  { id: 'tnt_0e5544b1', name: 'demo-broken', vms: 0, ip: '—', region: 'eu-central-1', status: 'failed' as const },
];

const KPIS = [
  { label: 'Расход за месяц', value: '$ 12,847.42', delta: '+8.4 %', trend: 'up' as const },
  { label: 'VMs active', value: '38', delta: '+3', trend: 'up' as const },
  { label: 'CPU avg', value: '42 %', delta: '-1.2 %', trend: 'down' as const },
  { label: 'Disk used', value: '8.4 / 20 TB', delta: '+0.3 TB', trend: 'up' as const },
];

const AUDIT = [
  { ts: '12:48:02', actor: 'bradw', action: 'vm.create', target: 'vm-prod-014', result: 'ok' as const, ip: '10.128.42.17' },
  { ts: '12:45:11', actor: 'fury', action: 'volume.attach', target: 'vol-9a2', result: 'ok' as const, ip: '10.128.42.18' },
  { ts: '12:30:09', actor: 'system', action: 'node.reconcile', target: 'node-eu-1b', result: 'warn' as const, ip: '—' },
  { ts: '12:14:55', actor: 'maria', action: 'sg.update', target: 'sg-web-frontend', result: 'ok' as const, ip: '10.128.42.21' },
  { ts: '11:58:00', actor: 'bradw', action: 'vm.terminate', target: 'vm-test-007', result: 'err' as const, ip: '10.128.42.17' },
];

function DesignSystemShowcase() {
  return (
    <div className="space-y-12 p-8">
      <TopNav />
      <PageHeader />
      <Section id="status" eyebrow="Status" title="Статусы — ok / err / warn / idle">
        <div className="flex flex-wrap items-center gap-2">
          <StatusPill variant="running">running</StatusPill>
          <StatusPill variant="ok">active</StatusPill>
          <StatusPill variant="pending">pending</StatusPill>
          <StatusPill variant="warn">degraded</StatusPill>
          <StatusPill variant="err">failed</StatusPill>
          <StatusPill variant="idle">stopped</StatusPill>
          <StatusPill variant="ok" hideDot>no-dot</StatusPill>
          <span className="pill tag">tag</span>
          <span className="pill tag">region=eu</span>
          <span className="pill tag">mono</span>
        </div>
      </Section>

      <Section
        id="buttons"
        eyebrow="Buttons"
        title="Кнопки — flat, no translate, accent = monochrome"
      >
        <div className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <button className="btn primary">Primary</button>
            <button className="btn">Default</button>
            <button className="btn lg">Large (32)</button>
            <button className="btn sm">Small (24)</button>
            <button className="btn danger">Danger</button>
            <button className="btn danger-solid">Danger solid</button>
            <button className="btn ghost">Ghost</button>
            <button className="btn" disabled>
              Disabled
            </button>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <div className="btn-group">
              <button className="btn is-active">List</button>
              <button className="btn">Grid</button>
              <button className="btn">Map</button>
            </div>
            <div className="btn-group">
              <button className="btn sm is-active">7d</button>
              <button className="btn sm">30d</button>
              <button className="btn sm">90d</button>
            </div>
          </div>
        </div>
      </Section>

      <Section
        id="chips"
        eyebrow="Chips"
        title="Фильтр-чипы — поверхность, pill-shape, нажимаемые"
      >
        <div className="toolbar">
          <div className="toolbar-group">
            <button className="chip is-on" aria-pressed="true">
              running <span className="chip-count mono">12</span>
            </button>
            <button className="chip" aria-pressed="false">
              stopped <span className="chip-count mono">3</span>
            </button>
            <button className="chip" aria-pressed="false">
              failed <span className="chip-count mono">1</span>
            </button>
          </div>
          <div className="toolbar-group">
            <button className="chip" aria-pressed="false">
              eu-central-1
            </button>
            <button className="chip is-on" aria-pressed="true">
              eu-west-1
            </button>
          </div>
          <div className="toolbar-end">
            <button className="btn sm">Clear</button>
            <button className="btn sm primary">Apply</button>
          </div>
        </div>
      </Section>

      <Section
        id="inputs"
        eyebrow="Forms"
        title="Инпуты — 28px плотные, focus = outline + box-shadow ring"
      >
        <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
          <div className="space-y-4">
            <div className="field">
              <label htmlFor="t-name">Имя тенанта</label>
              <input id="t-name" className="input" placeholder="acme-prod" defaultValue="acme-prod" />
              <div className="field-hint">Lowercase, dashes only.</div>
            </div>
            <div className="field">
              <label htmlFor="t-slug">Slug</label>
              <input id="t-slug" className="input mono" placeholder="acme-prod" />
            </div>
            <div className="field">
              <label htmlFor="t-region">Region</label>
              <div className="select-wrap">
                <select id="t-region" className="select" defaultValue="eu-central-1">
                  <option>eu-central-1</option>
                  <option>eu-west-1</option>
                  <option>us-east-1</option>
                </select>
              </div>
            </div>
          </div>
          <div className="space-y-4">
            <div className="field">
              <label htmlFor="t-desc">Описание</label>
              <textarea id="t-desc" className="textarea" rows={3} placeholder="Краткое описание…" />
            </div>
            <div className="field">
              <label className="check">
                <input type="checkbox" defaultChecked />
                <span>Enable metering for this tenant</span>
              </label>
              <label className="radio">
                <input type="radio" name="billing" defaultChecked />
                <span>Hourly billing</span>
              </label>
              <label className="radio">
                <input type="radio" name="billing" />
                <span>Monthly billing</span>
              </label>
            </div>
          </div>
        </div>
      </Section>

      <Section
        id="table"
        eyebrow="Table"
        title="Таблица — плотные строки 30px, mono в numeric, sticky header"
      >
        <div className="table-wrap">
          <table className="tbl">
            <thead>
              <tr>
                <th>Tenant</th>
                <th>Region</th>
                <th className="num">VMs</th>
                <th>Status</th>
                <th>IP</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {TENANTS.map((t) => (
                <tr key={t.id}>
                  <td>
                    <MonoNum muted>{t.id}</MonoNum>
                  </td>
                  <td className="mono">{t.region}</td>
                  <td className="num">
                    <MonoNum>{t.vms}</MonoNum>
                  </td>
                  <td>
                    <StatusPill variant={t.status}>{t.status}</StatusPill>
                  </td>
                  <td>
                    <MonoNum muted>{t.ip}</MonoNum>
                  </td>
                  <td style={{ width: 32, textAlign: 'right' }}>
                    <button
                      className="icon-btn sm"
                      data-tooltip="Open tenant"
                      aria-label="Open"
                    >
                      →
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="pagination">
            <div className="pg-info mono">
              Rows 1–5 of 5 · selected: 0
            </div>
            <div className="pg-controls">
              <button className="pg-page" aria-label="prev">
                ‹
              </button>
              <button className="pg-page is-active">1</button>
              <button className="pg-page" aria-label="next">
                ›
              </button>
            </div>
          </div>
        </div>
      </Section>

      <TabsSection />

      <Section
        id="kpis"
        eyebrow="KPI"
        title="Карточки KPI — billing dashboard"
      >
        <div className="kpi-grid">
          {KPIS.map((k) => (
            <div key={k.label} className="kpi">
              <div className="kpi-label">{k.label}</div>
              <div className="kpi-value">{k.value}</div>
              <div className={`kpi-trend ${k.trend}`}>
                <span>{k.delta}</span>
                <span className="text-muted-foreground">vs прошлый месяц</span>
              </div>
            </div>
          ))}
        </div>
      </Section>

      <Section
        id="audit"
        eyebrow="Audit row"
        title="Audit log timeline — фиксированные колонки, hover подсветка"
      >
        <div className="table-wrap">
          {AUDIT.map((row, i) => (
            <div key={i} className="audit-row">
              <span className="mono">{row.ts}</span>
              <span className="mono">{row.actor}</span>
              <span className="mono">{row.action}</span>
              <span>{row.target}</span>
              <span className="flex items-center gap-2">
                <StatusPill variant={row.result}>{row.result}</StatusPill>
                <MonoNum muted>{row.ip}</MonoNum>
              </span>
            </div>
          ))}
        </div>
      </Section>

      <Section
        id="console"
        eyebrow="Console"
        title="Serial console — dark panel для boot-логов"
      >
        <div className="console">
          <span className="line-ok">[ ok ]</span> Reached target Multi-User System.
          <br />
          <span className="line-ok">[ ok ]</span> Started Plexor Node Agent v0.1.0.
          <br />
          <span className="line-err">[warn]</span> Network interface eth0: DHCP lease in 38s.
          <br />
          <span className="line-ok">[ ok ]</span> Mounted /var/lib/plexor.
          <br />
          <span>$</span> <span className="line-cursor" />
        </div>
      </Section>

      <Section
        id="alerts"
        eyebrow="Alerts"
        title="Alerts / Empty / Skeleton — граничные состояния"
      >
        <div className="space-y-4">
          <div
            className="rounded-md border p-3"
            style={{
              borderColor: 'var(--warn)',
              background: 'var(--warn-soft)',
              color: 'var(--warn-ink)',
            }}
          >
            <div className="font-semibold">Quota exceeded</div>
            <div className="text-sm">
              Tenant <span className="font-mono">acme-prod</span> reached its VM limit (38/38).
            </div>
          </div>
          <div
            className="rounded-md border p-3"
            style={{
              borderColor: 'var(--err)',
              background: 'var(--err-soft)',
              color: 'var(--err-ink)',
            }}
          >
            <div className="font-semibold">Node down</div>
            <div className="text-sm">node-eu-1b unreachable for 5 minutes.</div>
          </div>
          <div className="rounded-md border border-border bg-card p-4">
            <div className="empty-state">
              <div className="mb-2">No tenants</div>
              <div className="text-muted-foreground text-xs">
                Create your first tenant to get started.
              </div>
            </div>
          </div>
          <div className="rounded-md border border-border bg-card p-4">
            <div className="space-y-2">
              <div className="skeleton line" style={{ width: '40%' }} />
              <div className="skeleton line sm" style={{ width: '60%' }} />
              <div className="skeleton line sm" style={{ width: '30%' }} />
            </div>
          </div>
        </div>
      </Section>

      <Section
        id="typography"
        eyebrow="Type & helpers"
        title="Типографика — Onest Variable + JetBrains Mono"
      >
        <div className="space-y-4">
          <div>
            <div className="eyebrow mb-2">Heading</div>
            <h1 className="text-2xl font-semibold tracking-tight">Display H1</h1>
            <h2 className="text-xl font-semibold tracking-tight">Heading H2</h2>
            <h3 className="text-base font-semibold tracking-tight">Heading H3</h3>
            <div className="text-sm">
              Body text — 13px / 1.5 line-height — used in most paragraphs.
            </div>
          </div>

          <div>
            <div className="eyebrow mb-2">Helpers</div>
            <div className="space-y-1.5 text-sm">
              <div>
                <span className="eyebrow inline-block w-32">Mono</span>
                <MonoNum>10.128.42.17</MonoNum>
              </div>
              <div>
                <span className="eyebrow inline-block w-32">Code</span>
                <code className="code">plx tenant create</code>
              </div>
              <div>
                <span className="eyebrow inline-block w-32">Kbd</span>
                <span className="kbd">⌘</span> <span className="kbd">K</span> for command palette
              </div>
              <div>
                <span className="eyebrow inline-block w-32">Tab nav</span>
                <span className="kbd">/</span> focus search ·{' '}
                <span className="kbd">Esc</span> close drawer
              </div>
            </div>
          </div>
        </div>
      </Section>

      <Section
        id="toolbar-icon"
        eyebrow="Toolbar + icon buttons + tooltip"
        title="icon-btn 24/28/32 — CSS-only [data-tooltip] hover label"
      >
        <div className="flex items-center gap-2">
          <button className="icon-btn sm" data-tooltip="Settings" aria-label="Settings">
            ⚙
          </button>
          <button
            className="icon-btn"
            data-tooltip="Notifications"
            aria-label="Notifications"
          >
            ◑
          </button>
          <button
            className="icon-btn md danger"
            data-tooltip="Delete (danger)"
            aria-label="Delete"
          >
            ✕
          </button>
          <button className="btn sm" data-tooltip="Click me">Hover me</button>
        </div>
      </Section>

      <Section id="dialog" eyebrow="Dialog" title="Модалка — подтверждение удаления">
        <Dialog>
          <DialogTrigger
            render={<button className="btn danger-solid">Delete cluster</button>}
          />
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Delete cluster prod-eu-1?</DialogTitle>
              <DialogDescription>
                All 14 VMs and 38 floating IPs will be removed. This action cannot
                be undone.
              </DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <button className="btn">Cancel</button>
              <button className="btn danger-solid">Confirm delete</button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </Section>

      <Section
        id="kv"
        eyebrow="Detail"
        title="KV-list + mono values — для detail страниц"
      >
        <dl className="kv-list">
          <dt>ID</dt>
          <dd className="mono">vm-prod-014</dd>
          <dt>Status</dt>
          <dd>
            <StatusPill variant="running">running</StatusPill>
          </dd>
          <dt>Zone</dt>
          <dd className="mono">eu-central-1-a</dd>
          <dt>Node</dt>
          <dd className="mono">node-eu-1a</dd>
          <dt>Internal IP</dt>
          <dd className="mono">10.128.42.17</dd>
          <dt>Public IP</dt>
          <dd className="mono">203.0.113.42</dd>
          <dt>CPU</dt>
          <dd>
            <MonoNum>4 vCPU</MonoNum>
          </dd>
          <dt>RAM</dt>
          <dd>
            <MonoNum>8.0 GB</MonoNum>
          </dd>
          <dt>Disk</dt>
          <dd>
            <MonoNum>128.45 GB</MonoNum>
          </dd>
          <dt>Uptime</dt>
          <dd>
            <MonoNum>14d 22h</MonoNum>
          </dd>
        </dl>
      </Section>

      <footer className="border-t border-border pt-6 text-muted-foreground text-xs">
        <span className="font-mono">.agents/docs/design/styles.css</span> ·{' '}
        <span className="font-mono">.agents/docs/design/design-system.html</span> ·{' '}
        <Link to="/" className="underline">
          refresh
        </Link>
      </footer>
    </div>
  );
}

// ── Sub-components ────────────────────────────────────────────────

function TopNav() {
  return (
    <header className="border-border bg-card sticky top-0 z-40 flex h-12 items-center gap-4 border-b px-5">
      <div className="flex items-center gap-2 text-sm font-semibold tracking-tight">
        <span
          className="flex h-6 w-6 items-center justify-center rounded font-mono text-xs font-bold text-[var(--surface)]"
          style={{ background: 'var(--foreground)' }}
        >
          P
        </span>
        Plexor
      </div>
      <span className="border-l border-border pl-3 font-mono text-xs text-muted-foreground">
        showcase
      </span>
      <div className="flex-1" />
      <div className="flex items-center gap-2 text-sm">
        <span className="text-muted-foreground">Cluster</span>
        <span className="font-medium">prod-eu-1</span>
        <span className="text-muted-foreground">·</span>
        <span className="text-muted-foreground">Project</span>
        <span className="font-medium">default-project</span>
      </div>
      <ThemeToggle />
    </header>
  );
}

function PageHeader() {
  return (
    <header>
      <div className="eyebrow">Plexor Portal · design system reference</div>
      <h1 className="mt-1 text-2xl font-semibold tracking-tight">
        Компоненты и состояния
      </h1>
      <p className="text-muted-foreground mt-1 text-sm">
        Полный mirror{' '}
        <span className="font-mono text-xs">.agents/docs/design/styles.css</span> ·{' '}
        использует Plexor DS tokens (light + dark) + Base UI registry + 4 custom
        primitives: StatusPill, MonoNum, ThemeToggle, Button (sized).
      </p>
      <div className="mt-4 flex flex-wrap gap-2 text-xs">
        <span className="pill ok">
          <span className="dot"></span>build: 1.34s
        </span>
        <span className="pill tag">CSS 29 KB gzip</span>
        <span className="pill tag">JS 95 KB gzip</span>
        <span className="pill tag">60 components</span>
        <span className="pill tag">2 themes</span>
      </div>
    </header>
  );
}

function TabsSection() {
  const [active, setActive] = useState('overview');
  const tabs = [
    { id: 'overview', label: 'Overview' },
    { id: 'nodes', label: 'Nodes', badge: '3' },
    { id: 'vms', label: 'VMs', badge: '14' },
    { id: 'audit', label: 'Audit' },
  ];
  return (
    <Section
      id="tabs"
      eyebrow="Tabs"
      title="Underlined tabs — border-bottom highlight на активном"
    >
      <div className="rounded-md border border-border bg-card overflow-hidden">
        <div className="tabs">
          {tabs.map((t) => (
            <button
              key={t.id}
              className={`tab ${active === t.id ? 'is-active' : ''}`}
              onClick={() => setActive(t.id)}
              data-state={active === t.id ? 'active' : 'inactive'}
            >
              {t.label}
              {t.badge ? <span className="tab-badge">{t.badge}</span> : null}
            </button>
          ))}
        </div>
        <div className="p-4 text-sm">
          {active === 'overview' ? (
            <div>
              Cluster <MonoNum>prod-eu-1</MonoNum> healthy.{' '}
              <span className="text-muted-foreground">No active alerts.</span>
            </div>
          ) : null}
          {active === 'nodes' ? (
            <div>
              <MonoNum>3</MonoNum> nodes online.{' '}
              <MonoNum muted>1 draining</MonoNum>
            </div>
          ) : null}
          {active === 'vms' ? (
            <div>
              <MonoNum>14</MonoNum> VMs across 3 nodes. <MonoNum muted>1 stopped</MonoNum>.
            </div>
          ) : null}
          {active === 'audit' ? (
            <div>
              <MonoNum>3</MonoNum> audit events in the last hour.
            </div>
          ) : null}
        </div>
      </div>
    </Section>
  );
}

function Section({
  id,
  eyebrow,
  title,
  caption,
  children,
}: {
  id?: string;
  eyebrow?: string;
  title: string;
  caption?: string;
  children: React.ReactNode;
}) {
  return (
    <section id={id} className="space-y-3">
      <header className="space-y-1">
        {eyebrow ? <div className="eyebrow">{eyebrow}</div> : null}
        <h2 className="text-base font-semibold tracking-tight">{title}</h2>
        {caption ? <p className="text-muted-foreground text-xs">{caption}</p> : null}
      </header>
      <div>{children}</div>
    </section>
  );
}

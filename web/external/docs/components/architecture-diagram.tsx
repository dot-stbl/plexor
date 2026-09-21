'use client';

import { useState } from 'react';

/**
 * Architecture diagram of Plexor's deployment topology — the canonical
 * mental model from `.agents/docs/architecture.md`. One control plane,
 * N compute nodes, NATS as event bus, PostgreSQL as state DB. Each
 * component is a hoverable block that reveals a one-line note about
 * what it owns. No animation, no gradients — a line drawing.
 */
interface Node {
  readonly id: string;
  readonly label: string;
  readonly sublabel: string;
  readonly note: string;
  readonly x: number;
  readonly y: number;
  readonly w: number;
  readonly h: number;
  readonly kind: 'control' | 'node' | 'bus' | 'state';
}

const NODES: readonly Node[] = [
  {
    id: 'host',
    label: 'Plexor.Host',
    sublabel: 'control plane · 1 binary',
    note: 'REST + gRPC, all modules, BootConfig, audit, metering rollups. Stateless; scale by replicas.',
    x: 30,
    y: 30,
    w: 220,
    h: 64,
    kind: 'control',
  },
  {
    id: 'console',
    label: 'console.plexor',
    sublabel: 'operator UI · React 19',
    note: 'Vite SPA; consumes /api/v1 via the OpenAPI-generated TS client.',
    x: 30,
    y: 110,
    w: 220,
    h: 48,
    kind: 'control',
  },
  {
    id: 'cli',
    label: 'plx',
    sublabel: 'NativeAOT CLI',
    note: 'Same endpoints as the console. Used in plx init, provider install, audit queries.',
    x: 30,
    y: 174,
    w: 220,
    h: 48,
    kind: 'control',
  },
  {
    id: 'nats',
    label: 'NATS JetStream',
    sublabel: 'event bus · single source of fan-out',
    note: 'Subjects: plexor.compute.vm.* · plexor.app.install.* · plexor.network.lb.* · plexor.audit.*.',
    x: 320,
    y: 110,
    w: 200,
    h: 64,
    kind: 'bus',
  },
  {
    id: 'postgres',
    label: 'PostgreSQL',
    sublabel: 'state DB · schema per module',
    note: 'Migrations applied in FK order: realm → sigil → atlas → ... → branding. Aspire Backups / point-in-time.',
    x: 320,
    y: 30,
    w: 200,
    h: 64,
    kind: 'state',
  },
  {
    id: 'node1',
    label: 'Plexor.NodeAgent · #1',
    sublabel: 'compute node · libvirt / OVS / Ceph',
    note: 'Subscribes to plexor.app.install.* · runs provider hooks · reports health every 30s.',
    x: 590,
    y: 30,
    w: 200,
    h: 64,
    kind: 'node',
  },
  {
    id: 'node2',
    label: 'Plexor.NodeAgent · #N',
    sublabel: 'compute node · libvirt / OVS / Ceph',
    note: 'Same binary as #1. New nodes join via JoinToken; no operator round-trip per node.',
    x: 590,
    y: 110,
    w: 200,
    h: 64,
    kind: 'node',
  },
  {
    id: 'kv',
    label: 'Object store',
    sublabel: 'MinIO or Ceph RGW',
    note: 'VM snapshots (qcow2 diffs) · backup archives · app-provider source caches.',
    x: 590,
    y: 174,
    w: 200,
    h: 48,
    kind: 'state',
  },
];

interface Edge {
  readonly from: string;
  readonly to: string;
}

const EDGES: readonly Edge[] = [
  { from: 'host', to: 'console' },
  { from: 'host', to: 'cli' },
  { from: 'host', to: 'nats' },
  { from: 'host', to: 'postgres' },
  { from: 'nats', to: 'node1' },
  { from: 'nats', to: 'node2' },
  { from: 'node1', to: 'kv' },
  { from: 'node2', to: 'kv' },
];

function nodeFill(kind: Node['kind']): string {
  switch (kind) {
    case 'control':
      return 'var(--fd-card)';
    case 'bus':
      return 'var(--fd-surface-3)';
    case 'state':
      return 'var(--fd-card)';
    case 'node':
      return 'var(--fd-secondary)';
  }
}

function nodeStroke(kind: Node['kind']): string {
  switch (kind) {
    case 'control':
      return 'var(--fd-border)';
    case 'bus':
      return 'var(--fd-muted-foreground)';
    case 'state':
      return 'var(--fd-border)';
    case 'node':
      return 'var(--fd-border)';
  }
}

export function ArchitectureDiagram() {
  const [hoverId, setHoverId] = useState<string | null>(null);

  const hoveredNode = NODES.find((n) => n.id === hoverId);

  return (
    <div className="not-prose my-6 grid grid-cols-1 gap-4 lg:grid-cols-[2fr_1fr]">
      <div className="overflow-hidden rounded-xl border border-fd-border bg-fd-card p-2">
        <svg
          viewBox="0 0 820 240"
          className="h-auto w-full"
          role="img"
          aria-label="Plexor architecture diagram: control plane, event bus, state DB, compute nodes"
        >
          {EDGES.map((edge) => (
            <EdgeLine key={`${edge.from}-${edge.to}`} edge={edge} highlight={hoverId} />
          ))}
          {NODES.map((node) => (
            <NodeBlock
              key={node.id}
              node={node}
              hover={hoverId === node.id}
              onHover={setHoverId}
              onLeave={() => setHoverId(null)}
            />
          ))}
        </svg>
      </div>

      <div className="rounded-xl border border-fd-border bg-fd-card p-4">
        <div className="mb-2 text-xs font-medium uppercase tracking-wider text-fd-muted-foreground">
          Что на схеме
        </div>
        {hoveredNode === undefined ? (
          <p className="text-fd-muted-foreground text-sm">
            Наведи на блок, чтобы увидеть его роль. Контур: один control plane
            бинарь, PostgreSQL как state DB, NATS как event bus, N compute nodes
            с локальными KVM/Ceph/OVS. Никаких managed services между ними.
          </p>
        ) : (
          <div className="space-y-2">
            <div>
              <div className="text-sm font-medium">{hoveredNode.label}</div>
              <div className="text-fd-muted-foreground text-xs">
                {hoveredNode.sublabel}
              </div>
            </div>
            <p className="text-sm">{hoveredNode.note}</p>
          </div>
        )}
      </div>
    </div>
  );
}

function NodeBlock({
  node,
  hover,
  onHover,
  onLeave,
}: {
  node: Node;
  hover: boolean;
  onHover: (id: string) => void;
  onLeave: () => void;
}) {
  return (
    <g
      onMouseEnter={() => onHover(node.id)}
      onMouseLeave={onLeave}
      onFocus={() => onHover(node.id)}
      onBlur={onLeave}
      tabIndex={0}
      role="button"
      aria-label={`${node.label} — ${node.sublabel}`}
      style={{ cursor: 'pointer', outline: 'none' }}
    >
      <rect
        x={node.x}
        y={node.y}
        width={node.w}
        height={node.h}
        rx={10}
        fill={nodeFill(node.kind)}
        stroke={hover ? 'var(--fd-primary)' : nodeStroke(node.kind)}
        strokeWidth={hover ? 2 : 1}
      />
      <text
        x={node.x + 14}
        y={node.y + 24}
        fill="var(--fd-foreground)"
        fontSize={13}
        fontWeight={600}
        fontFamily="var(--font-sans)"
      >
        {node.label}
      </text>
      <text
        x={node.x + 14}
        y={node.y + 42}
        fill="var(--fd-muted-foreground)"
        fontSize={11}
        fontFamily="var(--font-sans)"
      >
        {node.sublabel}
      </text>
    </g>
  );
}

function EdgeLine({
  edge,
  highlight,
}: {
  edge: Edge;
  highlight: string | null;
}) {
  const from = NODES.find((n) => n.id === edge.from);
  const to = NODES.find((n) => n.id === edge.to);
  if (from === undefined || to === undefined) return null;

  const x1 = from.x + from.w;
  const y1 = from.y + from.h / 2;
  const x2 = to.x;
  const y2 = to.y + to.h / 2;

  const isHighlighted = highlight === edge.from || highlight === edge.to;

  return (
    <line
      x1={x1}
      y1={y1}
      x2={x2}
      y2={y2}
      stroke={isHighlighted ? 'var(--fd-primary)' : 'var(--fd-border)'}
      strokeWidth={isHighlighted ? 2 : 1}
      strokeDasharray="4 4"
    />
  );
}
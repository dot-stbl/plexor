import type { LxcContainer } from './lxc-types';

const GIB = 1024 ** 3;

/**
 * Mock LXC inventory spread across the two nodes of the self-hosted cluster.
 * Sizes are exact binary bytes — `Size` picks the display unit. Mostly
 * running, with one stopped / paused / error to exercise every status pill.
 */
const CONTAINERS: LxcContainer[] = [
  {
    id: 'lxc-web-01',
    name: 'web-01',
    status: 'running',
    template: 'ubuntu-24.04',
    os: 'Ubuntu',
    osVersion: '24.04',
    cores: 2,
    ramBytes: 2 * GIB,
    rootfsBytes: 12 * GIB,
    unprivileged: true,
    nodeHostname: 'node-a.local',
    ip: '10.0.0.31',
    createdAt: '2026-03-12T09:20:00Z',
  },
  {
    id: 'lxc-web-02',
    name: 'web-02',
    status: 'running',
    template: 'ubuntu-24.04',
    os: 'Ubuntu',
    osVersion: '24.04',
    cores: 2,
    ramBytes: 2 * GIB,
    rootfsBytes: 12 * GIB,
    unprivileged: true,
    nodeHostname: 'node-b.local',
    ip: '10.0.0.32',
    createdAt: '2026-03-12T09:22:00Z',
  },
  {
    id: 'lxc-cache-01',
    name: 'cache-01',
    status: 'running',
    template: 'alpine-3.20',
    os: 'Alpine',
    osVersion: '3.20',
    cores: 1,
    ramBytes: Math.round(0.5 * GIB),
    rootfsBytes: 4 * GIB,
    unprivileged: true,
    nodeHostname: 'node-a.local',
    ip: '10.0.0.33',
    createdAt: '2026-04-02T14:05:00Z',
  },
  {
    id: 'lxc-build-01',
    name: 'build-01',
    status: 'stopped',
    template: 'debian-12',
    os: 'Debian',
    osVersion: '12',
    cores: 4,
    ramBytes: 4 * GIB,
    rootfsBytes: 20 * GIB,
    unprivileged: false,
    nodeHostname: 'node-b.local',
    ip: '10.0.0.34',
    createdAt: '2026-05-18T11:40:00Z',
  },
  {
    id: 'lxc-legacy-01',
    name: 'legacy-01',
    status: 'paused',
    template: 'debian-12',
    os: 'Debian',
    osVersion: '12',
    cores: 1,
    ramBytes: 1 * GIB,
    rootfsBytes: 8 * GIB,
    unprivileged: false,
    nodeHostname: 'node-a.local',
    ip: '10.0.0.35',
    createdAt: '2026-02-27T08:15:00Z',
  },
  {
    id: 'lxc-metrics-01',
    name: 'metrics-01',
    status: 'error',
    template: 'ubuntu-24.04',
    os: 'Ubuntu',
    osVersion: '24.04',
    cores: 2,
    ramBytes: 1 * GIB,
    rootfsBytes: 10 * GIB,
    unprivileged: true,
    nodeHostname: 'node-b.local',
    ip: '10.0.0.36',
    createdAt: '2026-06-30T17:50:00Z',
  },
];

export function listLxc(): LxcContainer[] {
  return CONTAINERS;
}

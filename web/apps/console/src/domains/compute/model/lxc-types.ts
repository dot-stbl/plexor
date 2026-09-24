import type { StatusVariant } from '@/shared/ui/primitives/status-pill';

/** Lifecycle state of an LXC system container on its node. */
export type LxcStatus = 'running' | 'stopped' | 'paused' | 'error';

/**
 * LXC container — a lightweight system container sharing the host kernel.
 * Self-hosted: resources are exact binary sizes (bytes), pinned to a node.
 * `template` is the base rootfs (`ubuntu-24.04`); `os`/`osVersion` are its
 * human-readable split.
 */
export interface LxcContainer {
  id: string;
  name: string;
  status: LxcStatus;
  /** Base template the rootfs was created from, e.g. `ubuntu-24.04`. */
  template: string;
  /** Human-readable OS, e.g. `Ubuntu`. */
  os: string;
  osVersion: string;
  cores: number;
  ramBytes: number;
  rootfsBytes: number;
  /** Unprivileged (uid-mapped, safer) vs privileged container. */
  unprivileged: boolean;
  nodeHostname: string;
  ip: string;
  createdAt: string;
}

export function mapLxcStatusToVariant(status: LxcStatus): StatusVariant {
  switch (status) {
    case 'running':
      return 'running';
    case 'stopped':
      return 'idle';
    case 'paused':
      return 'warn';
    case 'error':
      return 'err';
  }
}

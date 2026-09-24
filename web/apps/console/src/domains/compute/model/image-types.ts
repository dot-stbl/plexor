import type { StatusVariant } from '@/shared/ui/primitives/status-pill';

/** Публичный (shipped Plexor / зеркала дистрибутивов) или приватный (свой). */
export type ImageVisibility = 'public' | 'private';

/** Готов к использованию / собирается / ошибка сборки. */
export type ImageStatus = 'ready' | 'creating' | 'error';

export type ImageArch = 'x86_64' | 'arm64';

/**
 * OS-образ (диск-шаблон) для создания ВМ — эталон YC «Образы». Self-hosted:
 * размеры в байтах (бинарные), точные, не круглые. `techSlug` → цветной
 * бренд-логотип дистрибутива в таблице; для своих образов — fallback-иконка.
 */
export interface OsImage {
  id: string;
  name: string;
  /** Семейство: ubuntu / debian / rocky / almalinux / custom. */
  family: string;
  /** Человекочитаемая ОС: «Ubuntu», «Debian», «Своя сборка». */
  os: string;
  version: string;
  arch: ImageArch;
  /** Размер образа в байтах. */
  sizeBytes: number;
  /** Минимальный размер диска ВМ под этот образ, байты. */
  minDiskBytes: number;
  visibility: ImageVisibility;
  status: ImageStatus;
  createdAt: string;
  /** Бренд-slug для TechIcon (дистрибутивы). Нет → fallback-иконка. */
  techSlug?: string;
  description: string;
}

export function mapImageStatusToVariant(status: ImageStatus): StatusVariant {
  switch (status) {
    case 'ready':
      return 'ok';
    case 'creating':
      return 'info';
    case 'error':
      return 'err';
  }
}

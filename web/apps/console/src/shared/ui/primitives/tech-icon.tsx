import type { ComponentType } from 'react';
import { Icon as Iconify } from '@iconify/react';
import { LOGO_DATA } from '@/shared/ui/tech-icon-data';
import { Database } from '@/shared/ui/icon';
import { cn } from '@/lib/utils';

/**
 * TechIcon — цветные бренд/тех-логотипы (Iconify `logos:`, инлайн-данные из
 * `tech-icon-data.ts`). Гибрид (patterns.md): цветной лого в героических местах
 * (launcher, каталог, empty). Рендер через `@iconify/react` — офлайн, без сети.
 *
 * Нет цветного лого (`clickhouse`/`garnet`/`minio`/`ceph`) → `fallback`
 * (Material generic по умолчанию, либо переданная иконка по kind).
 */
type IconLike = ComponentType<{ className?: string }>;

export interface TechIconProps {
  /** Технологический slug: `postgres`, `redis`, `kafka`, `ubuntu`, `docker`, … */
  slug: string;
  className?: string;
  /** Иконка для slug'ов без цветного лого. По умолчанию — Material `Database`. */
  fallback?: IconLike;
}

export function TechIcon({ slug, className, fallback: Fallback = Database }: TechIconProps) {
  const data = LOGO_DATA[slug.toLowerCase()];
  if (data) {
    return <Iconify icon={data} className={cn('size-4', className)} />;
  }
  return <Fallback className={cn('size-4', className)} />;
}

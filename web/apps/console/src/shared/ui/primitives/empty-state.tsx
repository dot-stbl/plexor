import type { ComponentProps, ReactNode } from 'react';
import { cn } from '@/lib/utils';
import type { Icon } from '@nine-thirty-five/material-symbols-react';

/**
 * EmptyState — YC-подобный онбординг для пустого раздела/таблицы: иллюстрация
 * слева (бренд-марка или Material-иконка в рамке), справа — заголовок, текст,
 * ссылки на документацию и CTA. На узком экране складывается в колонку.
 *
 * Managed-сервисы и ресурсы — сложные, поэтому пустой экран объясняет, что это
 * и с чего начать, а не просто говорит «пусто». `compact` — уменьшенный вариант
 * для инлайновой пустой таблицы.
 *
 * @example
 *   <EmptyState
 *     icon={HardDrives}
 *     title="No virtual machines yet"
 *     description="Create your first VM — Plexor brings it up on the chosen runtime."
 *     docs={[{ href: 'https://plexor.dev/docs/vm', label: 'How VMs work' }]}
 *     action={<Button>Create VM</Button>}
 *   />
 */
export interface EmptyStateDoc {
  href: string;
  label: ReactNode;
}

export interface EmptyStateProps extends Omit<ComponentProps<'div'>, 'title'> {
  /** Кастомная иллюстрация слева (например `<TechIcon/>`). Приоритетнее `icon`. */
  media?: ReactNode;
  /** Fallback-иллюстрация: Material-иконка в стандартной рамке. */
  icon?: Icon;
  title: ReactNode;
  /** Лид-текст. Строка → один `<p>`; узел рисуется как есть (несколько абзацев). */
  description?: ReactNode;
  /** Доп. контент между описанием и ссылками. */
  children?: ReactNode;
  /** Ссылки на документацию — список с подчёркиванием под `docsLabel`. */
  docs?: EmptyStateDoc[];
  docsLabel?: ReactNode;
  /** Основной CTA (кнопка/ссылка). */
  action?: ReactNode;
  /** Компактная рамка иконки — для инлайновой пустой таблицы. */
  compact?: boolean;
}

export function EmptyState({
  media,
  icon: IconCmp,
  title,
  description,
  children,
  docs,
  docsLabel,
  action,
  compact = false,
  className,
  ...props
}: EmptyStateProps) {
  return (
    <div
      data-slot="empty-state"
      className={cn(
        'flex flex-col items-center gap-8 py-8 md:flex-row md:items-start md:justify-center md:gap-14',
        compact ? 'md:py-8' : 'md:py-14',
        className,
      )}
      {...props}
    >
      <div
        className={cn(
          // Plexor branded frame: soft muted fill + inset ring (no hard stock
          // border box), a faint dotted grid backdrop, icon in muted ink.
          'relative flex shrink-0 items-center justify-center overflow-hidden rounded-2xl bg-muted/40 text-muted-foreground shadow-sm ring-1 ring-inset ring-border/60',
          compact ? 'size-24' : 'size-40',
        )}
      >
        <span
          aria-hidden
          className="pointer-events-none absolute inset-0 opacity-40 [background-image:radial-gradient(var(--border)_1px,transparent_1px)] [background-size:10px_10px]"
        />
        <span className="relative">
          {media ?? (IconCmp ? <IconCmp className={compact ? 'size-11' : 'size-16'} /> : null)}
        </span>
      </div>

      <div className="max-w-md space-y-4">
        <h2 className={cn('font-semibold text-foreground', compact ? 'text-base' : 'text-lg')}>{title}</h2>
        {typeof description === 'string' ? (
          <p className="text-sm text-muted-foreground">{description}</p>
        ) : (
          description
        )}
        {children}
        {docs && docs.length > 0 && (
          <div className="space-y-1.5">
            {docsLabel && <p className="text-sm text-muted-foreground">{docsLabel}</p>}
            <ul className="space-y-1">
              {docs.map((doc) => (
                <li key={doc.href}>
                  <a
                    href={doc.href}
                    target="_blank"
                    rel="noreferrer"
                    className="text-sm text-primary underline underline-offset-4 hover:opacity-80"
                  >
                    {doc.label}
                  </a>
                </li>
              ))}
            </ul>
          </div>
        )}
        {action}
      </div>
    </div>
  );
}

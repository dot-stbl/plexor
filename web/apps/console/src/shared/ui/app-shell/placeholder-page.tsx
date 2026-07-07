import type { Icon } from '@phosphor-icons/react';

/**
 * Honest placeholder for a route whose real content is not built yet.
 * No invented data — a titled header + a dashed empty-state panel.
 */
export function PlaceholderPage({
  eyebrow,
  title,
  description,
  icon: PageIcon,
}: {
  eyebrow: string;
  title: string;
  description: string;
  icon: Icon;
}) {
  return (
    <main className="mx-auto w-full max-w-6xl p-6 lg:p-8">
      <header className="space-y-1.5 border-b border-border pb-5">
        <div className="font-mono text-[11px] uppercase tracking-[0.08em] text-muted-foreground">
          {eyebrow}
        </div>
        <h1 className="text-xl font-semibold tracking-tight">{title}</h1>
        <p className="max-w-prose text-sm text-muted-foreground">{description}</p>
      </header>

      <div className="mt-8 flex flex-col items-center justify-center gap-3 rounded-lg border border-dashed border-border py-16 text-center">
        <div className="flex size-11 items-center justify-center rounded-md border border-border bg-muted/40 text-muted-foreground">
          <PageIcon className="size-5" />
        </div>
        <div className="space-y-1">
          <p className="text-sm font-medium">Экран в разработке</p>
          <p className="text-xs text-muted-foreground">
            Каркас готов — контент появится в следующем проходе.
          </p>
        </div>
      </div>
    </main>
  );
}

import { Link } from '@tanstack/react-router';

/**
 * Bottom-of-landing CTA strip — the only purpose is to keep the docs
 * link visible after the manifesto + roadmap push it offscreen. The
 * strip mirrors the hero's primary CTA so the user has the same exit
 * path at both ends of the page.
 */
export function MarketingCta() {
  return (
    <section className="border-b border-border">
      <div className="mx-auto max-w-7xl px-6 py-16 md:py-20">
        <div className="flex flex-col items-start gap-4 rounded-xl border border-border bg-card p-8 md:flex-row md:items-center md:justify-between md:gap-6 md:p-10">
          <div className="max-w-xl">
            <p className="mb-2 font-mono text-[11px] font-medium uppercase tracking-[0.16em] text-muted-2">
              Дальше
            </p>
            <h2 className="text-2xl font-semibold tracking-tight text-foreground">
              Документация →{' '}
              <span className="text-muted-foreground">
                что внутри бинарника, и как с этим работать
              </span>
            </h2>
          </div>
          <Link
            to="/docs/getting-started"
            className="inline-flex h-10 shrink-0 items-center justify-center rounded-md bg-primary px-5 text-sm font-medium text-primary-foreground transition-colors duration-fast ease-out hover:bg-primary/90"
          >
            Открыть документацию
          </Link>
        </div>
      </div>
    </section>
  );
}
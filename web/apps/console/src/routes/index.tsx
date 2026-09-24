import { createFileRoute, Link, redirect } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { PageTemplate, SECTIONS, sectionPrimaryRoute } from '@/shared/ui/app-shell';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import { routeHead } from '@/shared/lib/route-head';
import { hasValidSession } from '@/shared/lib/session';

export const Route = createFileRoute('/')({
  beforeLoad: () => {
    if (!hasValidSession()) {
      throw redirect({ to: '/login' });
    }
  },
  component: HomePage,
  ...routeHead(null),
});

const cardBase =
  'flex items-start gap-3 rounded-lg border border-border bg-card p-4 shadow-sm transition-all duration-150 ease-out';

export function HomePage() {
  const { t } = useTranslation();
  return (
    <PageTemplate
      title={t('home.title')}
      width="wide"
      data-od-id="home"
      description={t('home.description')}
    >
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {SECTIONS.map((section, index) => {
          const SectionIcon = section.icon;
          const to = sectionPrimaryRoute(section);
          const inner = (
            <>
              <div className="flex size-10 shrink-0 items-center justify-center rounded-md bg-muted text-foreground">
                <SectionIcon className="size-5" />
              </div>
              <div className="min-w-0 space-y-0.5">
                <div className="flex items-center gap-1.5">
                  <span className="text-sm font-medium text-foreground">{t(section.label)}</span>
                  {!to && (
                    <StatusPill variant="idle" hideDot className="px-1.5 py-0 text-[9.5px] font-normal">
                      {t('common.soon')}
                    </StatusPill>
                  )}
                </div>
                <p className="text-xs text-muted-foreground">{t(section.caption)}</p>
              </div>
            </>
          );
          // Stagger the entrance so cards fade in from the top in sequence;
          // cap at 8 to keep the longest delay under ~350ms even with many cards.
          const delayMs = Math.min(index, 8) * 40;
          const motion = `animate-in fade-in slide-in-from-top-2 fill-mode-both duration-200 [animation-delay:${delayMs}ms]`;
          if (to) {
            return (
              <Link
                key={section.id}
                to={to}
                data-od-id={`home-card-${section.id}`}
                className={`${cardBase} ${motion} hover:-translate-y-px hover:border-foreground/20 hover:shadow-md`}
              >
                {inner}
              </Link>
            );
          }
          return (
            <div key={section.id} data-od-id={`home-card-${section.id}`} className={`${cardBase} ${motion} opacity-60`}>
              {inner}
            </div>
          );
        })}
      </div>
    </PageTemplate>
  );
}

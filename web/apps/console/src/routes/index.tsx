import { createFileRoute, Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { PageTemplate, SECTIONS, sectionPrimaryRoute } from '@/shared/ui/app-shell';
import { StatusPill } from '@/shared/ui/primitives/status-pill';

export const Route = createFileRoute('/')({
  component: HomePage,
});

const cardBase =
  'flex items-start gap-3 rounded-lg border border-border bg-card p-4 shadow-sm transition-all';

function HomePage() {
  const { t } = useTranslation();
  return (
    <PageTemplate
      title={t('home.title')}
      width="full"
      data-od-id="home"
      description={t('home.description')}
    >
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {SECTIONS.map((section) => {
          const SectionIcon = section.icon;
          const to = sectionPrimaryRoute(section);
          const inner = (
            <>
              <div className="flex size-10 shrink-0 items-center justify-center rounded-md bg-muted text-foreground">
                <SectionIcon className="size-5" />
              </div>
              <div className="min-w-0 space-y-0.5">
                <div className="flex items-center gap-1.5">
                  <span className="text-sm font-medium text-foreground">{section.label}</span>
                  {!to && (
                    <StatusPill variant="idle" hideDot className="px-1.5 py-0 text-[9.5px] font-normal">
                      {t('common.soon')}
                    </StatusPill>
                  )}
                </div>
                <p className="text-xs text-muted-foreground">{section.caption}</p>
              </div>
            </>
          );
          if (to) {
            return (
              <Link
                key={section.id}
                to={to}
                data-od-id={`home-card-${section.id}`}
                className={`${cardBase} hover:-translate-y-px hover:border-foreground/20 hover:shadow-md`}
              >
                {inner}
              </Link>
            );
          }
          return (
            <div key={section.id} data-od-id={`home-card-${section.id}`} className={`${cardBase} opacity-60`}>
              {inner}
            </div>
          );
        })}
      </div>
    </PageTemplate>
  );
}

import { createFileRoute, Link } from '@tanstack/react-router';
import { PageHeader, navItems } from '@/shared/ui/app-shell';

export const Route = createFileRoute('/')({
  component: HomePage,
});

function HomePage() {
  return (
    <main data-od-id="home">
      <PageHeader
        breadcrumb={['Plexor']}
        title="Обзор"
        description="Быстрый доступ к разделам проекта. Полный каталог сервисов — в «Центре управления» (кнопка с сеткой в левом рейле)."
      />

      <div className="mx-auto w-full max-w-6xl px-6 py-6 lg:px-8">
        <section className="space-y-3" data-od-id="home-sections">
          <h2 className="text-[11px] font-medium tracking-[0.06em] text-muted-foreground uppercase">
            Разделы
          </h2>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {navItems.map((item) => {
              const ItemIcon = item.icon;
              return (
                <Link
                  key={item.to}
                  to={item.to}
                  data-od-id={`home-card-${item.to.slice(1)}`}
                  className="group flex items-start gap-3 rounded-lg border border-border bg-card p-4 shadow-sm transition-all hover:-translate-y-px hover:border-foreground/20 hover:shadow-md"
                >
                  <div className="flex size-10 shrink-0 items-center justify-center rounded-md bg-muted text-foreground">
                    <ItemIcon className="size-5" />
                  </div>
                  <div className="min-w-0 space-y-0.5">
                    <div className="text-sm font-medium text-foreground">{item.title}</div>
                    <p className="text-xs text-muted-foreground">{item.description}</p>
                  </div>
                </Link>
              );
            })}
          </div>
        </section>
      </div>
    </main>
  );
}

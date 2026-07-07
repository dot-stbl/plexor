import { createFileRoute } from '@tanstack/react-router';
import { Link } from '@tanstack/react-router';

export const Route = createFileRoute('/')({
  component: HomePage,
});

function HomePage() {
  return (
    <main className="mx-auto max-w-3xl space-y-6 p-8">
      <header className="space-y-2 border-b border-border pb-6">
        <div className="text-[11px] uppercase tracking-[0.06em] text-muted-foreground font-medium">Plexor Portal · welcome</div>
        <h1 className="text-2xl font-semibold tracking-tight">Plexor Portal</h1>
        <p className="text-muted-foreground text-sm">
          Frontend scaffold ready. Plexor DS tokens applied via shadcn-ui defaults.
        </p>
        <nav className="flex gap-2 pt-2 text-sm">
          <Link to="/components" className="underline">
            /components
          </Link>
        </nav>
      </header>
    </main>
  );
}

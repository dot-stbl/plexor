import { createFileRoute } from '@tanstack/react-router';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { Button } from '@/shared/ui/primitives/button';

export const Route = createFileRoute('/')({
  component: HomePage,
});

function HomePage() {
  return (
    <main className="container mx-auto max-w-3xl space-y-6 p-8">
      <header>
        <h1 className="text-2xl font-semibold tracking-tight">Plexor Portal</h1>
        <p className="text-muted-foreground mt-1 text-sm">
          Self-hosted cloud platform control plane.
        </p>
      </header>

      <Card>
        <CardHeader>
          <CardTitle>Welcome</CardTitle>
          <CardDescription>
            Vite + React + TanStack Router + TanStack Query + shadcn-ui (Base UI) + Plexor DS.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex gap-2">
          <Button>Primary action</Button>
          <Button variant="outline">Secondary</Button>
          <Button variant="ghost">Ghost</Button>
        </CardContent>
      </Card>
    </main>
  );
}
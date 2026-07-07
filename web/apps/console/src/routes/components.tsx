import { createFileRoute, Link } from '@tanstack/react-router';
import { Button } from '@/shared/ui/primitives/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/primitives/card';
import { Input } from '@/shared/ui/primitives/input';
import { Label } from '@/shared/ui/primitives/label';

export const Route = createFileRoute('/components')({
  component: ComponentsPage,
});

function ComponentsPage() {
  return (
    <main className="mx-auto max-w-3xl space-y-6 p-8">
      <header className="space-y-2 border-b border-border pb-6">
        <div className="text-[11px] uppercase tracking-[0.06em] text-muted-foreground font-medium">Plexor Portal · components</div>
        <h1 className="text-2xl font-semibold tracking-tight">Components</h1>
        <p className="text-muted-foreground text-sm">
          Minimal scaffold working. shadcn-ui + Plexor DS tokens active.
        </p>
        <Link to="/" className="inline-block text-sm underline">
          ← back to home
        </Link>
      </header>

      <Card>
        <CardHeader>
          <CardTitle>shadcn Button (Plexor DS adapted)</CardTitle>
          <CardDescription>Variants and sizes from Plexor DS</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="flex flex-wrap items-center gap-2">
            <Button>Default</Button>
            <Button variant="outline">Outline</Button>
            <Button variant="secondary">Secondary</Button>
            <Button variant="ghost">Ghost</Button>
            <Button variant="destructive">Destructive</Button>
            <Button variant="link">Link</Button>
            <Button disabled>Disabled</Button>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button size="sm">sm</Button>
            <Button>md (default)</Button>
            <Button size="lg">lg</Button>
            <Button size="icon" aria-label="settings">⚙</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Plexor DS tokens via Tailwind</CardTitle>
          <CardDescription>
            bg-muted, text-muted-foreground, border-border-2, etc.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="bg-muted text-muted-foreground rounded-md p-3 text-sm">
            Surface-3 (muted) — used for sticky headers, selected, tags
          </div>
          <div className="bg-card border-border text-card-foreground rounded-md border p-3 text-sm">
            Card surface — main content
          </div>
          <p className="text-muted-foreground text-xs">
            These classes come from <code className="font-mono">@theme inline</code> mapping
            Plexor DS tokens to Tailwind utilities.
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Form (shadcn + Plexor DS)</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="space-y-1.5">
            <Label htmlFor="demo-input">Tenant name</Label>
            <Input id="demo-input" placeholder="acme-prod" />
          </div>
          <Button>Create</Button>
        </CardContent>
      </Card>
    </main>
  );
}

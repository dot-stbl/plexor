# Recipe: rules cheat sheet

Condensed version of `.agents/rules/web-frontend.md` (433 lines). Canonical
short version — when something isn't covered here, read the full rulebook.

## Components & inputs

```tsx
// ❌ Hand-rolled control / raw select
<div className="border rounded px-2" onClick={...}>Click me</div>
<select><option>a</option></select>
// ✅ Real primitives
<Button variant="outline" onClick={...}>Click me</Button>
<SimpleSelect options={['a', 'b']} value={v} onChange={setV} />
```

## Icons & colors

```tsx
// ❌ Wrong icon package / local shim, raw color
import { Plus } from 'lucide-react';
<span className="text-red-600 bg-[#fee]">Failed</span>
// ✅ Only sanctioned icon source, token utility (add token to index.css if missing)
import { Add } from '@nine-thirty-five/material-symbols-react/rounded/700';
<span className="text-err-ink bg-err-soft">Failed</span>
```

## Package manager

```bash
# ❌                    # ✅
npm install              bun install
pnpm add foo             bun add foo
npx shadcn add foo       bunx --bun shadcn@latest add foo
```

## i18n

```tsx
// ❌ hardcoded string                // ❌ const columns (t() not ready at module load)
<h1>Networks</h1>                     const columns = [{ header: 'Name' }];
// ✅ key in en+ru common.json        // ✅ function, called with t inside the component
<h1>{t('networks.title')}</h1>        export function getNetworkColumns(t: TFunction) { ... }
```

## Page scaffold, titles & tables

```tsx
// ❌ hand-rolled header div, document.title = 'Plexor'
// ✅ static title in route config: ...routeHead('Networks'),
// ✅ data-dependent title in the component: useDocumentTitle(vm?.name ?? null);
<PageTemplate title={title} description={desc} actions={<Button>...</Button>}>
  {children}
</PageTemplate>
<DataTable columns={columns} data={rows} density="compact" />
```

## Empty states & icon-only buttons

```tsx
// ❌ bare sentence, dead end            // ❌ no accessible name
{items.length === 0 && <p>No items.</p>}  <Button size="icon"><Settings /></Button>
// ✅ EmptyState with a CTA              // ✅ labeled
<EmptyState icon={Icon} title={t('x.empty.title')}
  action={<Button render={<Link to="/x/new" />}><Add />{t('x.empty.cta')}</Button>} />
<Button size="icon" aria-label="Settings"><Settings /></Button>
```

## Collections & sizes

```tsx
// ❌ joined string / count sentence / manual unit
<p>{tags.join(', ')}</p>
<MonoNum>{vm.ramGb}</MonoNum> GB
// ✅ one Badge per item, overflow → +N          ✅ auto-scaling unit
<div className="flex gap-1">{tags.map(t => <Badge key={t}>{t}</Badge>)}</div>
<Size bytes={vm.ramBytes} />
```

## Domain folders & forms

```
src/domains/network/
  index.ts                 ← barrel, the ONLY thing routes import from
  ui/network-columns.tsx   ← getNetworkColumns(t)
  ui/networks-empty.tsx    ← NetworksEmpty
  api/use-networks.ts      ← useNetworks()
```

```tsx
// ❌ raw div layout                    // ✅ Field primitives
<div className="space-y-4">              <Field data-invalid={hasError}>
  <label>Email</label>                     <FieldLabel htmlFor="email">Email</FieldLabel>
  <input />                                <Input id="email" aria-invalid={hasError} />
</div>                                   </Field>
```

## Full rulebook

`.agents/rules/web-frontend.md` for anything not covered above (Tailwind v4,
overlay/Select pitfalls, forms in depth). `src/shared/ui/INDEX.md` is the
component decision table — check it before building any UI.

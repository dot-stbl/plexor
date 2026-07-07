# Plexor Brand

## Positioning

**Plexor** — self-hosted cloud platform для команд, которые хотят AWS-like
ergonomics без vendor lock-in.

| Аудитория | Промт |
|-----------|-------|
| DevOps / Platform engineers | "Весь cloud у тебя дома, без лицензий AWS и сюрпризов в биллинге" |
| Engineering managers | "Контроль над затратами и VM provisioning для команды" |
| Open-source enthusiasts | "Open-source альтернатива YC/AWS, которую можно хостить" |

**Не** пытаемся быть:
- Enterprise-y (без длинных текстов про compliance certs — это опциональные аддоны)
- Marketing-y (без "revolutionize your workflow")
- Toy-like (без mascot и emoji-as-design)

## Voice

Спокойный, точный, технический. Hetzner-уровень без хвастовства.

### Tone examples

✅ **Хорошо:**
- "Provision VM in 30 seconds"
- "Cluster state synced via NATS"
- "Failed to delete VPC: 2 networks still attached"

❌ **Плохо:**
- "🚀 Launch blazingly fast VMs!"
- "Revolutionize your cloud experience"
- "Oopsie! Something went wrong 😱"

### Microcopy guidelines

- **Errors**: технические детали + что делать. Не извиняйся.
- **Buttons**: imperative verbs ("Create VM", not "Create a VM" or "Creation")
- **Tooltips**: 1 строка, ≤ 80 символов
- **Empty states**: краткое объяснение + 1 CTA

## Logo

**Concept:** строчная **p** с шестью точками вокруг — mesh провайдеров вокруг ядра (Plexor).

```
       ●           ●
            ●
   ●      p      ●
            ●
       ●           ●
```

- **Wordmark**: "plexor" (lowercase) рядом с monogram
- **Monogram**: только "p" с точками
- **Min size**: 24px для monogram, 96px для wordmark
- **Clear space**: ≥ высоты буквы "p" со всех сторон
- **Forbidden**: вращать, искажать цвета, отзеркаливать, ставить на busy backgrounds

## Colors

### Brand palette

| Token | Hex | Использование |
|-------|-----|---------------|
| `brand-primary` | `#5E5BE8` | primary actions, links |
| `brand-primary-hover` | `#4A47D6` | primary hover state |
| `brand-primary-soft` | `#EEEEFE` | backgrounds, badges |
| `brand-accent` | `#22D3EE` | callouts, highlights |
| `brand-deep` | `#1B1B3A` | backgrounds (dark mode) |
| `brand-light` | `#F8F9FC` | backgrounds (light mode) |

### Semantic palette

| Token | Hex | Использование |
|-------|-----|---------------|
| `success` | `#22C55E` | success states, running VMs |
| `warning` | `#F59E0B` | warnings, pending states |
| `danger` | `#EF4444` | errors, destructive actions, stopped |
| `info` | `#3B82F6` | informational badges |
| `neutral` | scale 0-950 | text, borders |

### Dark / Light mode

Обе темы обязательны. CSS variables в `tokens.css`:

```css
:root[data-theme="light"] {
  --bg-primary: var(--brand-light);
  --bg-elevated: #FFFFFF;
  --text-primary: var(--neutral-900);
  /* ... */
}

:root[data-theme="dark"] {
  --bg-primary: var(--brand-deep);
  --bg-elevated: var(--neutral-900);
  --text-primary: var(--neutral-50);
  /* ... */
}
```

## Typography

| Token | Font | Use |
|-------|------|-----|
| `font-sans` | Inter (400, 500, 600, 700) | UI default |
| `font-mono` | JetBrains Mono | code, IDs, IP addresses |
| `font-display` | Inter Tight (700) | only logo + huge numbers |

### Type scale

| Size | Token | Line-height | Use |
|------|-------|-------------|-----|
| 12px | `text-xs` | 1.4 | microcopy, badges |
| 14px | `text-sm` | 1.5 | default body |
| 16px | `text-base` | 1.5 | body emphasis |
| 18px | `text-lg` | 1.4 | section titles |
| 24px | `text-xl` | 1.3 | page titles |
| 32px | `text-2xl` | 1.2 | dashboard metrics |
| 48px | `text-3xl` | 1.1 | hero / empty states |

## Spacing

4px grid. Используем `space-1` через `space-16`:
- `space-1` = 4px
- `space-2` = 8px
- `space-4` = 16px
- `space-8` = 32px
- `space-16` = 64px

## Radii

- `rounded-sm` = 4px (badges, tags)
- `rounded-md` = 6px (buttons, inputs)
- `rounded-lg` = 12px (cards, dialogs)
- `rounded-full` = 9999px (avatars, status dots)

## Shadows

| Token | Use |
|-------|-----|
| `shadow-sm` | default elevated elements |
| `shadow-md` | dropdowns, popovers |
| `shadow-lg` | modals |
| `shadow-xl` | rare, only for top-level modals |

## Iconography

- **Library**: Lucide Icons (open source, MIT)
- **Default size**: 16px (inline), 20px (default), 24px (feature)
- **Stroke**: 1.5px default, 2px on hover for emphasis

## Motion

| Token | Duration | Easing | Use |
|-------|----------|--------|-----|
| `transition-fast` | 100ms | ease-out | hover, focus states |
| `transition-base` | 200ms | ease-in-out | most animations |
| `transition-slow` | 300ms | ease-in-out | page transitions |
| `transition-slower` | 500ms | ease-in-out | complex state changes |

## Accessibility

- WCAG AA minimum
- Контраст ≥ 4.5:1 для текста
- ≥ 3:1 для non-text elements
- Все интерактивные элементы должны иметь focus-ring
- Движение: respect `prefers-reduced-motion`

## Open tokens

Все эти токены публикуются как **JSON** для OpenDesign и других
дизайн-тулзов. Файл: `tokens.json` (генерируется `scripts/transform-tokens.mjs`).
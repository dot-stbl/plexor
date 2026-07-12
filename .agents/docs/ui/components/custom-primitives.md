# Custom primitives — Plexor DS

Это **не-shadcn** компоненты, которые мы добавили в `src/shared/ui/primitives/`.
Они общие, переиспользуемые, несут свою семантику (а не только стиль)
и следуют тем же правилам что и shadcn-обёртки (cva, cn, data-slot).

---

## Size — bytes → human-readable

**Файл:** `src/shared/ui/primitives/size.tsx`

Plexor API возвращает RAM, disk, image size как **raw bytes** (или KB
для legacy endpoints). Размер в гигабайтах в данных API не хранится
— это вывод, а не input. Компонент Size делает unit math, чтобы
экраны передавали только `bytes={...}` без ручной конвертации.

### API

```tsx
<Size bytes={vm.diskBytes} />                        // 80.0 GiB
<Size bytes={vm.diskBytes} decimals={2} />            // 80.00 GiB
<Size bytes={512} decimals={0} />                     // 512 B
<Size bytes={512} showUnit={false} />                 // 512 (unit скрыт)
<Size bytes={4 * 1024 ** 3} unit="GiB" />            // 4.0 GiB (forced unit)
<Size bytes={vm.diskBytes} muted />                   // muted text
```

### Props

| Prop | Type | Default | Описание |
|---|---|---|---|
| `bytes` | `number` | required | Размер в байтах. |
| `decimals` | `number` | `1` | Значаков после запятой. `0` → "80 GB", `1` → "80.0 GB", `2` → "80.00 GB". |
| `unit` | `'B' \| 'KiB' \| 'MiB' \| 'GiB' \| 'TiB' \| 'PiB'` | auto | Force a specific unit. Используй когда контекст фиксированный (например, "все RAM в GiB"). |
| `showUnit` | `boolean` | `true` | Показать суффикс unit'а. Скрывай когда родитель уже показывает unit в заголовке. |
| `muted` | `boolean` | `false` | `text-muted-foreground` (для второстепенных значений). |

### Поведение

**Auto-pick unit.** Компонент выбирает наибольший unit, в котором
value >= 1. Это даёт читаемые числа в любом диапазоне:

| Bytes | Auto-pick | Render |
|---|---|---|
| 0 | `B` | `0 B` |
| 512 | `B` | `512 B` |
| 1024 | `KiB` | `1.0 KiB` |
| 1536 | `KiB` | `1.5 KiB` |
| 1 048 576 | `MiB` | `1.0 MiB` |
| 1 073 741 824 | `GiB` | `1.0 GiB` |
| 1 099 511 627 776 | `TiB` | `1.0 TiB` |

**Binary (1024), не SI (1000).** RAM и disk в self-hosted Plexor
выделяются в бинарных единицах (1 GiB = 1024³ bytes). Суффикс "iB"
делает это явным. Если понадобятся SI-единицы (KB, MB, GB, TB) — это
другая пропа, другой primitive.

**Whole numbers без trailing zero.** `4.0 GiB` → `4 GiB` если
значение целое. Удобно для дисков с круглыми размерами.

**Sub-1 values получают extra decimal.** 0.5 GiB → не округляется
до 0 GiB.

### Visual

- `font-mono` + `tabular-nums` — числа выровнены по столбцам в
  таблицах, не дёргаются когда цифра меняет ширину
- Unit suffix в `text-[0.85em] text-muted-foreground` — вторичный
  визуально, не конкурирует с самим значением
- `leading-none` + `align-baseline` — стабильно сидит в строке текста
- `data-slot="size"` + `data-unit="GiB"` — для e2e селекторов и
  тестов, не для стилизации

### SizeUtils — converters

```tsx
import { Size, SizeUtils } from '@/shared/ui/primitives/size';

SizeUtils.gibToBytes(8)   // 8 * 1024**3  = 8 589 934 592
SizeUtils.gbToBytes(8)    // 8 * 10**9     = 8 000 000 000
```

Legacy mock-данные в GiB/GiB-эквиваленте: конвертируй inline в `bytes`.
Не пиши `<MonoNum>{x} GiB</MonoNum>` — это нарушает правило, размер
застрянет в одной единице.

### Где использовать

✅ Используй `Size`:

- ClusterNodeRow — RAM/Disk ноды
- ClusterCard — capacity bars
- VM Table — колонка Disk
- VmCreatePage — capacity preview в Node context
- Любой другой capacity / metric / image size

❌ НЕ используй `Size` для:

- Счётчики (count, vmCount, nodeCount) — это `<MonoNum>{n}</MonoNum>` без unit'а
- Проценты — это `<Progress value={pct} />` или MonoNum с `sign="%"` если нужно явно
- Валюты — `MonoNum` с `prepend="$"`, Size только для байтов
- Latencies (ms, s) — отдельный primitive (когда понадобится)

### Когда НЕ писать свой форматировщик

Все размеры в Plexor идут через `Size`. Если видишь `<MonoNum>{x} GB</MonoNum>` или `<span>{x} MiB</span>` — это тех-долг. Замени на `Size bytes={x}`. Если mock в GiB, добавь `SizeUtils.gibToBytes(x)` inline — это однострочник.

### Альтернативы, которые мы НЕ используем

- **SI-десятичные (KB, MB, GB, TB)** — для self-hosted инфраструктуры
  не подходят (RAM не 1000 МБ = 1 ГБ, а 1024 МБ = 1 MiB). `Size` всегда
  binary.
- **Bit representation** (KB = 1024 bytes, но Mb = 1 000 000 bits) —
  бессмысленная путаница. `Size` использует байты.
- **Format on the data layer** (kubb-types) — нет, это display concern.
  API отдаёт `bytes: number`, фронт форматит.

### Тестовая стратегия

`Size` — pure function от `bytes` + `decimals` + `unit`. Можно
покрыть unit-тестами:

```tsx
import { render, screen } from '@testing-library/react';
import { Size } from './size';

it('formats 0 as "0 B"', () => {
  render(<Size bytes={0} />);
  expect(screen.getByText('0')).toBeInTheDocument();
  expect(screen.getByText('B')).toBeInTheDocument();
});

it('picks the largest unit where value >= 1', () => {
  render(<Size bytes={1.5 * 1024 ** 3} />);
  expect(screen.getByText('1.5')).toBeInTheDocument();
  expect(screen.getByText('GiB')).toBeInTheDocument();
});

it('omits the decimal when the value is whole', () => {
  render(<Size bytes={4 * 1024 ** 3} />);
  expect(screen.getByText('4')).toBeInTheDocument();
  expect(screen.queryByText('4.0')).not.toBeInTheDocument();
});
```

# Screen 05: Billing — Usage Dashboard

## Purpose

Главная страница billing. Andrey открывает её раз в неделю чтобы
увидеть куда уходят деньги, на что обратить внимание, нужно ли увеличить
квоты.

## User goal

Andrey: за 30 секунд увидеть месячную цифру, понять тренд, отреагировать
на аномалии.

## Entry points

- Sidebar nav "Billing" (admin only)
- Quick link from notification: "Quota almost reached"
- Direct URL: `/billing/usage`

## Layout

```
┌──────────────────────────────────────────────────────────────────────┐
│ Top bar                                                              │
├──────────┬───────────────────────────────────────────────────────────┤
│ Sidebar  │ Billing > Usage          [ This month ▼ ] [ Refresh ]  ⋯  │
│          │                                                            │
│  Compute │ ┌─────────────────────────────────────────────────────┐ │
│  Storage │ │  Current spend                                         │ │
│  Network │ │  $243.18                                              │ │
│  IAM     │ │  Forecast $389.04 by month-end                        │ │
│  Observ. │ │  ████████████████░░░░░░░░░░░  62% of $500 budget      │ │
│          │ │  [Set alert] [Adjust budget]                          │ │
│ ──────── │ └─────────────────────────────────────────────────────┘ │
│ ●Billing │                                                            │
│  Usage   │ ┌─ Spend by service ───────────┬─ Top resources ──────┐│
│  Invoice │ │ Compute   $148.20 (60%)       │ db-1   $43.20       ││
│  Quota   │ │ Storage    $42.18 (18%)       │ kube-1 $86.40       ││
│          │ │ Network    $28.80 (12%)       │ bucket-prod $24.00  ││
│ ──────── │ │ Managed DB $24.00 (10%)       │ ...                  ││
│ Settings │ └──────────────────────────────┴──────────────────────┘│
│          │                                                            │
│          │ ┌─ Cost trend (last 30 days) ─────────────────────────┐│
│          │ │ [Line chart with daily resolution]                    ││
│          │ │ Compare: this month (solid) vs last month (dashed)   ││
│          │ │                                                          ││
│          │ └─────────────────────────────────────────────────────────┘│
│          │                                                            │
│          │ ┌─ Recent invoices ────────────────────────────────────┐│
│          │ │ 2026-06  $312.50  Paid      [Download PDF] [CSV]     ││
│          │ │ 2026-05  $287.40  Paid      [Download PDF] [CSV]     ││
│          │ │ 2026-04  $245.80  Paid      [Download PDF] [CSV]     ││
│          │ └─────────────────────────────────────────────────────────┘│
└──────────┴────────────────────────────────────────────────────────────┘
```

## Content elements

### Period selector

- "This month" (default) / "Last month" / "Last 90 days"
- Custom date range
- Compare with previous period (checkbox)

### Current spend card

- Big number: $243.18 (text-3xl, mono)
- Sub: "Current spend this period"
- Progress bar: against budget
- Forecast: "On track for $389 by month-end" (linear projection)
- Actions: Set alert · Adjust budget

### Spend by service card

- Bar chart (horizontal) showing %
- Top categories:
  - Compute (60%) $148.20
  - Storage (18%) $42.18
  - Network (12%) $28.80
  - Managed DB (10%) $24.00
- Click category → drill into detail (see [#open-decision-quote])

### Top resources card (right of breakdown)

- Top 5-10 ресурсов по затратам
- Columns: Resource (link to detail), Type, Cost this month
- Click row → resource detail

### Cost trend chart

- Line chart, 30 days back, daily resolution
- Two series: current period (solid blue), previous period (dashed gray)
- Hover tooltip: "Date • Total • Delta vs prev"
- X axis: dates with weekday labels
- Y axis: $ amount, auto-scale

### Recent invoices

- DataTable
- Period (link to invoice detail), Total, Status (Paid/Pending/Failed), Actions

### Quick actions (top right ⋯)

- Export all data (CSV / JSON)
- Generate usage report (email scheduled)
- Configure payment method

## Sub-pages

### /billing/invoices

- DataTable with all historical invoices
- Filter: date range, status
- Click row → modal with line items

### /billing/quota

- Current quotas vs usage per project
- "Request increase" button → admin notification flow

## States

### Empty (first month)
- "No usage data yet — usage starts accumulating when you create resources"

### Loading
- Skeleton cards

### Forecast exceeded
- Banner: "Forecast exceeds budget. Reduce usage or increase budget."

### Failed payment
- Red banner persistent: "Last payment failed. Update payment method."

### Multiple projects
- Add project selector at top
- Per-project breakdown

## Permissions

- **Owner**: full billing access
- **Admin**: read-only on billing + can request quota increase
- **Developer**: no billing access (hide menu item)

## OpenDesign prompt

```
OpenDesign session for Plexor Portal > Billing > Usage

Critical for Andrey — must answer "where is money going" in 30 seconds.

Required elements:
- Top: Period selector + Refresh button
- Big "Current spend" card with progress bar + forecast + alerts
- Side-by-side: Spend by service (left) + Top resources (right)
- Cost trend line chart comparing this vs last period
- Recent invoices table

Variants:
- Empty (no usage yet this month)
- Loading (skeleton)
- Forecast exceeded (alert)
- Multiple projects toggle

Dark + Light.

File: billing-usage.figma

Output: 4 frames (full dashboard + 3 variants)
```

## Open design decisions

- [ ] Currency formatting: USD only for MVP, or configurable per tenant?
- [ ] Granularity: hourly / daily / monthly views?
- [ ] Forecast algorithm: linear projection vs ML-based?
- [ ] Cost optimization suggestions: separate page or inline?
- [ ] Export: PDF + CSV? Both from start?
- [ ] Multi-project breakdown: per-card or drill-down only?
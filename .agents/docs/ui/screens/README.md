# Screens — брифы для дизайнера

Каждый файл содержит полный бриф для одного экрана: purpose, layout,
content elements, states, open design decisions.

## Critical (MVP — дизайним первыми)

| # | Screen | Файл | Persona priority |
|---|--------|------|------------------|
| 01 | VM list (главная) | [01-vm-list.md](01-vm-list.md) | Dmitriy ★★★, Maria ★★★, Vasya ★★ |
| 02 | VM detail (с console) | [02-vm-detail.md](02-vm-detail.md) | Dmitriy ★★★, Maria ★★, Vasya ★★ |
| 03 | Create VM wizard | [03-create-vm-wizard.md](03-create-vm-wizard.md) | Maria ★★★, Vasya ★★ |
| 04 | Network → VPC detail | [04-network-vpc.md](04-network-vpc.md) | Dmitriy ★★★, Maria ★★ |
| 05 | Billing → Usage | [05-billing-usage.md](05-billing-usage.md) | Andrey ★★★ |
| 06 | Observability → Audit log | [06-audit-log.md](06-audit-log.md) | Dmitriy ★★, Vasya ★★, Andrey ★ |

## Important (Phase 2 — после MVP)

См. [99-future-screens.md](99-future-screens.md).

## Структура каждого брифа

```
# Screen N: <Name>

## Purpose
<1-2 sentences>

## User goal
<что пользователь пытается сделать>

## Entry points
<как сюда попадают>

## Layout
<top-level structure: header, sidebar, content, footer>

## Content elements
<конкретные виджеты: tables, forms, charts, etc.>

## States
<empty / loading / error / success>

## Interactions
<что происходит при кликах>

## Open design decisions
<что нужно решить (placeholder для твоих решений)>

## OpenDesign prompt
<готовая инструкция для OpenDesign>
```
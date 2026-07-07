# Personas

Plexor обслуживает 4 типа пользователей. Дизайн каждого экрана должен
учитывать хотя бы одну persona явно.

## Persona 1: Dmitriy — DevOps / Platform engineer

**Background**: Senior backend engineer, 8 лет опыта. Работает в
команде которая делает Kubernetes-based SaaS продукты. Отвечает за
то что dev-окружение работает быстро.

**Goals**:
- Provision dev/staging/prod environments за минуты, не дни
- Делать это скриптовано (IaC), чтобы вся команда использовала одинаковые конфиги
- Видеть observability и logs когда что-то ломается
- Не зависеть от AWS/GCP/Azure

**Behaviors**:
- 90% времени в терминале или VS Code, не в браузере
- Когда заходит в UI — это диагностика проблем или on-call срочные фиксы
- Ценит keyboard shortcuts и CLI
- Не любит marketing-copy в продукте

**Quote**: *"I want to do everything from my shell, but when something breaks at 3am I need a UI that shows me what's wrong."*

**Primary flows**:
- View VM list — фильтры по статусу и тегам
- Open console для зависшей VM
- SSH key management (редко, через CLI обычно)
- Network topology view
- Audit log investigation

**UI implications**:
- Терминал-стиль таблиц (compact, monospace для IP/ID)
- Keyboard shortcuts (`/` для поиска, `c` create, `?` help)
- Bulk operations
- JSON view в любой момент
- Log viewer как в Grafana (фильтры, time range)

## Persona 2: Maria — Backend / Application developer

**Background**: Junior-middle developer, 3 года опыта. Пишет на
Python/Node, не часто трогает инфру.

**Goals**:
- Получить Postgres + пару VM для своего проекта
- Поделиться доступом с командой
- Видеть свои VMs и не думать про железо

**Behaviors**:
- 80% времени в IDE и Slack
- Заходит в Plexor раз в день или реже
- Click-driven, не любит CLI
- Ценит понятные error messages

**Quote**: *"I just want my database and a VM. Why do I need to know about flavors?"*

**Primary flows**:
- Browse project resources (dashboard)
- Create VM через wizard
- Attach volume to VM
- Share SSH key с teammate
- View metrics и logs

**UI implications**:
- Большие buttons, чёткий wizard
- Helpful tooltips ("что такое flavor?")
- Visible progress bars
- One-action outcomes (можно посмотреть IP сразу после создания)
- Friendly empty states

## Persona 3: Andrey — Engineering manager / Team lead

**Background**: 15+ лет опыта. Управляет командой из 5-15 инженеров.
Отвечает за budget и работающие процессы.

**Goals**:
- Видеть куда уходят деньги
- Устанавливать квоты чтобы команда не вышла за рамки
- Approve провизию для новых сервисов

**Behaviors**:
- Раз в неделю смотрит dashboards
- Ценит high-level overviews, не details
- Может принять решение про инфру на основе визуальной информации

**Quote**: *"I need to know we're not over-spending, and I need to know it from a single page."*

**Primary flows**:
- Billing dashboard (usage breakdown)
- Quota management
- Cost alerts и approvals
- Team member list + their usage
- Project health overview

**UI implications**:
- Big numbers, sparklines, trend arrows
- Cost breakdowns by service
- Compare current vs last month
- Alerts prominently shown
- One-click "drill down" to specific tenant's usage

## Persona 4: Vasya — Open-source / self-hosted enthusiast

**Background**: Hobbyist / freelance consultant. Запускает Plexor
у себя дома или в маленьком бизнесе. Очень technical.

**Goals**:
- Получить работающее облако за минимум времени
- Расширять под свои нужды (custom images, providers)
- Понять что происходит под капотом

**Behaviors**:
- 100% keyboard-driven
- Читает docs / source code
- Готов перезагрузить ОС чтобы попробовать что-то
- Ценит reverse-engineerable UX

**Quote**: *"I want to see the API contract, the events, and the source. Don't hide anything."*

**Primary flows**:
- Initial setup (boot ISO / curl install.sh)
- First cluster setup wizard
- Browse API docs (Scalar)
- Inspect events / audit log
- Provider plugin management

**UI implications**:
- "View as code" toggle (any form → JSON / YAML)
- Link to API docs from every screen
- Show raw events / config alongside UI
- "What's running on this VM" introspection
- Power-user keyboard shortcuts

---

## Personas ↔ features ↔ screens

| Feature | Dmitriy | Maria | Andrey | Vasya |
|---------|---------|-------|--------|-------|
| VM list | primary | primary | ─ | ─ |
| Create VM wizard | secondary | primary | ─ | secondary |
| VM detail | primary | primary | ─ | secondary |
| Console | primary | secondary | ─ | secondary |
| Network (VPC/SG) | primary | secondary | ─ | secondary |
| IAM (users/SSH) | primary | secondary | ─ | ─ |
| Billing | secondary | ─ | primary | ─ |
| Audit log | primary | ─ | secondary | primary |
| Settings (provider plugins) | ─ | ─ | ─ | primary |
| API docs (Scalar) | primary | ─ | ─ | primary |

## Что это значит для дизайна

- **VM list и detail** — два наиболее критичных экрана. Должны работать
  идеально и для Dmitriy (terminal feel) и для Maria (click).
- **Billing** — отдельный фокус. Andrey должен получить ответ за 5 секунд
  с момента открытия страницы.
- **Audit log** — must-have с самого начала, не Phase 2.
- **Power-user affordances** (keyboard shortcuts, JSON view, link to API
  docs) — добавляем сразу, не "когда-нибудь".
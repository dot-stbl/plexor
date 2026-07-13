# Screen 00: Clusters (control-plane fleet)

> Self-hosted-специфичный экран, которого не было в исходных YC-брифах.
> Задокументирован постфактум по реализации (`web/apps/console/src/routes/clusters.tsx`,
> `clusters.$id.tsx`, `features/clusters/`). Центр self-hosted-топологии:
> тут пользователь видит control-plane'ы и управляет нодами.

## Purpose

Показать зарегистрированные Plexor-кластеры (control-plane + ноды) и дать
управлять составом кластера: добавить ноду (join-токен), посмотреть здоровье
нод, найти команды установки.

## User goal

Dmitriy/Vasya: «У меня control-plane на сервере — хочу подключить ещё одну ноду
и убедиться, что все ноды `ready`.»

## Entry points

- Sidebar → Clusters (`/clusters`).
- Из VM-flow: `/vms/new` ссылается на кластер/ноду (куда шедулить VM).
- Пустое состояние `/vms` может вести сюда («сначала зарегистрируйте кластер»).

## Layout

### `/clusters` — list

- `PageHeader`: title «Кластеры Plexor», description — live-счётчики
  (`N кластер(ов) · R/T нод(ов) ready`, mono-цифры), primary action.
- Grid карточек `ClusterCard` (1 / 2 / 3 колонки responsive).
- Empty state: «Нет зарегистрированных кластеров» + объяснение (`plx init` / ISO)
  + CTA на документацию по установке.

### `/clusters/{id}` — detail

- `PageHeader`: title = имя кластера, description — версия хоста (mono-бейдж),
  `R/T нод ready`, активные токены, uptime. Actions: «Назад», «Добавить нод».
- Info-карточка сверху: **Install providers** (Badge-стек: kvm / ceph-rbd / ovs …)
  + endpoint кластера (mono, copyable) + пометка «выбраны при `plx init`».
- `Tabs`:
  - **Ноды** (`counts.total`) — карточка со списком `NodeRow` (hostname, role
    control/compute, статус, spec vCPU/RAM/disk, ISO-версия, vmCount,
    lastSeen). Header-action «Добавить нод». Empty: «Нодов нет. Сгенерируйте
    join-токен и установите Plexor ISO».
  - **Join-токены** (`tokens.length`) — список `TokenRow` (label, статус
    active/revoked/expired, intendedRole, TTL/expiresAt, revoke). Header-action
    «Создать токен». Токен показывается один раз, копируется, одноразовый.
  - **Документация** — карточки-ссылки (Установка / ISO / Обновление /
    Troubleshooting) на `plexor.dev/docs/*`.
- `AddNodeDialog` — выдаёт join-токен + команду `plx node join <endpoint> --token=…`.

## Content elements

- **ClusterCard**: имя, endpoint, host-версия, статус-сводка нод (StatusPill/бейджи),
  install providers как Badge-стек.
- **NodeRow**: hostname (mono), role-бейдж, StatusPill (pending/ready/draining/offline),
  spec, ISO-версия, vmCount, относительное «last seen».
- **TokenRow**: label, StatusPill, intendedRole, обратный отсчёт до expiry, copy, revoke.

## States

- **empty (list)**: нет кластеров → CTA на docs.
- **empty (nodes)**: control-plane есть, нод нет → «сгенерируйте токен».
- **loading**: skeleton карточек / строк.
- **error**: banner + retry.
- **not-found (detail)**: «Кластер не найден» + «Назад к кластерам».

## Interactions

- «Добавить нод» / «Создать токен» → `AddNodeDialog`, выдаёт токен + команду
  (обе — через `<CopyableText>`).
- Revoke токена → confirm (`AlertDialog`), после — статус `revoked`.
- Клик по ClusterCard → `/clusters/{id}`.

## State machine

Node: `pending → ready → draining → offline` (heartbeat каждые 30s).
Token: `active → redeemed | revoked | expired`.
См. `ui-state-machines.md` (Node) и `../architecture/networking.md` § node join.

## Open design decisions

- **Primary CTA на `/clusters`** сейчас — «Документация». Для self-hosted спорно:
  вероятно правильнее «Зарегистрировать control-plane» (или показать команду
  `plx init`), а docs — вторичная ссылка. ← решить.
- Показывать ли на карточке кластера агрегат ресурсов (сумма vCPU/RAM/disk
  по нодам) как capacity-бар?
- Нужен ли отдельный `/admin/nodes` (все ноды всех кластеров) при single-cluster,
  или это дубль вкладки «Ноды»? В MVP, вероятно, дубль — отложить.

## OpenDesign prompt

> Экран управления self-hosted кластером Plexor. Тёмная/светлая тема, монохром +
> статусные цвета, Onest + JetBrains Mono. List: grid карточек кластеров
> (имя, endpoint, host-версия, сводка нод, install-providers бейджами). Detail:
> info-карточка (install providers + endpoint) и табы Ноды / Join-токены /
> Документация. Ноды — плотный список строк со StatusPill и spec в mono.
> Плотность как у dashboard, hairline-бордеры, без теней на плоских панелях.

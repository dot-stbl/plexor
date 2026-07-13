# Interaction Patterns — эталон Yandex Cloud, адаптированный под Plexor

> Yandex Cloud console — **эталон структуры интерфейса** для Plexor. Отсюда мы
> копируем **layout, приёмы, компоненты** — но НЕ палитру: YC синий, Plexor
> **монохром-ink + статусные цвета** (см. [[brand.md]] / plexor-design-direction).
> Референс-скрины: `.agents/references/`.
>
> Этот файл — каталог паттернов. Реализационные правила — в
> [`../../rules/web-frontend.md`](../../rules/web-frontend.md).

---

## 0. Что берём и что нет

| Берём (структура/приёмы) | НЕ берём |
|---|---|
| Layout экранов, секции форм, горизонтальные поля, сегменты, карты-пресеты, summary-панель, генерация кода, empty-онбординг | Синий primary/accent/selected → у нас **ink** |
| `?` help-тултипы, `*` required, inline-callouts, PREVIEW-бейджи | Разноцветные бренд-логотипы → у нас **монохром** ([[tech-icon]]) |

## 1. App chrome (верхний бар)

```
┌──────────────────────────────────────────────────────────────────────┐
│ [org ▾][folder ▾] │ ⌂ › Платформа данных › PostgreSQL      [Создать ▸] │  ← top bar
├──────────────────────────────────────────────────────────────────────┤
│ PostgreSQL                                                             │  ← page title (в шаблоне)
```

- **Крошки живут ТОЛЬКО в верхнем баре** (`AppHeader`), и строятся из
  **route-matches** (`staticData.crumb` каждого matched-роута), НЕ из nav-config
  по pathname и **НЕ рисуются внутри страниц**. Дублировать `<Breadcrumb>` в
  компоненте страницы — запрещено.
- **Глобальная «Создать ▾»** — справа в баре (эталон YC «Создать ресурс»):
  `GlobalCreateMenu` (`app-shell/global-create-menu.tsx`) создаёт **глобальные,
  кросс-секционные ресурсы** — Образ, Снапшот, SSH-ключ, Сеть (VPC), Диск. ВМ и
  кластеры БД тут НЕ создаются — у них контекстные CTA на своих страницах.
  Навигация через `useNavigate` (render-prop `<Link>` в `Menu.Item` не
  срабатывает); пункты без готового маршрута → toast «скоро». Каталог
  захардкожен — app-shell не зависит от `features/*`. ✅
- Contextual primary-CTA раздела (напр. «Создать кластер», «Создать ВМ») — в
  `actions` шаблона (`PageTemplate`), справа в шапке страницы. Одна на вид, ink.
  Секционное создание живёт здесь, глобальное — в баре (как у YC).
- Слева — scope-switcher (org/folder), затем `⌂` (home) и трейл крошек.

## 2. Шаблоны страниц — layout-routes + `<Outlet/>` (целевая архитектура)

> **Текущий `PageHeader` (рендерится в каждой странице) — deprecated.** Заменяем
> на **шаблоны через layout-routes**: общий каркас (заголовок, actions, область
> контента) живёт в layout-роуте, страницы открываются в него через `<Outlet/>`.
> Это канон TanStack и убирает копипасту чрома по страницам.

```
routes/managed/route.tsx        ← layout: staticData.crumb='Платформа данных',
                                   рендерит общий каркас + <Outlet/>
routes/managed/postgres.tsx     ← child: staticData.crumb='PostgreSQL', контент
routes/managed/new.tsx          ← child: staticData.crumb='Новый кластер', контент
```

Три базовых шаблона:

| Шаблон | Каркас | Контент через Outlet |
|---|---|---|
| **ListTemplate** | заголовок + primary CTA + (tabs/toolbar) | таблица / empty |
| **DetailTemplate** | заголовок + статус + tab-strip + actions | вкладка ресурса |
| **CreateTemplate** | заголовок + секции-скролл + **sticky summary** + футер | форма |

Заголовок/actions шаблон получает из route-context/`staticData` (или slot-пропа),
а не хардкодит каждая страница. Крошки при этом — из matches (см. §1).

## 3. List page

Заголовок + primary CTA (правый верх) + опц. tabs/toolbar-фильтры, ниже — таблица
(`DataTable density=compact`) **или** rich empty-state (§4). Плотные строки,
copyable ID/DNS/host ([[web-frontend]] Tables).

## 4. Empty-state (список пуст) — онбординг ✅ сделано

Двухколоночный: иллюстрация (бренд-марка [[tech-icon]]) слева / заголовок
«Создайте ваш первый …» + 2-3 абзаца (что это, из чего состоит) + список
doc-ссылок (Начало работы / Тонкая настройка / Бэкапы / Тарификация) + primary CTA.
Реализация: `features/databases/managed-service-empty.tsx`.

## 5. Create page — главный паттерн YC

**Длинная одностраничная секционированная форма** (НЕ модалка, НЕ wizard, если
это не настоящий multi-step). Скрины: k8s (203242-x3) / PostgreSQL (211025-x4).

- **Секции** с жирными заголовками: «Вычислительные ресурсы», «Хранилище», «База
  данных», «Сетевые настройки», «Дополнительные настройки», «Настройки СУБД».
- **Горизонтальные поля** (`FieldRow`): label слева (+ `?` help, + красная `*`
  required), контрол справа. Доминирующий layout config-форм.
- **Sticky summary справа** (§7).
- **Футер**: `Создать` (primary ink) · `Отменить` · `</> Генерация кода` (§8).

## 6. Строительные блоки форм

| Блок | Где у YC | Наш статус / примитив |
|---|---|---|
| **SegmentedControl** (2-4 взаимоисключающих: Авто/Из списка, standard/cpu-opt/mem-opt, Вручную/Сгенерировать, В любое время/По расписанию) | везде | 🔨 на `toggle-group`, selected=ink |
| **SelectableCardGrid** (тайлы «2 vCPU · 8 ГБ», большие карты «Высокодоступный/Базовый» + badge «Рекомендуемый») | k8s/pg | 🔨 обобщить из `RuntimePicker` |
| **Selectable table rows** (классы хостов, single-select строка) + **«Показать все N»** | pg | 🔨 single-select в DataTable + ShowMore |
| **Stepper** (`− [n] +`, min/max подписи) | размер диска | 🔨 новый |
| **Slider** (min/max: 24-28, 8-110, 7-60) | k8s/pg | 🔨 нет примитива — добавить |
| **PasswordInput** (глаз + Вручную/Сгенерировать) | pg | 🔨 новый |
| **FieldRow** горизонтальный (label-кол + help + required) | везде | 🔨 расширить `Field` |
| **HelpTooltip** (`?` кружок) | везде | 🔨 на `tooltip` |
| **InputGroup** (CIDR + `/mask`) | k8s | ✅ `input-group` |
| **Switch / Checkbox** (тумблеры, доступы) | везде | ✅ |
| **Repeatable rows** (Хосты: #/зона/подсеть/⋯ + «Добавить хост») | k8s/pg | 🔨 новый |
| **Callout** inline (info/warning в форме) | k8s/pg | ✅ `alert` — паттерн §9 |
| **PREVIEW/beta badge** | DB Proxy, генерация кода | ✅ `StatusPill variant=beta` |
| Placeholder-подсказка («postgresql684») | pg | ✅ мелочь |

## 7. Summary-панель (sticky aside)

YC: правая липкая панель с «₽/месяц + разбивка + инфо». **Наш аналог — «Что
развернётся»**: движок → рантайм → нода (placement), суммарные ресурсы,
binding-строка. (Зачаток уже есть в create-странице как review-блок — поднять в
липкую панель шаблона `CreateTemplate`.) ₽ не показываем (self-hosted).

## 8. Генерация кода — «`</> Генерация кода`» ⭐

YC-модалка «Команда для создания кластера» (PREVIEW): табы **Terraform /
Yandex Cloud CLI**, код с **номерами строк + подсветкой + copy**, ссылка на доку.

**Прямое попадание в вижн Plexor** (IaC-friendly, persona Vasya «покажи код»).
Наш вариант: табы **Terraform** / **`plx` CLI** (/ Pulumi позже). Каждый
нетривиальный create-флоу даёт эту кнопку. Компонент: `CodeGenDialog` + `CodeBlock`
(номера строк, copy через `CopyableText`, монохром-подсветка). PREVIEW-бейдж.

## 9. Inline callouts

Внутри форм — `Alert` для последствий:
- **info** (нейтральный ink/idle-tint): «Если не включить Cilium…».
- **warning** (`warn`): «Размер хранилища нельзя уменьшить», «Настройте окно
  обслуживания…».
Не статусные цвета ради украшения — только семантика последствия.

## 10. Монохром-адаптация (обязательно)

Selected-состояния (сегменты, карты, строки, табы) — **ink**, не синий. Ссылки —
ink + underline. Focus-ring — ink. **UI-иконки** монохром (Material Symbols,
`@/shared/ui/icon`); **тех/сервис-логотипы — ЦВЕТНЫЕ** (`<TechIcon>`, осознанный
карв-аут — см. `web-frontend.md` rule 63). Это не «упрощение YC», а принцип.

## 11. Таблицы и списки (bare-metal + VPC референсы)

- **Full-width**: таблицы/списки тянутся на всю ширину (`PageTemplate width="full"`) —
  без боковых пустот. Центрируем только узкие формы/онбординг.
- **Список — вход в раздел** (IA, rule 70): nav/лаунчер ведут на СПИСОК ресурса,
  не на `/new`. «Создать *» — CTA в шапке списка → `/new`; пустой — `EmptyState`.
  Каждый тип ресурса: layout-роут + `index` (список) + `new` (+ `$id` позже).
- **Менеджер колонок**: шестерёнка над таблицей → popover (чекбокс видимости +
  drag-хэндл порядка) + «Применить»; первая колонка запинена.
- **kv-list деталей**: «Обзор» ресурса — пары label⋯value с dotted-leader,
  copyable ID/CIDR/endpoint.
- **Secondary nav внутри ресурса**: свой левый список вкладок (Обзор / Подсети /
  SG / DNS / Операции) — отдельно от глобального сайдбара.
- **Фильтр-сайдбар** (большие каталоги): чипы (Ядра/CPU/DDR/GPU), **dual-range
  слайдеры** (Частота/RAM/Диски с min/max-инпутами), toggles; правая залипающая колонка.
- **Карта инфраструктуры**: топология-канвас (VPC-боксы → подсети по зонам),
  зум-контролы, фильтр-бар → отдельный экран (backlog).

## 12. Ресурсы — self-hosted (пресеты + КАСТОМ)

- **Пресеты + «Своя конфигурация» (+ «по запросу»)** через `SegmentedControl`
  (как YC bare-metal «Готовые / Своя / По запросу»). **Кастом обязателен** —
  self-hosted указывает ресурсы точечно от рантайма/ноды, не фиксированными
  облачными «классами».
- **Пресеты** — карточки с бинарными размерами (GiB, степени двойки): RAM
  8/16/32, диск 64/256/512 — НЕ круглые 500 (том/память бьются по 2^n).
- **Кастом = точечный ввод, НЕ дропдаун фиксированных значений**:
  - vCPU — `Stepper` (целое, кламп на blur).
  - RAM/диск — `SizeField` (`Stepper` + выбор единицы МиБ/ГиБ/ТиБ), **точность
    до МиБ** (эталон Proxmox). Наружу отдаёт **байты**; переключение единицы
    сохраняет физический размер. ✅
- **OS-образы** — раздел «Образы» (`/images`): full-width таблица с **цветными
  лого** (`<TechIcon>`: Ubuntu/Debian/Rocky/AlmaLinux), размеры через `Size`,
  видимость public/private, column-manager. Пустой — `EmptyState` c доклинками.
- Custom-инпуты **ограничены доступной ёмкостью ноды** выбранного рантайма
  (min/max у `SizeField`/`Stepper`).

## 12a. Глубина формы (self-hosted ≠ managed) — эталон Proxmox, не YC

Мы **не Yandex**: managed-облако прячет placement/гипервизор/бэкенд-хранилища/
сеть, а self-hosted их **выставляет**. Создание ВМ (`/vms/new`) — референс глубины:

- **Опции адаптируются под ноду**: storage-пулы и сетевой fabric берутся из
  `node.spec.providers` (Ceph RBD / LVM-Thin / ZFS; OVS / Cilium). Нельзя
  положить диск на бэкенд, которого нет на ноде. Это ключевой self-hosted-паттерн
  — форма знает про железо.
- **Выставленные knob'ы**: placement (нода), гипервизор (machine q35/i440fx,
  firmware UEFI/SeaBIOS, CPU type host/kvm64…), CPU sockets×cores, RAM +
  ballooning + NUMA, boot-диск (пул/размер/шина/кэш/discard/IO-thread) + доп.
  диски (`RepeatableRows`), сеть (VPC, DHCP/static IP+gw, NIC-модель, VLAN,
  firewall, rate-limit), cloud-init user-data + DNS, guest-agent, autostart,
  protection, labels.
- **Basic видно, deep — в `Disclosure`** (лёгкий self-toggle, `summary="Advanced · …"`;
  НЕ `Accordion` — тот клипает высоту и заливает фон). Базовые поля не тонут,
  глубина по клику. Не прячь knob целиком — прячь под disclosure.
- Каркас — как `/managed/new`: `width="full"`, карточки слева + липкая
  `SummaryPanel` справа, `FieldRow` для строк, `SimpleSelect` для строковых селектов.
- **LXC (`/lxc` + `/lxc/new`)** и **k8s (`/k8s` + `/k8s/new`)** — список + глубокий мастер на тех же паттернах: LXC
  (template/unprivileged/nesting, cores+limit, rootfs+mounts, features); k8s —
  кластерная форма (control-plane mode, **node pools** через `RepeatableRows`,
  CNI/ingress/LB, StorageClass из providers флота, add-ons).

## 13. Backlog компонентов (приоритет)

Сделано: `PageTemplate` (+full-width), `FieldRow`, `SegmentedControl`,
`SelectableCardGrid`, `SummaryPanel`, `HelpTooltip`, `TechIcon`, `Size`,
`SizeField` (МиБ-точность), `Stepper`, `PasswordInput`, `RepeatableRows`,
`Disclosure` (self-toggle «Advanced», не Accordion), `SimpleSelect`,
`FilterSidebar`, `EmptyState` (онбординг с доклинками), раздел «Образы»
(`/images`), `GlobalCreateMenu` (глобальная «Создать ▾» в баре),
разделы-ресурсы (список + глубокий мастер): ВМ `/vms`, LXC `/lxc`, k8s `/k8s`, образы `/images`,
пресеты+кастом-ресурсы (Stepper).

1. **CodeGen**: `CodeBlock` + `CodeGenDialog` (Terraform / `plx`).
2. **Менеджер колонок** таблицы (popover: видимость + порядок).
3. **Фильтр-сайдбар** (чипы + dual-range `Slider`) + **Stepper** / **PasswordInput** / **RepeatableRows** / **ShowMore**.
4. **Карта инфраструктуры** (топология-экран).
5. Layout-route шаблоны (`CreateTemplate`/`ListTemplate`/`DetailTemplate`); раскатка `PageTemplate`+staticData на vms/clusters/networks/audit, выпил `PageHeader`.
6. Детальные экраны `/managed/<engine>/$id` (kv-list обзор + secondary-nav + бэкапы/подключения/тюнинг).

## См. также

- [../../rules/web-frontend.md](../../rules/web-frontend.md) — реализационные правила
- [../web/routing.md](../web/routing.md) — layout-routes / Outlet / breadcrumbs
- [brand.md](brand.md) — токены (монохром), [personas.md](personas.md) — Vasya/Dmitriy
- [screens/](screens/) — брифы конкретных экранов

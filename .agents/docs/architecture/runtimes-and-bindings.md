# Runtimes & Bindings — как Plexor запускает и связывает сервисы

> Ядро продуктовой идеи Plexor: **облако — это легко.** Достигается двумя
> абстракциями — **Runtime** (где крутится workload) и **Binding** (как workloads
> связаны). Plexor — **транслятор/оркестратор поверх готовых решений**, а не их
> переизобретатель.
>
> Читать вместе с [architecture.md](../architecture.md) (общая картина) и
> [networking.md](networking.md) (mesh/overlay/DNS — субстрат для связности).
> Статус: design-anchor, реализация — Э3/Э5 (см. [`../../STATE.md`](../../STATE.md)).

---

## TL;DR

- **Service** = «что» (Postgres, Kafka, my-api). **Runtime** = «где» (VM / LXC /
  Docker / k8s). Сервис отвязан от рантайма.
- Plexor **тупой про деплой, умный про связывание.** Тело деплоя занимает у
  готовых решений (official image, Helm chart, cloud-init). Сам определяет только
  **binding-контракт** + capabilities + маппинг «runtime → артефакт».
- Рантаймы делятся на **direct** (Plexor владеет лайфсайклом: VM/LXC/Docker) и
  **delegated** (Plexor — клиент чужого оркестратора: k8s).
- Манифест сервиса — **тонкий конверт (YAML + JSON Schema), не язык.** Модель
  «Nomad jobspec», НЕ «Crossplane universal abstraction».
- Placement = `validRuntimes ∩ availableRuntimes ∩ userPref`. Capability-флаги
  (`stateful`, …) сужают сами.
- **Binding** — объект первого класса: материализует conninfo + secret + DNS +
  сетевой путь. Default-deny → binding = ещё и модель безопасности.

## 1. Философия — транслятор, не изобретатель

Plexor не пишет свой способ запускать контейнеры, свой Helm, свой cloud-init.
Он **берёт готовое и связывает**. Граница ответственности:

| Что Plexor **занимает** (не изобретает) | Что Plexor **определяет сам** |
|---|---|
| Образы (OCI), compose-фрагменты | **Binding-контракт** (как отдать connstring/secret потребителю) |
| Helm-чарты, k8s-манифесты | **Capabilities** сервиса (stateful, ports, volumes, valid runtimes) |
| cloud-init, пакетные рецепты | **Placement** (scheduler: где поднять) |
| Официальные upstream-конфиги | **Тонкий адаптер** runtime → артефакт (маппинг secret/volume/port) |

«Трансляция» — это адаптер: он маппит понятия Plexor (secret, volume, port,
binding) на то, что ожидает чужой артефакт (env официального образа, `values.yaml`
чарта). Не passthrough, но копеечно.

## 2. Runtime — два класса

Главный вопрос про любой рантайм: **Plexor владеет лайфсайклом или делегирует?**

| Класс | Runtime | Кто оркестратор | Отношение |
|---|---|---|---|
| **Direct** | `vm` (KVM), `lxc`, `docker` | **Plexor** | Знает node/container, сам start/stop/health, владеет placement |
| **Delegated** | `k8s` (позже: другой Plexor-регион, внешнее облако) | **сам k8s** | Отдаёт манифест в чужой шедулер; Plexor — bounded tenant |

**k8s — не субстрат уровня Docker, это сам оркестратор.** «Postgres в k8s» =
«k8s поднял, Plexor попросил». Притворяться `k8s == docker` нельзя — два
шедулера подерутся за placement.

### Граница k8s (locked)

- k8s — **runtime-цель, куда Plexor деплоит, НЕ peer-оркестратор.** Plexor
  владеет размещением на уровне нод; внутри k8s поды размещает k8s.
- Делегированный деплой идёт в **Plexor-owned namespace** (`plexor-system` /
  per-service), чтобы не сталкиваться с ворклоадами пользователя.
- Stateful managed-сервисы (Postgres) **по умолчанию не идут в k8s** — только
  через явный operator-рецепт с пометкой `delegated`.

## 3. Placement — пересечение множеств

```
service.validRuntimes  ∩  cluster.availableRuntimes  ∩  user.preference  →  placement
```

- Сервис объявляет валидные рантаймы: `postgres.runtimes = [vm, lxc, docker]`.
- Кластер знает доступные (нода A = docker, нода B = k8s).
- Scheduler пересекает, выбирает по политике, юзер может override (advanced).

**Реальная ось — stateful vs stateless, не «VM vs k8s».** Postgres не хочет в
делегированный k8s *потому что stateful* (нужны data-locality + контроль
лайфсайкла). Поэтому:

> **Capability-флаги гонят placement, а не рантайм-переключатель руками.**
> `stateful: true` сам сужает валидные рантаймы к direct-классу.

Флаги: `stateful`, `needsPersistentVolume`, `exposesPorts`, `singleton`, …

## 4. Service manifest — тонкий конверт (модель B)

Две философии описания сервиса. Мы выбираем **(B)**:

- **(A) Универсальный DSL-абстракция** — один синтаксис, компилящийся в
  Docker/k8s/VM. ❌ Ловушка: вечно догоняешь union фич всех рантаймов.
  Это Crossplane XRD / OAM — печально известная сложность.
- **(B) Тонкий конверт + нативные рецепты** — схема описывает только
  metadata + capabilities + binding-интерфейс; тело деплоя на каждый рантайм —
  **в его родном формате.** ✅ Прецедент: **Nomad jobspec** (`driver` = поле,
  конфиг driver-специфичный).

### Дисциплины манифеста (locked)

1. **Это манифест, не язык** — YAML + JSON Schema, без control-flow, без Тьюринг-полноты.
2. **Шаблоны — только подстановка значений** (`{{.secret.x}}`), НЕ логика. Нужен
   `if/for` → это сигнал уйти в нативный escape-hatch артефакт.
3. **Не изобретай синтаксис контейнеров/k8s** — переиспользуй OCI/compose/Helm/cloud-init.
4. **Разреженная матрица — норма.** `k8s: false` — валидный честный ответ.
5. **k8s = delegated, свой namespace**, не peer-шедулер.
6. **Capabilities гонят placement**, не человек дёргает рантайм.

### Strawman — Postgres

```yaml
service: postgres
version: "16"
category: database
stateful: true                     # → сам сужает до direct-рантаймов

runtimes:
  docker: { recipe: neutral }      # общий контейнерный дескриптор (см. ниже)
  lxc:    { recipe: neutral }
  vm:     { recipe: neutral }
  k8s:    false                    # осознанно НЕ предлагаем (stateful → direct)
  # альтернатива (delegated): k8s: { recipe: helm, chart: bitnami/postgresql, delegated: true }

container:                         # ЗАНЯТО у official image — не изобретаем
  image: postgres:16               # (blessed base → pinned + vendored)
  ports: [5432]
  volumes:
    - { name: data, path: /var/lib/postgresql/data, size: 20Gi, persistent: true }
  env:
    POSTGRES_PASSWORD: { from: secret }
  health: { tcp: 5432 }

provides:                          # binding-контракт — ОПРЕДЕЛЯЕМ САМИ, один раз, вне рантайма
  postgres:
    connstring: "postgres://{{.secret.user}}:{{.secret.password}}@{{.instance.dns}}:5432/{{.config.db}}"
```

`neutral` покрывает vm/lxc/docker механически; k8s явно выключен; binding-контракт
объявлен один раз независимо от рантайма.

## 5. Разреженная матрица — как не получить O(N×M)

- **80% — контейнер это контейнер.** Stateless/простой сервис = «OCI-образ +
  порты + env + volumes + health» → каждый direct-адаптер **транслирует
  механически** (Docker: run; k8s: gen Deployment; VM/LXC: podman/systemd-unit).
  → нейтральный дескриптор даёт **O(N+M)**.
- **Сложный/stateful** (Postgres-HA, Kafka) — нейтральный дескриптор *врёт*
  (k8s-operator ≠ VM-patroni). Нужен **нативный per-pair рецепт** = escape hatch.
- **Спасение: матрица разреженная.** Платишь только за клетки, которые сам решил
  заполнить. Postgres = ~3 клетки, k8s пропущен. Никакого взрыва.

> **Правило:** нейтральный путь по умолчанию + нативный escape-hatch там, где он
> врёт + capability-флаги, отсекающие небезопасные рантаймы.

## 6. Binding — единственное, что Plexor определяет сам

Связь `consumer → provider` — объект первого класса. Одно действие «привязать
приложение к сервису» делает сразу:

1. резолвит conninfo (host/port/creds/connstring из `provides`);
2. инъектит в потребителя по его рантайму (env / k8s Secret / cloud-init / mounted file);
3. открывает сетевой путь (SG/firewall);
4. регистрирует DNS-имя (`pg.db.plexor.internal`, см. networking.md).

Пользователю знать надо только два глагола: **declare service, declare binding.**

- **Binding = модель безопасности.** Плоский mesh, где все достают всех — дыра.
  Поэтому **default-deny**, а binding — единственный способ открыть путь
  (микросегментация через связи).
- **Ротация.** Provider меняет creds → у всех consumers инъекции обновляются.
  Binding — indirection, поэтому проектируется с notify+reload с 1-го дня.
- **Портируемость** («перенести Postgres Docker→k8s») становится реальной именно
  из-за indirection: снапшот данных → материализация на новом рантайме →
  bindings переуказываются сами. Тяжёлая часть — миграция данных, не связи.

## 7. Курирование рецептов (base vs marketplace)

**Один механизм деплоя, два уровня курирования** — НЕ две системы.

- **Base / blessed** (едет в ISO, поддерживаем мы): рецепты **pinned + vendored**
  (не тянем Docker Hub / upstream вживую в рантайме — supply-chain).
- **Marketplace / community**: те же декларативные рецепты + provenance
  (источник, подпись). YAML + hooks, без .NET.
- «Base» = просто благословлённое подмножество того же движка, не отдельный
  code-path. См. [scope.md](../scope.md), [providers.md](../providers.md).

## 8. Prior art

| Взять пример | Не повторять |
|---|---|
| **Nomad jobspec** — driver как поле, driver-специфичный конфиг (=модель B) | **Crossplane XRD / OAM** — универсальная абстракция (=модель A, сложность) |
| **Helm** — Chart.yaml (метаданные) + templates (k8s-native) | Свой синтаксис описания контейнеров |
| **compose / OCI / cloud-init** — занимаем как тело деплоя | Свой оркестратор внутри k8s |
| **k8s Service Binding spec** — валидирует концепт binding | — |

## 9. Секвенирование (что доказывает идею)

1. VM runtime + mesh + DNS → «VM за 2 мин, авто-сеть» (уже в плане).
2. **+ Docker runtime + Binding + один managed-сервис (Postgres)** →
   «Postgres в Docker, app биндится, всё прокинуто». ← **MVP самой идеи.**
   Можно пощупать на моках в UI до всякого .NET.
3. + k8s runtime → cross-runtime binding (app в k8s → Postgres в Docker).
4. Service-graph UI → портируемость → HA → multi-region (федерация).

## 10. Открытые вопросы

- Формат нейтрального контейнерного дескриптора — свой минимальный или подмножество
  compose? (склоняюсь к своему минимальному, чтобы не тянуть весь compose-spec).
- Last-hop networking: как завести Docker-bridge / k8s-CNI / VM-tap на общий
  overlay `10.100.0.0/16` с рабочим DNS (per-runtime работа — см. networking.md).
- Движок шаблонов: ограниченный Go-template vs чистый JSON-path substitution.
- Где хранится состояние binding'ов и как реагирует на move/rotate provider'а.

## См. также

- [`../../STATE.md`](../../STATE.md) — этапы и локнутые решения
- [networking.md](networking.md) — mesh / overlay / internal DNS (субстрат связности)
- [architecture.md](../architecture.md) — control/data plane, event flow
- [../providers.md](../providers.md) — install providers + app providers (marketplace)
- [../modules.md](../modules.md) — Marketplace module (исполняет эти рецепты)

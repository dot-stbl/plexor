# User Flows

5 критичных пользовательских путей. Дизайн каждого экрана должен
обеспечивать безболезненное прохождение соответствующего flow.

## Flow 1: Provision first VM (cold start)

**Actor**: Maria (junior dev) — никогда не использовала Plexor.

**Entry**: `/login` (после успешного OIDC) → автоматический редирект на
`/projects/{defaultProject}` если у неё есть дефолтный, иначе wizard
"Create your first project".

```
[Login]                                 Maria signs in via OIDC
   ↓
[First-login wizard]                   "Welcome! Create your first project"
   ↓                                    fields: name, region
[Project dashboard]                    Empty state: "No VMs yet"
   ↓                                    + Create VM CTA
[VM wizard step 1: Basics]             name, description, zone
   ↓
[VM wizard step 2: Resources]          flavor (cards: small/medium/large)
   ↓
[VM wizard step 3: Image]              card-grid of images, search,
                                       filter by family
   ↓
[VM wizard step 4: Network]            vpc, subnet, security groups
   ↓
[VM wizard step 5: Access]             ssh keys (or generate)
   ↓
[VM wizard step 6: Review]             summary + cost estimate
   ↓ click "Create"
[Wizard: Provisioning]                 progress bar, can navigate away
   ↓ ~30 seconds
[VM detail page]                        "VM Running" + IP address
   ↓
[Console modal]                         "Click to open console"
   ↓
[Done]                                  Maria's first VM is alive
```

**Success criteria**: 5 минут от login до SSH-сессии.
**Failure modes**:
- Image pull slow → progress bar visible
- Quota exceeded → clear error in wizard step 2
- Capacity unavailable → предложить другой zone / flavor

## Flow 2: Diagnose failing VM (incident)

**Actor**: Dmitriy — on-call, VM перестала отвечать.

```
[VM list]                                Filter: status=Error
   ↓
[VM detail page]                         Status: Error, condition visible
   ↓
[Activity log tab]                       Recent events: failed migrations
   ↓
[Metrics tab]                            CPU/memory graph: spike before failure
   ↓
[Console]                                Kernel panic or hung process
   ↓
[Console: type "sudo systemctl restart myapp"]
[VM: still failing]
   ↓
[Action menu → Reboot]
   ↓
[VM transitions: Error → Stopped → Provisioning → Running]
   ↓
[Done — VM back up]
```

**Success criteria**: диагноз за 2 минуты, fix за 5 минут.
**Failure modes**:
- Console не отвечает (hung kernel) → кнопка "Force power cycle"
- Не помог reboot → предложить "Recreate from snapshot"

## Flow 3: Set up network for new project

**Actor**: Dmitriy — стартует prod-environment для нового проекта.

```
[Project: switch to new]                 (admin pre-created project)
   ↓
[Network tab]                           Empty state: "No VPC yet"
   ↓                                    + Create VPC CTA
[Create VPC wizard step 1: Basics]      name, CIDR
   ↓
[Create VPC wizard step 2: Subnets]     add subnets: app, db, mgmt
   ↓ click "+ Add subnet" per zone
[Subnet form]                            cidr, zone, dhcp options
   ↓
[VPC overview created]                   Shows subnets, ready for VMs
   ↓
[Security Groups tab on VPC]
   ↓                                    + Create SG
[Create SG form]                        ingress rules (protocol, port, source)
   ↓
[Floating IPs tab on VPC]               + Allocate
[Allocate Floating IP]                 select IP, region
   ↓
[Done — Network ready]                   VMs can now be created in this VPC
```

**Success criteria**: сеть для проекта готова за 10 минут.
**Failure modes**:
- CIDR overlap с существующим VPC → validation в wizard
- Subnet allocation exhausted → автоматически resize
- IP exhausted в регионе → сообщение, нет silent failure

## Flow 4: Cost review & quota increase

**Actor**: Andrey — конец месяца, проверяет бюджет.

```
[Billing dashboard]                      "Current spend: $243 of $500 budget"
   ↓
[Spend breakdown by service]            Compute 60%, Storage 18%, etc.
   ↓ click "Compute"
[Top resources: Compute]               Sorted by cost desc
   ↓ click on a VM
[VM detail]                              "This VM costs $1.20/hour"
   ↓ "back to billing"
[Cost trend chart]                      Month-over-month comparison
   ↓
[Forecast]                               "At this rate: $389 by month-end"
   ↓
[Action: Set quota alert]
   ↓
[Quota management]                       "Increase max CPUs from 100 to 150"
   ↓
[Done — quota raised, alert set]
```

**Success criteria**: полная картина затрат за 30 секунд.
**Failure modes**:
- Anomaly detection false positive → manual override
- Quota request rejected (over tenant tier limit) → contact support

## Flow 5: Audit incident (forensics)

**Actor**: Vasya — кто-то изменил IAM-роль вчера ночью, нужно найти кто.

```
[Observability → Audit]                  Filter: time=last 24h,
                                         action=role.assign,
                                         resource=user:vasya
   ↓
[Audit timeline]                         Shows: alice@acme,
                                         2026-07-06 03:42 UTC,
                                         from 198.51.100.4 (VPN),
                                         role: viewer → admin
   ↓ click event
[Event detail]                           Full payload, IP geo,
                                         user-agent, MFA status
   ↓
[Drill into "user:vasya"]               All events for this user
   ↓
[Side-by-side: alice's recent actions]   cross-reference
   ↓
[Conclusion: account compromise suspected]
[Recommendation: rotate alice's password + API keys]
   ↓
[Action: disable alice's account]       (audit log records this too)
   ↓
[Done — incident contained]
```

**Success criteria**: root cause за 10 минут.
**Failure modes**:
- Audit log not retained long enough → silent failure, alert
- Geo/IP data missing → graceful degradation
- Massive event volume → efficient filtering + pagination

## Cross-cutting concerns

### Loading states

Все flows должны показывать что что-то происходит:
- **Skeleton** при первичной загрузке
- **Spinner** при последующих операциях (mutations)
- **Progress bar** для long operations (image pull, snapshot create)
- **Streaming updates** через SSE/WS — для фоновых операций

### Empty states

Каждый список должен иметь продуманный empty state:
- Что это за ресурс (1 строка)
- Почему пусто (объяснение)
- CTA для создания (button)

### Error states

Все mutating actions:
- Валидация на client (preventive)
- Error display с actionable message (reactive)
- Retry option (если transient)
- Contact support link (если persistent)

### Permissions / RBAC visibility

User sees only what's relevant to them:
- Filter UI affordances by role
- Hidden (not greyed) actions they can't perform
- Tooltip explaining why

## Open design questions

- **VM create wizard** — currently 6 steps. Could be 3 (less comprehensive)
  for users who already know what they want (preselected defaults with
  optional "Customize" toggle). Document decision per persona.
- **Network topology view** — visual graph or table? Currently planned: visual
  graph for Dmitriy (VPC → Subnets → VMs) + table view.
- **Billing export** — CSV / PDF / Excel / API? Pick 2 for MVP, document.
- **Audit retention** — UI показывает 365d, больше через API. Document.

Все эти решения в `[screens/0X.md]` когда сделаешь.content>
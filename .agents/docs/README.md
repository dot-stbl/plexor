# Plexor — Documentation Index

> Point d'entrée для разработчиков, LLM-агентов и оперейшенс-инженеров.
> Canonical rules живут в `.agents/rules/`. Этот каталог — operational docs.

## Что здесь

| Файл | О чём | Когда читать |
|------|-------|--------------|
| [scope.md](scope.md) | Что в MVP, что нет, как расширять | Перед тем как начать фичу |
| [architecture.md](architecture.md) | Слои, потоки данных, границы | Новый контрибьютор; перед крупным рефактором |
| [modules.md](modules.md) | Каждый модуль: контракт, зависимости, типичные операции | Перед работой с конкретным модулем |
| [providers.md](providers.md) | Каталог провайдеров + как написать свой | Добавляешь провайдер или новый ресурс |
| [yandex-cloud-parity.md](yandex-cloud-parity.md) | Карта YC-сервисов → Plexor | Планирование roadmap |
| [ui.md](ui.md) | UI design system, OpenDesign интеграция | Дизайн/фронтенд |
| [api-contracts.md](api-contracts.md) | OpenAPI workflow, фронт-codegen | Меняешь API контроллеры |
| [operations/install.md](operations/install.md) | install flow | Разворачиваешь |
| [operations/upgrade.md](operations/upgrade.md) | atomic updates | Апгрейдишь |
| [operations/troubleshooting.md](operations/troubleshooting.md) | типовые проблемы | Сломалось |

## Quick context

- **Plexor** — self-hosted IaaS/PaaS-платформа типа Yandex Cloud.
- **MVP** — 8-10 сервисов (VMs, block storage, S3, VPC, security groups,
  floating IPs, load balancers, users, projects, billing).
- **Расширяемость** — provider-plugin pattern, third-party провайдеры
  ставятся через `plx provider install <package>`.
- **Stack** — .NET 10 / Plexor.Core / ASP.NET Core / EF Core / PostgreSQL /
  NATS / Vite + React + shadcn/ui + TanStack Router.

## Архитектурный язык

- **Tenant** — корневой уровень изоляции (аналог YC-organization).
- **Project** — внутри тенанта (аналог YC-folder).
- **User** — внутри тенанта, может иметь роли в проектах.
- **Resource** — всё что видит пользователь: VM, volume, bucket, VPC, LB.
- **Provider** — плагин, который реализует ресурс через инфраструктуру
  (KVM, Ceph, OVS, MinIO, CloudNativePG, Keycloak, …).
- **Node** — сервер в кластере, на котором крутится Plexor.NodeAgent.
- **Control plane** — Plexor.Host (REST + gRPC API).
- **Data plane** — Plexor.NodeAgent + providers на каждой ноде.
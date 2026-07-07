# Upgrade

Plexor поддерживает **atomic updates** через `plx upgrade`.

## Виды upgrade

| Вид | Команда | Что меняется |
|------|---------|--------------|
| Patch (0.1.x) | `plx upgrade` (default) | Бинарь + config без breaking changes |
| Minor (0.x) | `plx upgrade --minor` | Может менять DB schema (миграции) |
| Major (x.0) | `plx upgrade --major` | Может breaking, требует подтверждения |

## Flow

1. `plx upgrade` скачивает новую версию (cosign-verified)
2. Создаёт pre-update snapshot (btrfs/ZFS subvolume)
3. Применяет изменения к @update subvolume (или через ostree-style A/B)
4. Migrates DB schema (если есть migrations)
5. Reboot в новый образ
6. Если новая версия не поднялась за 5 минут — auto-rollback на @previous

## Что меняется в Plexor при upgrade

- `plexor-host`, `plexor-portal`, `plexor-migrator`, `plexor-nodeagent` — перезапуск с rolling
- DB schema — EF Core migrations (auto-apply при старте)
- Provider plugins — auto-update через NuGet feed (если включён auto-update)
- `/etc/plexor/plexor.yaml` — config migration (если нужно)

## Rollback вручную

```bash
# 1. Остановить текущий
plx stop

# 2. Откатить на предыдущий snapshot
plx rollback

# 3. Подтвердить
plx rollback --confirm
```

## Maintenance windows

`plx upgrade --window "Sun 02:00 UTC"` — отложенный upgrade.

## См. также

- [install.md](install.md) — первоначальная установка
- [troubleshooting.md](troubleshooting.md) — если что-то пошло не так
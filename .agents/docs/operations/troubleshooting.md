# Troubleshooting

Типичные проблемы и их решения.

## `plx init` failed at step X

```bash
# 1. State.json не повреждён?
cat /var/lib/plexor/state.json | jq

# 2. Конкретный шаг можно перезапустить
plx init   # автоматически продолжит с последнего неуспешного шага

# 3. Если шаг всё время падает — диагностика:
plx doctor --step install-k3s
```

## `KVM not detected` хотя /dev/kvm есть

```bash
ls -la /dev/kvm          # должен быть rw для qemu user
[ -e /dev/kvm ] && echo "kvm exists"
groups plexor             # пользователь должен быть в группе kvm

# Fix:
sudo usermod -aG kvm plexor
sudo systemctl restart plexor-nodeagent
```

## Ceph OSDs не появляются

```bash
cephadm shell -- ceph -s                  # cluster status
lsblk                                     # обнаружение дисков
ceph-volume lvm list                      # OSDs status
journalctl -u ceph-osd@0 -n 100           # logs
```

## OVS bridge missing

```bash
ovs-vsctl show                            # текущие бриджи
systemctl status openvswitch-switch
modprobe openvswitch                      # если модуль не загружен
```

## Plexor.Host не стартует

```bash
journalctl -u plexor-core -n 200 --no-pager
plx doctor                                # встроенная диагностика
plx logs                                  # структурированные логи
```

## Portal не открывается (TLS)

```bash
# 1. Self-signed сертификат?
openssl s_client -connect localhost:8443 < /dev/null 2>&1 | grep subject

# 2. Let's Encrypt rate limit?
plx doctor --tls

# 3. DNS?
dig cloud.acme.internal
```

## VM stuck in "Provisioning"

```bash
# 1. Смотрим node agent, к которому привязана VM
plx compute vms get <vm-id>

# 2. Логи на ноде
ssh node-1 "journalctl -u plexor-nodeagent -n 100 --no-pager"

# 3. Provider health
plx provider health --provider=kvm
```

## NATS event backlog

```bash
nats stream info plexor-compute
nats stream report plexor-compute
```

## "Failed to apply migrations" при upgrade

```bash
# Безопасный rollback
plx rollback --confirm

# Или ручной downgrade миграции
plx migrator rollback --target <migration-name> --db <connection>
```

## OOM на control plane

```bash
free -h                                   # memory available
systemctl status plexor-core
plx metrics | grep plexor_memory          # internal metrics

# Если реально не хватает RAM — уменьшить cache:
plx config set idp.cache.ttl=00:05:00
```

## Provider plugin не загружается

```bash
plx provider list                        # все установленные
plx provider health --provider=<X>       # конкретный
plx provider logs --provider=<X>
```

Если `Provider not found` после `plx provider install`:
- Проверьте что NuGet пакет содержит `provider-manifest.yaml`
- Перезапустите Plexor.Host

## Часто нужные команды

```bash
plx status                # общий статус кластера
plx doctor                # диагностика всех слоёв
plx provider list         # какие провайдеры установлены
plx provider health       # health-check всех провайдеров
plx logs --since=1h       # логи за последний час
plx metrics               # Prometheus-совместимые метрики

# Emergency
plx stop                  # остановить всё
plx destroy --keep-data   # удалить всё, сохранить данные
```
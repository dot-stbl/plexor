import { Link } from '@tanstack/react-router';
import { AccountTree, Add } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/primitives/button';
import { EmptyState } from '@/shared/ui/primitives/empty-state';

/**
 * Пустое состояние для раздела сетей — «VPC ещё не создан». В отличие
 * от managed-сервисов, у VPC нет «первого экземпляра»; докучи — это
 * инфраструктурный контейнер, который держит подсети, SG и floating
 * IPs. CTA ведёт на форму создания VPC (Phase X).
 */
export function NetworksEmpty() {
  const { t } = useTranslation();
  return (
    <EmptyState
      data-od-id="networks-empty"
      icon={AccountTree}
      title={t('networks.empty.title')}
      description={t('networks.empty.description')}
      docs={[
        { href: 'https://plexor.dev/docs/networking/vpc', label: t('networks.empty.docs.vpc') },
        { href: 'https://plexor.dev/docs/networking/subnets', label: t('networks.empty.docs.subnets') },
        { href: 'https://plexor.dev/docs/networking/security-groups', label: t('networks.empty.docs.sg') },
      ]}
      action={
        <Button nativeButton={false} render={<Link to="/networks" />}>
          <Add className="size-3.5" />
          {t('networks.empty.cta')}
        </Button>
      }
    />
  );
}

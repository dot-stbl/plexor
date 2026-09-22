import { History } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { useTranslation } from 'react-i18next';
import { EmptyState } from '@/shared/ui/primitives/empty-state';

/**
 * Пустое состояние timeline'а аудита — «ещё ничего не произошло». Отличается
 * от managed-онбординга (см. `ManagedServiceEmpty`): здесь нет CTA «создать
 * первый», потому что audit rows появляются в результате действий пользователя,
 * не кнопкой. Документация — единственный выход.
 */
export function AuditEmpty() {
  const { t } = useTranslation();
  return (
    <EmptyState
      data-od-id="audit-empty"
      icon={History}
      title={t('audit.empty.title')}
      description={t('audit.empty.description')}
      docs={[
        {
          href: 'https://plexor.dev/docs/audit',
          label: t('audit.empty.docs.audit'),
        },
        {
          href: 'https://plexor.dev/docs/retention',
          label: t('audit.empty.docs.retention'),
        },
      ]}
    />
  );
}

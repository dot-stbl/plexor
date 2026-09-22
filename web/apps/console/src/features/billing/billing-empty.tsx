import { Receipt } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { useTranslation } from 'react-i18next';
import { EmptyState } from '@/shared/ui/primitives/empty-state';
import { Button } from '@/shared/ui/primitives/button';

/**
 * Пустое состояние для стека биллинга: когда usage не зафиксирован (проект
 * только что поднят) и/или нет инвойсов. Сообщение отличается от пустой
 * таблицы счетов — здесь речь про «ничего не зафиксировано вообще», там
 * про «инвойсы выставляются раз в месяц».
 */
export function BillingEmpty() {
  const { t } = useTranslation();
  return (
    <EmptyState
      data-od-id="billing-empty"
      icon={Receipt}
      title={t('billing.empty.title')}
      description={t('billing.empty.description')}
      action={<Button variant="outline">{t('billing.empty.title')}</Button>}
    />
  );
}

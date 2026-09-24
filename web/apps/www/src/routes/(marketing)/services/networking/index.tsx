import { createFileRoute } from '@tanstack/react-router';
import { requireBentoCell } from '@/components/marketing/bento/bento-data';
import { requireServiceContent } from '@/components/marketing/services/service-content';
import { ServicePage } from '@/components/marketing/services/service-page';

const CELL = requireBentoCell('networking');
const CONTENT = requireServiceContent('networking');

export const Route = createFileRoute('/(marketing)/services/networking/')({
  component: Page,
  head: () => ({
    meta: [
      { title: `${CELL.title} — Plexor` },
      { name: 'description', content: CONTENT.lead },
    ],
  }),
});

function Page() {
  return <ServicePage id="networking" />;
}
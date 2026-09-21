import { loader } from 'fumadocs-core/source';
import { defineDocs } from 'fumadocs-mdx/macro';
import { metaSchema, pageSchema } from 'fumadocs-core/source/schema';
import { i18n } from '@/lib/i18n';
import { getIcon } from '@/components/icons';

const docs = defineDocs({
  dir: 'content/docs',
  docs: {
    schema: pageSchema,
  },
  meta: {
    schema: metaSchema,
  },
});

/**
 * `baseUrl: '/'` + `hideLocale: 'default-locale'` (see lib/i18n.ts) means:
 * RU pages get URLs without a locale prefix (`/exchanges` -> public
 * `/docs/exchanges/`), EN pages keep the prefix (`/en/exchanges` ->
 * public `/docs/en/exchanges/`). This matches the two Next route trees:
 * `app/(ru)/...` at the internal root and `app/(en)/en/...`.
 */
export const source = loader({
  baseUrl: '/',
  i18n,
  icon: getIcon,
  source: docs.toFumadocsSource(),
  plugins: [],
});

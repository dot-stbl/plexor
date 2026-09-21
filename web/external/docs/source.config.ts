import { defineDocs } from 'fumadocs-mdx/config';
import { metaSchema, pageSchema } from 'fumadocs-core/source/schema';

/**
 * Canonical fumadocs-mdx collection declaration, mirroring the `defineDocs` macro
 * call in `lib/source.ts` (same dir + schemas). The macro in `lib/source.ts` is
 * what the bundler compiles content files against (collections are discovered
 * from macro call sites); this file declares the same collection globally so the
 * fumadocs-mdx build-time tooling (typegen, config load) sees a conventional
 * package layout.
 *
 * Note: fumadocs-mdx v15 has no `checkLinks` option (older fumadocs versions did)
 * — relative links are resolved through the source loader instead
 * (`createRelativeLink` in components/docs-page-view.tsx), so nothing is lost.
 */
export const docs = defineDocs({
  dir: 'content/docs',
  docs: {
    schema: pageSchema,
  },
  meta: {
    schema: metaSchema,
  },
});

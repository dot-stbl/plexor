import defaultMdxComponents from 'fumadocs-ui/mdx';
import type { MDXComponents } from 'mdx/types';
import { DocsHero, Status } from '@/components/docs-blocks';

export function getMDXComponents(components?: MDXComponents) {
  return {
    ...defaultMdxComponents,
    DocsHero,
    Status,
    ...components,
  } satisfies MDXComponents;
}
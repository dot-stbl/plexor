import defaultMdxComponents from 'fumadocs-ui/mdx';
import type { MDXComponents } from 'mdx/types';
import { DocsHero, Status } from '@/components/docs-blocks';
import { ThemePreview } from '@/components/landing';
import { TerminalPlayground } from '@/components/terminal-playground';
import { ArchitectureDiagram } from '@/components/architecture-diagram';
import { Manifesto } from '@/components/manifesto';
import { Roadmap } from '@/components/roadmap';
import { ProvidersDirectory } from '@/components/providers-directory';

export function getMDXComponents(components?: MDXComponents) {
  return {
    ...defaultMdxComponents,
    DocsHero,
    Status,
    ThemePreview,
    TerminalPlayground,
    ArchitectureDiagram,
    Manifesto,
    Roadmap,
    ProvidersDirectory,
    ...components,
  } satisfies MDXComponents;
}
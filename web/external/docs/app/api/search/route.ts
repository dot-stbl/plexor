import { source } from '@/lib/source';
import { createFromSource } from 'fumadocs-core/search/server';

/**
 * Static search index: with `output: 'export'` the response of `staticGET`
 * is prerendered into the export, and the browser-side `staticClient`
 * (components/search-dialog.tsx) downloads it and runs the search locally.
 * The default multilingual tokenizer (Unicode word segmentation) handles
 * both RU and EN in a single shared database.
 */
export const revalidate = false;

export const { staticGET: GET } = createFromSource(source);

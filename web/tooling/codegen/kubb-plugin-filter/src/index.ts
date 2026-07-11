// @plexor/kubb-plugin-filter — generates typed fluent filter builders + sortable
// field lists from the backend x-filterable / x-sortable OpenAPI extensions
// (emitted by Hybrid.Composition.OpenApi.FilterableSchemaTransformer /
// SortableSchemaTransformer). One file per filterable entity schema.
import path from 'node:path';

import { definePlugin } from '@kubb/core';
import { pluginOasName } from '@kubb/plugin-oas';
import type { Oas } from '@kubb/oas';

import { generateFilterBuilderSource, generateSortBuilderSource, type EntityBuildContext } from './generator';
import { SERIALIZER_SOURCE } from './serializer-source';
import type { FilterableExtension } from './types';

export const pluginFilterName = 'plugin-filter.ts' as const;

export interface PluginFilterOptions {
    readonly output?: {
        readonly path?: string;
    };
}

export const pluginFilter = definePlugin((options: { output?: { path?: string } }) => {
    const outputPath = options?.output?.path ?? 'filters';

    return {
        name: pluginFilterName,
        options: { output: { path: outputPath } },
        // plugin-oas must parse the spec first so getOas() returns a resolved tree.
        pre: [pluginOasName],

        resolvePath(baseName: string): string {
            const root = path.resolve(this.config.root, this.config.output.path);
            return path.resolve(root, outputPath, baseName);
        },

        resolveName(name: string): string {
            return name;
        },

        async install() {
            const oas: Oas = await this.getOas();
            const { schemas } = oas.getSchemas({ includes: ['schemas'] });

            const entityContexts: EntityBuildContext[] = [];
            for (const [schemaName, schema] of Object.entries(schemas) as Array<[string, Record<string, unknown>]>) {
                const ctx = extractEntityContext(schemaName, schema);
                if (ctx !== null) {
                    entityContexts.push(ctx);
                }
            }

            if (entityContexts.length === 0) {
                return;
            }

            // 1. Runtime (serializer + Term types) — emitted once, imported by every builder.
            await this.addFile({
                baseName: '_runtime.ts',
                path: path.resolve(this.config.root, this.config.output.path, outputPath, '_runtime.ts'),
                sources: [{ name: '_runtime', value: SERIALIZER_SOURCE }],
                imports: [],
                exports: [],
            });

            // 2. One file per filterable entity: builder + sortable fields.
            for (const ctx of entityContexts) {
                const source = generateFilterBuilderSource(ctx) + '\n' + generateSortBuilderSource(ctx);
                const baseName = `${ctx.entityName}.ts` as `${string}.${string}`;
                await this.addFile({
                    baseName,
                    path: path.resolve(this.config.root, this.config.output.path, outputPath, baseName),
                    sources: [{ name: ctx.entityName, value: source }],
                    imports: [],
                    exports: [],
                });
            }

            // 3. Barrel index re-exporting all entity modules.
            const barrelLines = entityContexts
                .map((ctx) => `export * from './${ctx.entityName}';`)
                .join('\n');
            await this.addFile({
                baseName: 'index.ts',
                path: path.resolve(this.config.root, this.config.output.path, outputPath, 'index.ts'),
                sources: [{ name: 'index', value: barrelLines }],
                imports: [],
                exports: [],
            });
        },
    };
});

function extractEntityContext(schemaName: string, schema: Record<string, unknown>): EntityBuildContext | null {
    const filterable = schema['x-filterable'] as FilterableExtension | undefined;
    if (!filterable || !Array.isArray(filterable.fields) || filterable.fields.length === 0) {
        return null;
    }

    const sortable = (schema['x-sortable'] as readonly string[] | undefined) ?? [];

    return {
        entityName: schemaName,
        filterable,
        sortable,
    };
}

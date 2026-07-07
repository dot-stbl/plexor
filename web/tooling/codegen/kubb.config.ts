// kubb config — generates @plexor/api from Plexor.Host OpenAPI spec.
// Run: bun run codegen (from web/).
// Reads: ../../artifacts/openapi.json (built by `dotnet build` of Plexor.Host)
// Writes: ../shared/api/src/{types,client,hooks,schemas,msw,fixtures}

import { defineConfig } from '@kubb/core';
import { pluginOas } from '@kubb/plugin-oas';
import { pluginClient } from '@kubb/plugin-client';
import { pluginReactQuery } from '@kubb/plugin-react-query';
import { pluginZod } from '@kubb/plugin-zod';
import { pluginMsw } from '@kubb/plugin-msw';
import { pluginFaker } from '@kubb/plugin-faker';

export default defineConfig({
  root: '.',
  input: {
    path: '../../artifacts/openapi.json',
  },
  output: {
    path: '../shared/api/src',
    clean: true,
  },
  plugins: [
    pluginOas({ validate: true }),
    pluginClient({ output: { path: 'client' } }),
    pluginZod({ output: { path: 'schemas' } }),
    pluginReactQuery({ output: { path: 'hooks' } }),
    pluginFaker({ output: { path: 'fixtures' } }),
    pluginMsw({ output: { path: 'msw' } }),
  ],
});

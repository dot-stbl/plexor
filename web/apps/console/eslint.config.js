import plexorConfig from '@plexor/eslint-config';

export default [
  ...plexorConfig,
  {
    ignores: [
      'dist/**',
      'node_modules/**',
      'coverage/**',
      'playwright-report/**',
      'public/**',
      'src/routeTree.gen.ts',
      'src/shared/api/src/.kubb/**',
    ],
  },
];

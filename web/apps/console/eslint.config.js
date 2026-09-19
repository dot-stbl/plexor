import plexorConfig from '@plexor/eslint-config';

export default [
  ...plexorConfig,
  {
    ignores: [
      'dist/**',
      'node_modules/**',
      'coverage/**',
      'playwright-report/**',
      'src/routeTree.gen.ts',
      // kubb-generated API client (excluded from tsconfig as well)
      'src/shared/api/src/**',
      // MSW generated worker script
      'public/mockServiceWorker.js',
    ],
  },
  {
    files: ['**/*.{ts,tsx}'],
    rules: {
      // shadcn-style extension-point interfaces (`interface XProps extends
      // YProps {}`) are the DS pattern for prop passthrough; empty interfaces
      // without a single extends still error.
      '@typescript-eslint/no-empty-object-type': [
        'error',
        { allowInterfaces: 'with-single-extends' },
      ],
    },
  },
];

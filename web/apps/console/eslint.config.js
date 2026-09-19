import plexorConfig from '@plexor/eslint-config';

export default [
  ...plexorConfig,
  {
    ignores: [
      'dist/**',
      'dist-storybook/**',
      'node_modules/**',
      'coverage/**',
      'playwright-report/**',
      'src/routeTree.gen.ts',
      // kubb output — generated (see web/tooling/codegen/kubb.config.ts); the
      // hand-written msw.test.ts inside is exercised by vitest, not eslint.
      'src/shared/api/src/**',
      // MSW generated worker script (`msw init public/`).
      'public/**',
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

import plexorConfig from '@plexor/eslint-config';

export default [
  ...plexorConfig,
  {
    ignores: ['**/dist/**', '**/node_modules/**'],
  },
];
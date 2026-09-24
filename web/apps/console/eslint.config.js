import plexorConfig from '@plexor/eslint-config';

export default [
  ...plexorConfig,
  {
    ignores: [
      'dist/**',
      'dist-storybook/**',
      'node_modules/**',
      'coverage/**',
      'src/routeTree.gen.ts',
      // kubb output — generated (see web/tooling/codegen/kubb.config.ts).
      // The hand-written msw smoke test lives in src/shared/api/mocks/
      // (outside the clean-wiped dir) and IS linted.
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
      // UI stack is react-aria-components wrapped in src/shared/ui/primitives.
      // The pre-migration libs are banned outright; reintroducing one is a
      // regression, not a shortcut. NOTE: no-restricted-imports does NOT
      // merge across flat-config blocks — a later block that sets the rule
      // replaces this one for its files. The src-scoped blocks below must
      // repeat the banned-libs patterns.
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['@base-ui/*', '@shadcn/react', '@hugeicons/*'],
              message: 'Banned UI library (pre react-aria migration). Use the DS wrappers in src/shared/ui/primitives (react-aria-components based) or @nine-thirty-five/material-symbols-react for icons.',
            },
          ],
        },
      ],
    },
  },
  {
    // react-aria-components is the primitive layer — only src/shared/ui/**
    // may import it directly. Features and routes go through the wrappers
    // in src/shared/ui/primitives so the DS owns the styling surface.
    //
    // Domain boundary (frontend DDD, .agents/docs/architecture/frontend-ddd.md
    // §3): this block covers everything under src/ EXCEPT src/shared/** (see
    // the two shared-scoped blocks below for that half) — routes/, domains/,
    // mocks/, test-utils/, lib/, etc. These are allowed to import a domain's
    // public barrel (@/domains/<context>) but never reach past it into
    // another domain's internals.
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/shared/**'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['@base-ui/*', '@shadcn/react', '@hugeicons/*'],
              message: 'Banned UI library (pre react-aria migration). Use the DS wrappers in src/shared/ui/primitives (react-aria-components based) or @nine-thirty-five/material-symbols-react for icons.',
            },
            {
              group: ['react-aria-components', 'react-aria-components/*'],
              message: 'Import the DS wrapper from src/shared/ui/primitives instead of react-aria-components directly.',
            },
            {
              group: ['@iconify/react', '@iconify/react/*'],
              message: 'Only src/shared/ui/primitives/tech-icon.tsx renders iconify icons — reuse TechIcon from @/shared/ui/primitives/tech-icon.',
            },
            {
              group: ['@/domains/*/model/*', '@/domains/*/api/*', '@/domains/*/ui/*', '@/domains/*/mocks/*'],
              message: 'Import another domain only via its public barrel (@/domains/<context>), never a deep path. Same-domain code uses a relative import (./model/x), not the @/domains/x alias form.',
            },
          ],
        },
      ],
    },
  },
  {
    // src/shared/lib + src/shared/api (the shared/ui half is the next
    // block): react-aria-components / iconify stay banned same as above,
    // PLUS the domain boundary's one hard rule with no exceptions —
    // shared/** may never import a domain, not even its barrel. A shared
    // module that thinks it needs domain data inverts control instead: the
    // route/composition-root fetches it and passes it down as a prop (see
    // frontend-ddd.md §3 and the AppLauncher fix in step 10).
    files: ['src/shared/**/*.{ts,tsx}'],
    ignores: ['src/shared/ui/**'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['@base-ui/*', '@shadcn/react', '@hugeicons/*'],
              message: 'Banned UI library (pre react-aria migration). Use the DS wrappers in src/shared/ui/primitives (react-aria-components based) or @nine-thirty-five/material-symbols-react for icons.',
            },
            {
              group: ['react-aria-components', 'react-aria-components/*'],
              message: 'Import the DS wrapper from src/shared/ui/primitives instead of react-aria-components directly.',
            },
            {
              group: ['@iconify/react', '@iconify/react/*'],
              message: 'Only src/shared/ui/primitives/tech-icon.tsx renders iconify icons — reuse TechIcon from @/shared/ui/primitives/tech-icon.',
            },
            {
              group: ['@/domains/*', '@/domains/*/**'],
              message: "shared/ must not depend on a domain. Invert control: the route/composition-root fetches domain data and passes it down as a prop.",
            },
          ],
        },
      ],
    },
  },
  {
    // src/shared/ui (the DS layer itself): react-aria-components is allowed
    // here, but iconify stays banned outside tech-icon.tsx /
    // tech-icon-data.ts (the generated logo set holds a type-only
    // IconifyIcon import; allowTypeImports needs eslint >= 9.18, we pin
    // 9.17 — hence the explicit ignore entries). Same domain-boundary ban
    // as the shared/lib + shared/api block above.
    files: ['src/shared/ui/**/*.{ts,tsx}'],
    ignores: ['src/shared/ui/primitives/tech-icon.tsx', 'src/shared/ui/tech-icon-data.ts'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['@base-ui/*', '@shadcn/react', '@hugeicons/*'],
              message: 'Banned UI library (pre react-aria migration). Use the DS wrappers in src/shared/ui/primitives (react-aria-components based) or @nine-thirty-five/material-symbols-react for icons.',
            },
            {
              group: ['@iconify/react', '@iconify/react/*'],
              message: 'Only src/shared/ui/primitives/tech-icon.tsx renders iconify icons — reuse TechIcon from @/shared/ui/primitives/tech-icon.',
            },
            {
              group: ['@/domains/*', '@/domains/*/**'],
              message: "shared/ must not depend on a domain. Invert control: the route/composition-root fetches domain data and passes it down as a prop.",
            },
          ],
        },
      ],
    },
  },
];

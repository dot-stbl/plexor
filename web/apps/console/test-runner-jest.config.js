const { getJestConfig } = require('@storybook/test-runner');

// The default Jest configuration comes from @storybook/test-runner.
//
// Two overrides are needed because Plexor's web/ monorepo sits inside a
// repo with its own `package.json` at the root, which breaks the
// test-runner's `getProjectRoot()` (it walks up from process.cwd() to
// the first package.json and stops — so it picks the repo root, not
// the app root):
//
//   1. `rootDir: __dirname` — Jest treats `web/apps/console` as the
//      project root, not the entire repo (13 347 files vs 634).
//   2. `testMatch` — the test-runner's default testMatch comes from
//      `getStorybookMetadata()`, which joins stories paths against the
//      (wrong) monorepo-root workingDir. We replace it with a path
//      relative to __dirname.
//
// `testRegex` is used instead of a glob because Jest's glob matcher
// (micromatch under the hood) had trouble with the literal `.` in
// `plexor/.agents/worktree/...` on Windows; a regex that matches
// `.stories.{ts,tsx,...}` is more portable.
const testRunnerConfig = getJestConfig();

/**
 * @type {import('@jest/types').Config.InitialOptions}
 */
module.exports = {
  ...testRunnerConfig,
  rootDir: __dirname,
  testMatch: undefined,
  testRegex: '\\.stories\\.[jt]sx?$',
};



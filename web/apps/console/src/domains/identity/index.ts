/**
 * Public surface of the identity domain (login form + its schema/error
 * mapping/provider icons). The session storage mechanism itself
 * (read/write/clear the bearer token) is shared kernel, not part of this
 * domain -- see shared/lib/session.ts. Routes import from
 * '@/domains/identity'; internal model/ui files stay unexported outside
 * this barrel (see .agents/docs/architecture/frontend-ddd.md).
 */
export { LoginPage } from './ui/login-page';
export { GoogleIcon, GithubIcon } from './ui/provider-icons';
export { loginSchema } from './model/login.schema';
export type { LoginValues } from './model/login.schema';
export { loginErrorKey } from './model/login-error';

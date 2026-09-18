import { z } from 'zod';

/**
 * Login form schema — the single source of both the validation rules
 * and the inferred form value type (z.infer). Mirrors the OIDC-aware
 * reference in console.x `domains/auth/model/login.schema.ts`.
 *
 *   email:    .trim() before checks (paste from clipboard often has
 *             leading/trailing whitespace, invisible to the user),
 *             .toLowerCase() after (mobile autocap uppercases the first
 *             letter — backend receives the canonical address).
 *   password: no strength rules — this is sign-in, not registration.
 *
 * Length caps (254 email / 128 password) match the backend's
 * PostAuthLoginRequest contract; rejecting over-long input on the
 * client avoids a round-trip just to surface a 400.
 *
 * The error messages are i18n KEYS — the form resolves them via
 * t(error.message). Returning keys (not user-facing strings) keeps the
 * schema renderer-agnostic and pairs cleanly with the locale JSON.
 */
export const loginSchema = z.object({
  email: z
    .string()
    .trim()
    .min(1, 'auth.login.validation.emailRequired')
    .max(254, 'auth.login.validation.emailTooLong')
    .email('auth.login.validation.emailInvalid')
    .toLowerCase(),
  password: z
    .string()
    .min(1, 'auth.login.validation.passwordRequired')
    .max(128, 'auth.login.validation.passwordTooLong'),
});

export type LoginValues = z.infer<typeof loginSchema>;

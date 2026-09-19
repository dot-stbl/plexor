import { useState, type FormEvent } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { ZodError } from 'zod';
import { Login as LoginIcon } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Input } from '@/shared/ui/primitives/input';
import { PasswordInput } from '@/shared/ui/primitives/password-input';
import { Button } from '@/shared/ui/primitives/button';
import { Label } from '@/shared/ui/primitives/label';
import { Alert, AlertDescription } from '@/shared/ui/primitives/alert';
import { Spinner } from '@/shared/ui/primitives/spinner';
import { HelpTooltip } from '@/shared/ui/primitives/help-tooltip';
import { PlexorMark } from '@/shared/ui/app-shell/plexor-mark';
import { cn } from '@/lib/utils';
import { postAuthLogin } from '@/shared/api';
import { loginSchema, type LoginValues } from './login.schema';
import { loginErrorKey } from './login-error';
import { writeSession } from './session-storage';

/**
 * LoginPage — credentials + SSO entry point for the Plexor console.
 *
 * Minimalist brand surface: big PlexorMark + title + subtitle stack on
 * a single centered column with the form directly beneath. No card
 * chrome — GitHub / Notion / Figma sign-in pattern. The PlexorMark +
 * title fade in from the top on mount to soften the page transition
 * and signal that this is the brand surface, not a generic form.
 *
 * Form follows the reference shape from console.x: email + password
 * fields, an SSO button below the password, and an inline error
 * banner when login fails. Validation is client-side (Zod) so the
 * user gets immediate feedback for empty / malformed email; the
 * server-side 401 is the only failure the user should see in the
 * banner.
 *
 * On success the page persists the session triple (accessToken,
 * refreshToken, user) to localStorage and routes to `/` so the home
 * dashboard renders with the bearer token. The OIDC button is wired
 * through `window.location.assign` — the OpenAPI contract specifies
 * an IdP redirect, and that intentionally bypasses the in-app
 * router.
 *
 * The form is intentionally small: no "forgot password" surface
 * (Phase 5+ lands password recovery) and no "create account" path
 * (operator-only console — accounts are provisioned via the admin
 * auth-provider page).
 */

const REDIRECT_AFTER_LOGIN = '/';
const DEFAULT_ORG_ID = '00000000-0000-0000-0000-000000000001';
const PLEXOR_MARK_SIZE = 'size-14';

export interface LoginPageProps {
  /** Optional override for tests (otherwise the hook router). */
  navigate?: ReturnType<typeof useNavigate>;
}

export function LoginPage({ navigate: navigateOverride }: LoginPageProps = {}) {
  const { t } = useTranslation();
  const routerNavigate = useNavigate();
  const navigate = navigateOverride ?? routerNavigate;

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState<{ email?: string; password?: string }>({});
  const [formErrorKey, setFormErrorKey] = useState<string | null>(null);

  const loginMutation = useMutation({
    mutationFn: async (values: LoginValues) => postAuthLogin(values),
  });

  const isSubmitting = loginMutation.isPending;

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFormErrorKey(null);

    const parsed = loginSchema.safeParse({ email, password });
    if (!parsed.success) {
      setFieldErrors(toFieldErrors(parsed.error));
      return;
    }
    setFieldErrors({});

    loginMutation.mutate(parsed.data, {
      onSuccess: (response) => {
        writeSession({
          accessToken: response.accessToken,
          refreshToken: response.refreshToken,
          expiresAt: Date.now() + response.expiresIn * 1000,
          user: response.user,
        });
        void navigate({ to: REDIRECT_AFTER_LOGIN });
      },
      onError: (error) => {
        setFormErrorKey(loginErrorKey(error));
      },
    });
  };

  const handleSso = () => {
    const target = `/auth/oidc/authorize?org=${encodeURIComponent(DEFAULT_ORG_ID)}&redirect=${encodeURIComponent(REDIRECT_AFTER_LOGIN)}`;
    window.location.assign(target);
  };

  return (
    <main
      className="flex min-h-dvh items-center justify-center bg-background p-6"
      data-od-id="login"
    >
      <div
        className="flex w-full max-w-sm flex-col items-stretch gap-8 animate-in fade-in slide-in-from-top-4 duration-500"
        data-od-id="login-surface"
      >
        <header className="flex flex-col items-center gap-4 text-center" data-od-id="login-brand">
          <PlexorMark
            className={cn(PLEXOR_MARK_SIZE, 'text-foreground')}
            data-testid="login-mark"
          />
          <div className="space-y-1.5">
            <h1
              className="font-heading text-2xl font-semibold tracking-tight text-foreground"
              data-testid="login-title"
            >
              {t('auth.login.title')}
            </h1>
            <p
              className="text-sm text-muted-foreground"
              data-testid="login-subtitle"
            >
              {t('auth.login.subtitle')}
            </p>
          </div>
        </header>

        <div className="flex flex-col gap-3" data-testid="login-form-wrapper">
          {formErrorKey !== null && (
            <Alert variant="destructive" data-testid="login-error">
              <AlertDescription>{t(formErrorKey)}</AlertDescription>
            </Alert>
          )}

          <form className="flex flex-col gap-3" onSubmit={handleSubmit} noValidate data-testid="login-form">
            <div className="space-y-1.5">
              <div className="flex items-center gap-1.5">
                <Label htmlFor="login-email" className="text-xs font-medium">
                  {t('auth.login.email.label')}
                </Label>
                <HelpTooltip>{t('auth.login.email.help')}</HelpTooltip>
              </div>
              <Input
                id="login-email"
                type="email"
                autoComplete="username"
                placeholder={t('auth.login.email.placeholder')}
                maxLength={254}
                value={email}
                onChange={(event) => {
                  setEmail(event.target.value);
                  if (fieldErrors.email !== undefined) {
                    setFieldErrors((prev) => ({ ...prev, email: undefined }));
                  }
                }}
                aria-invalid={fieldErrors.email !== undefined}
                disabled={isSubmitting}
                data-testid="login-email"
                className={cn(
                  fieldErrors.email !== undefined &&
                    'border-destructive ring-2 ring-destructive/20',
                )}
              />
              {fieldErrors.email !== undefined && (
                <p className="text-xs text-destructive" data-testid="login-email-error">
                  {t(fieldErrors.email)}
                </p>
              )}
            </div>

            <div className="space-y-1.5">
              <div className="flex items-center gap-1.5">
                <Label htmlFor="login-password" className="text-xs font-medium">
                  {t('auth.login.password.label')}
                </Label>
                <HelpTooltip>{t('auth.login.password.help')}</HelpTooltip>
              </div>
              <PasswordInput
                id="login-password"
                autoComplete="current-password"
                placeholder={t('auth.login.password.placeholder')}
                maxLength={128}
                value={password}
                onChange={(event) => {
                  setPassword(event.target.value);
                  if (fieldErrors.password !== undefined) {
                    setFieldErrors((prev) => ({ ...prev, password: undefined }));
                  }
                }}
                aria-invalid={fieldErrors.password !== undefined}
                disabled={isSubmitting}
                data-testid="login-password"
                className={cn(
                  fieldErrors.password !== undefined &&
                    'border-destructive ring-2 ring-destructive/20',
                )}
              />
              {fieldErrors.password !== undefined && (
                <p className="text-xs text-destructive" data-testid="login-password-error">
                  {t(fieldErrors.password)}
                </p>
              )}
            </div>

            <Button
              type="submit"
              variant="default"
              size="default"
              className="w-full"
              disabled={isSubmitting}
              aria-busy={isSubmitting}
              data-testid="login-submit"
            >
              {isSubmitting && <Spinner className="size-3.5" aria-hidden="true" />}
              {isSubmitting ? t('auth.login.submitting') : t('auth.login.submit')}
            </Button>
          </form>

          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="w-full"
            onClick={handleSso}
            disabled={isSubmitting}
            data-testid="login-sso"
          >
            <LoginIcon className="size-3.5" aria-hidden="true" />
            {t('auth.login.sso')}
          </Button>
        </div>
      </div>
    </main>
  );
}

function toFieldErrors(error: ZodError): { email?: string; password?: string } {
  const result: { email?: string; password?: string } = {};
  for (const issue of error.issues) {
    const key = issue.path[0];
    if (key === 'email' && result.email === undefined) {
      result.email = issue.message;
      continue;
    }
    if (key === 'password' && result.password === undefined) {
      result.password = issue.message;
    }
  }
  return result;
}
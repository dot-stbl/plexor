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
import { Card, CardContent } from '@/shared/ui/primitives/card';
import { Spinner } from '@/shared/ui/primitives/spinner';
import { HelpTooltip } from '@/shared/ui/primitives/help-tooltip';
import { cn } from '@/lib/utils';
import { postAuthLogin } from '@/shared/api';
import { loginSchema, type LoginValues } from './login.schema';
import { loginErrorKey } from './login-error';
import { writeSession } from './session-storage';

/**
 * LoginPage — credentials + SSO entry point for the Plexor console.
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
    <div
      className="flex min-h-dvh items-center justify-center bg-background p-4"
      data-od-id="login"
    >
      <Card className="w-full max-w-sm border-border bg-card shadow-sm" data-od-id="login-card" data-testid="login-card">
        <CardContent className="space-y-3">
          {formErrorKey !== null && (
            <Alert variant="destructive" data-testid="login-error">
              <AlertDescription>{t(formErrorKey)}</AlertDescription>
            </Alert>
          )}

          <form className="space-y-3" onSubmit={handleSubmit} noValidate data-testid="login-form">
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
        </CardContent>
      </Card>
    </div>
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

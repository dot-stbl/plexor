import { useState, type ComponentProps } from 'react';
import { Eye, EyeSlash } from '@/shared/ui/icon';
import { Input } from '@/shared/ui/primitives/input';
import { Button } from '@/shared/ui/primitives/button';
import { cn } from '@/lib/utils';

/**
 * PasswordInput — поле пароля с переключателем видимости (глаз). Эталон YC.
 * Всё остальное — как у обычного `Input` (пропы прокидываются).
 */
export type PasswordInputProps = Omit<ComponentProps<'input'>, 'type'>;

export function PasswordInput({ className, ...props }: PasswordInputProps) {
  const [show, setShow] = useState(false);
  return (
    <div className="relative">
      <Input type={show ? 'text' : 'password'} className={cn('pr-8', className)} {...props} />
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        aria-label={show ? 'Hide password' : 'Show password'}
        onClick={() => setShow((s) => !s)}
        className="absolute inset-y-0 right-0.5 my-auto size-6 text-muted-foreground"
      >
        {show ? <EyeSlash className="size-3.5" /> : <Eye className="size-3.5" />}
      </Button>
    </div>
  );
}

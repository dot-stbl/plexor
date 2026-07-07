import { cn } from '@/shared/lib/utils';

import type { ComponentProps } from 'react';

/**
 * IP — IP address primitive (IPv4 default, IPv6 supported).
 *
 * Abstract: any IP display (VM NIC, subnet, gateway, peer address).
 * Renders as Plexor DS MonoNum (font-mono + tabular-nums) so columns
 * of IPs align visually.
 *
 * @example
 *   <IP value="10.1.2.3" />
 *   <IP value="fe80::1" muted />
 *   <IP value="10.0.0.1" link />
 */
export interface IPProps extends Omit<ComponentProps<'span'>, 'children'> {
  value: string;
  muted?: boolean;
  /** Render as inline link (anchor) — adds click affordance. */
  link?: boolean;
  /** For IPv4 only — split octets for column alignment. */
  group?: boolean;
}

function isIPv6(value: string): boolean {
  return value.includes(':');
}

export function IP({
  value,
  muted = false,
  link = false,
  group = true,
  className,
  ...props
}: IPProps) {
  const v6 = isIPv6(value);

  // Bare MonoNum — no link, no group, or IPv6 (grouping doesn't help)
  if (!link && (!group || v6)) {
    return (
      <span
        data-slot="ip"
        data-muted={muted || undefined}
        data-ipv6={v6 || undefined}
        className={cn(
          'inline-block font-mono tracking-tight tabular-nums leading-none',
          muted && 'text-muted-foreground',
          className,
        )}
        {...props}
      >
        {value}
      </span>
    );
  }

  // Link variant — wraps in anchor
  if (link) {
    return (
      <a
        href="#"
        data-slot="ip-link"
        onClick={(e) => e.preventDefault()}
        className={cn(
          'inline-block font-mono tabular-nums underline-offset-4 hover:underline',
          muted && 'text-muted-foreground',
          className,
        )}
        {...(props as ComponentProps<'a'>)}
      >
        {value}
      </a>
    );
  }

  // IPv4 with grouping — splits octets for column alignment
  const octets = value.split('.');
  return (
    <span
      data-slot="ip-grouped"
      className={cn('inline-block font-mono tabular-nums', className)}
      {...props}
    >
      {octets.map((octet, i) => (
        <span key={i}>
          {i > 0 && <span className="text-muted-foreground">.</span>}
          <span className={cn(muted && 'text-muted-foreground')}>{octet}</span>
        </span>
      ))}
    </span>
  );
}


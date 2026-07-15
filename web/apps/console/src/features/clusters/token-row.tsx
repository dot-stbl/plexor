import { useState } from 'react';
import { Block, CheckCircle, Close, ProgressActivity } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { revokeJoinToken } from './use-clusters';
import type { JoinToken } from './cluster-types';

const STATUS_LABEL: Record<JoinToken['status'], string> = {
  active: 'active',
  expired: 'expired',
  revoked: 'revoked',
};

const STATUS_ICON: Record<JoinToken['status'], React.ReactNode> = {
  active: <CheckCircle className="size-3" />,
  expired: <ProgressActivity className="size-3" />,
  revoked: <Block className="size-3" />,
};

interface TokenRowProps {
  clusterId: string;
  token: JoinToken;
}

export function TokenRow({ clusterId, token }: TokenRowProps) {
  const [revoked, setRevoked] = useState(false);

  // After revoke, we render a stripped row locally — no setState coupling
  // with the in-memory store, just a visual indicator.
  const displayStatus = revoked ? 'revoked' : token.status;

  return (
    <div className="flex items-center justify-between gap-3 p-3">
      <div className="min-w-0 space-y-0.5">
        <div className="text-sm font-medium">{token.label}</div>
        <p className="flex items-center gap-1.5 text-[10px] text-muted-foreground">
          <span>{token.intendedRole === 'control' ? 'control-plane' : 'compute'}</span>
          <span aria-hidden>·</span>
          <span>issued {new Date(token.issuedAt).toLocaleDateString('ru-RU')}</span>
          <span aria-hidden>·</span>
          <span>
            expires <MonoNum>{new Date(token.expiresAt).toLocaleDateString('ru-RU')}</MonoNum>
          </span>
          {token.redeemedByNodeId && (
            <>
              <span aria-hidden>·</span>
              <span>redeemed by <MonoNum>{token.redeemedByNodeId}</MonoNum></span>
            </>
          )}
        </p>
      </div>
      <div className="flex shrink-0 items-center gap-2">
        <span
          className={`inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-[10px] font-medium uppercase tracking-[0.06em] ${
            displayStatus === 'active'
              ? 'bg-ok-soft text-ok-ink'
              : displayStatus === 'revoked'
                ? 'bg-err-soft text-err-ink'
                : 'bg-idle-soft text-idle-ink'
          }`}
        >
          {STATUS_ICON[displayStatus]}
          {STATUS_LABEL[displayStatus]}
        </span>
        {displayStatus === 'active' && (
          <Button
            variant="ghost"
            size="icon"
            aria-label="Отозвать токен"
            onClick={() => {
              revokeJoinToken(clusterId, token.id);
              setRevoked(true);
            }}
          >
            <Close className="size-4" />
          </Button>
        )}
      </div>
    </div>
  );
}
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Block, CheckCircle, Close, ProgressActivity } from '@nine-thirty-five/material-symbols-react/rounded/700';
import { Button } from '@/shared/ui/primitives/button';
import { MonoNum } from '@/shared/ui/primitives/mono-num';
import { StatusPill } from '@/shared/ui/primitives/status-pill';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/shared/ui/primitives/alert-dialog';
import { revokeJoinToken } from '../api/use-clusters';
import type { JoinToken } from '../model/cluster-types';
import { tokenStatusLabelKey } from '../model/node-status';

const STATUS_VARIANT: Record<JoinToken['status'], 'running' | 'pending' | 'err'> = {
  active: 'running',
  expired: 'pending',
  revoked: 'err',
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

/** One join token: label, scope, validity window + revoke (confirmed). */
export function TokenRow({ clusterId, token }: TokenRowProps) {
  const { t } = useTranslation();
  const [revoked, setRevoked] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);

  // After revoke, we render a stripped row locally — no setState coupling
  // with the in-memory store, just a visual indicator.
  const displayStatus = revoked ? 'revoked' : token.status;

  const confirmRevoke = () => {
    setConfirmOpen(false);
    revokeJoinToken(clusterId, token.id);
    setRevoked(true);
  };

  return (
    <div className="flex items-center justify-between gap-3 p-3">
      <div className="min-w-0 space-y-0.5">
        <div className="text-sm font-medium">{token.label}</div>
        <p className="flex items-center gap-1.5 text-[10px] text-muted-foreground">
          <span>{t(token.intendedRole === 'control' ? 'clusters.node.role.control' : 'clusters.node.role.compute')}</span>
          <span className="inline-block h-2.5 w-px bg-border" aria-hidden />
          <span>
            {t('clusters.token.issued')} {new Date(token.issuedAt).toLocaleDateString()}
          </span>
          <span className="inline-block h-2.5 w-px bg-border" aria-hidden />
          <span>
            {t('clusters.token.expires')}{' '}
            <MonoNum>{new Date(token.expiresAt).toLocaleDateString()}</MonoNum>
          </span>
          {token.redeemedByNodeId && (
            <>
              <span className="inline-block h-2.5 w-px bg-border" aria-hidden />
              <span>
                {t('clusters.token.redeemedBy')} <MonoNum>{token.redeemedByNodeId}</MonoNum>
              </span>
            </>
          )}
        </p>
      </div>
      <div className="flex shrink-0 items-center gap-2">
        <StatusPill variant={STATUS_VARIANT[displayStatus]} size="sm">
          <span className="inline-flex items-center gap-1">
            {STATUS_ICON[displayStatus]}
            {t(tokenStatusLabelKey(displayStatus))}
          </span>
        </StatusPill>
        {displayStatus === 'active' && (
          <Button
            variant="ghost"
            size="icon"
            aria-label={t('clusters.token.revoke')}
            onClick={() => setConfirmOpen(true)}
          >
            <Close className="size-4" />
          </Button>
        )}
      </div>

      <AlertDialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <AlertDialogContent size="default">
          <AlertDialogHeader>
            <AlertDialogTitle>{t('clusters.token.revokeTitle')}</AlertDialogTitle>
            <AlertDialogDescription>
              {t('clusters.token.revokeDescription', { label: token.label })}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={confirmRevoke}>
              {t('clusters.token.revoke')}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

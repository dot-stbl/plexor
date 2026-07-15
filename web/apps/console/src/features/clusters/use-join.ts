// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Hooks for the cluster / join API. Stand-in for kubb-generated hooks
// until plan/clusters ships. Read/write through the mock handlers in
// `src/mocks/cluster-join.ts` so the UI exercises a realistic
// fetch-shape contract end to end.
// ==========================================================================

import { useCallback, useState, useEffect } from 'react';
import type { JoinToken, NodeRole } from './cluster-types';
import {
  mockIssueToken,
  mockListTokens,
  mockRevokeToken,
} from '@/mocks/cluster-join';

/** List + mutate tokens for a cluster. Local component state — no
 *  global cache, so different screens can each have their own copy. */
export function useClusterJoin(clusterId: string) {
  const [tokens, setTokens] = useState<JoinToken[]>([]);

  const refresh = useCallback(() => {
    setTokens(mockListTokens(clusterId));
  }, [clusterId]);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const issueToken = useCallback(
    (args: { label: string; intendedRole: NodeRole; ttlDays?: number }) => {
      const token = mockIssueToken(clusterId, args);
      refresh();
      return token;
    },
    [clusterId, refresh],
  );

  const revokeToken = useCallback(
    (tokenId: string) => {
      mockRevokeToken(clusterId, tokenId);
      refresh();
    },
    [clusterId, refresh],
  );

  return { tokens, issueToken, revokeToken, refresh };
}

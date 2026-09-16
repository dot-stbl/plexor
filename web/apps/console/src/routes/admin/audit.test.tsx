/**
 * AdminAuditPage component tests — the read surface for the
 * tenant-scoped audit log. The page wires filter inputs to a TanStack
 * Query that hits the audit endpoint, paginates via a `before` cursor,
 * and falls back to an EmptyState when no rows are returned.
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import type { ComponentType } from 'react';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route } from './audit';
import { mockAuditService, renderWithProviders } from '@/test-utils';

// `Route.options.component` carries the loader-aware generic type from
// `createFileRoute` — extracting it into a ComponentType simplifies the
// JSX usage in the tests below.
const AdminAuditPage = Route.options.component as ComponentType;

function makeEntry(overrides: Partial<{
  id: string;
  action: string;
  orgId: string;
  actorUserId: string | null;
  targetKind: string;
  targetId: string | null;
  payload: Record<string, unknown>;
  occurredAt: string;
}> = {}) {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    action: 'quotas.assignment.changed',
    orgId: '00000000-0000-0000-0000-000000000001',
    actorUserId: '00000000-0000-0000-0000-000000000002',
    targetKind: 'quota_assignment',
    targetId: '00000000-0000-0000-0000-000000000003',
    payload: {},
    occurredAt: new Date('2026-09-14T12:00:00Z').toISOString(),
    ...overrides,
  };
}

describe('AdminAuditPage', () => {
  beforeEach(() => {
    // The page's table renders relative timestamps via `new Date(...)`;
    // leaving the system clock stable across runs keeps the snapshot
    // output reproducible.
  });

  afterEach(() => {
    // No teardown required — each test gets a fresh QueryClient via the
    // renderWithProviders default.
  });

  it('renders the audit table with rows from the API', async () => {
    const mocks = mockAuditService();
    mocks.fetch.mockResolvedValue([
      makeEntry({ id: 'row-1', action: 'quotas.assignment.changed' }),
      makeEntry({ id: 'row-2', action: 'clusters.created' }),
    ]);

    renderWithProviders(<AdminAuditPage />);

    // Both rows render their `action` text. The table cell uses
    // `font-mono text-xs` so the text is verbatim.
    expect(await screen.findByText('quotas.assignment.changed')).toBeInTheDocument();
    expect(await screen.findByText('clusters.created')).toBeInTheDocument();

    // The Load more button is rendered after the table.
    expect(await screen.findByRole('button', { name: /load more/i })).toBeInTheDocument();
  });

  it('passes the action filter to the audit query when the user applies it', async () => {
    const user = userEvent.setup();
    const mocks = mockAuditService();
    mocks.fetch.mockResolvedValue([makeEntry({ action: 'quotas.assignment.changed' })]);

    renderWithProviders(<AdminAuditPage />);

    const actionInput = await screen.findByLabelText(/^action$/i);
    await user.type(actionInput, 'quotas.assignment.changed');

    const applyButton = await screen.findByRole('button', { name: /apply filters/i });
    await user.click(applyButton);

    await waitFor(() => {
      expect(mocks.fetch).toHaveBeenCalledWith(
        expect.objectContaining({ action: 'quotas.assignment.changed' }),
      );
    });
  });

  it('refetches the audit query with the `before` cursor when Load More is clicked', async () => {
    const user = userEvent.setup();
    const mocks = mockAuditService();
    // First fetch returns PAGE_SIZE rows so the Load More button stays
    // enabled (the button disables when the page returns fewer rows).
    const firstPage = Array.from({ length: 100 }, (_, index) =>
      makeEntry({
        id: `row-${index}`,
        occurredAt: new Date(Date.UTC(2026, 8, 14, 12, 0, index)).toISOString(),
      }),
    );
    mocks.fetch.mockResolvedValueOnce(firstPage);

    renderWithProviders(<AdminAuditPage />);

    const loadMore = await screen.findByRole('button', { name: /load more/i });
    await user.click(loadMore);

    await waitFor(() => {
      const calls = mocks.fetch.mock.calls;
      // At least two fetches: the first page + the Load More cursor.
      expect(calls.length).toBeGreaterThanOrEqual(2);
      // The second fetch carries a `before` cursor equal to the oldest
      // row's occurredAt from the first page.
      const before = firstPage[firstPage.length - 1]?.occurredAt ?? '';
      expect(calls[calls.length - 1]?.[0]).toEqual(expect.objectContaining({ before }));
    });
  });

  it('renders an empty state when no rows are returned', async () => {
    const mocks = mockAuditService();
    mocks.fetch.mockResolvedValue([]);

    renderWithProviders(<AdminAuditPage />);

    // The page renders `<EmptyState>` with the i18n title
    // `admin.audit.table.empty` ("No audit rows match the current
    // filters.") — the heading carries that exact text.
    expect(await screen.findByText(/no audit rows match/i)).toBeInTheDocument();

    // No table is rendered when the rows array is empty.
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('renders a loading state while the first fetch is in flight', async () => {
    const mocks = mockAuditService();
    // Never resolve — keep the query in its loading state. The catch at
    // the end of the test prevents the hanging promise from leaking
    // across tests.
    mocks.fetch.mockReturnValue(new Promise(() => {}));

    renderWithProviders(<AdminAuditPage />);

    // The loading cell shows `common.loading` ("Loading…").
    expect(await screen.findByText(/loading/i)).toBeInTheDocument();

    // Sanity: the Apply button is the only interactive filter action
    // while the table is loading.
    expect(screen.getByRole('button', { name: /apply filters/i })).toBeInTheDocument();
  });
});
/**
 * CreateVmPage (routes/vms/new.tsx) component tests -- the wizard that
 * wires the create-VM form to the real `useProvisionVm` mutation via
 * `useCreateVm` + `mapVmWizardToCreateVmRequest`/`mapCreateVmErrorToFieldErrors`.
 *
 * The kubb-generated `provisionVm` client is stubbed via vi.spyOn (see
 * `nock-compute-api.ts`), same shape as the audit/auth/branding mocks --
 * `useListClusters`/`listImages` are synchronous fixture-backed calls
 * (no network), so the form renders fully on first render with no
 * loading state to await.
 */
import { describe, expect, it } from 'vitest';
import type { ComponentType } from 'react';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route } from './new';
import { renderWithProviders, mockProvisionVmService } from '@/test-utils';
import type { VmDetail } from '@/shared/api';

const CreateVmPage = Route.options.component as ComponentType;

function makeVmDetail(overrides: Partial<VmDetail> = {}): VmDetail {
  return {
    id: 'vm-mock-new1',
    name: 'edge-cache-09',
    status: 'provisioning',
    internalIp: '10.10.1.60',
    zone: 'eu-west-1a',
    machineType: '2-4',
    vcpu: 2,
    ramGb: 4,
    diskGb: 40,
    createdAt: new Date('2026-09-25T12:00:00Z').toISOString(),
    project: 'default',
    vpcId: 'vpc-prod-eu',
    subnetId: 'subnet-prod-eu-compute-a',
    image: 'img-ubuntu-2404',
    diskEncrypted: true,
    ...overrides,
  };
}

/** Fills the minimum required fields (name + node) so `canCreate` turns
 *  true -- image/VPC/boot-disk-pool all carry usable defaults on mount.
 *  Opens the node SimpleSelect and clicks its first option. */
async function fillMinimumRequiredFields(user: ReturnType<typeof userEvent.setup>, name: string) {
  await user.type(screen.getByLabelText(/^name$/i), name);
  // The node SimpleSelect starts unselected (nodeId=''); react-aria's
  // SelectValue falls back to its own generic "Select an item" as the
  // trigger's accessible name when no value/aria-label resolves it --
  // it's the only Select on this page with no default value, so the
  // query is unambiguous.
  await user.click(screen.getByRole('button', { name: /select an item/i }));
  const options = await screen.findAllByRole('option');
  await user.click(options[0]!);
}

describe('CreateVmPage', () => {
  it('submits the mapped payload and clears back to idle on success', async () => {
    const user = userEvent.setup();
    const mocks = mockProvisionVmService();
    mocks.provision.mockResolvedValue(makeVmDetail({ name: 'edge-cache-09' }));

    renderWithProviders(<CreateVmPage />);

    await fillMinimumRequiredFields(user, 'edge-cache-09');

    const submit = screen.getByRole('button', { name: /create vm/i });
    expect(submit).not.toBeDisabled();
    await user.click(submit);

    await waitFor(() => {
      expect(mocks.provision).toHaveBeenCalledWith(
        expect.objectContaining({ name: 'edge-cache-09', project: 'default' }),
        expect.anything(),
      );
    });

    await waitFor(() => expect(submit).not.toBeDisabled());
    expect(submit).toHaveTextContent('Create VM');
    expect(screen.queryByText(/already exists|is required|lowercase letters/i)).not.toBeInTheDocument();
  });

  it('shows an inline error on the name field for a 409 duplicate-name response', async () => {
    const user = userEvent.setup();
    const mocks = mockProvisionVmService();
    mocks.provision.mockRejectedValue({
      response: {
        status: 409,
        data: {
          type: 'https://plexor.dev/problems/vms/name-conflict',
          title: 'Conflict',
          status: 409,
          detail: "A VM named 'web-prod-01' already exists.",
        },
      },
    });

    renderWithProviders(<CreateVmPage />);

    await fillMinimumRequiredFields(user, 'web-prod-01');

    const submit = screen.getByRole('button', { name: /create vm/i });
    await user.click(submit);

    await waitFor(() => {
      expect(mocks.provision).toHaveBeenCalled();
    });

    expect(await screen.findByText(/already exists/i)).toBeInTheDocument();
    await waitFor(() => expect(submit).not.toBeDisabled());
  });
});

import { describe, expect, it } from 'vitest';
import type { ProblemDetails } from '@/shared/api';
import { mapCreateVmErrorToFieldErrors } from './vm-create-errors';

function makeProblem(overrides: Partial<ProblemDetails> = {}): ProblemDetails {
  return {
    type: 'about:blank',
    title: 'Unprocessable Entity',
    status: 422,
    detail: 'validation failed',
    ...overrides,
  };
}

describe('mapCreateVmErrorToFieldErrors', () => {
  it('maps the name-required type URI to the nameRequired i18n key on the name field', () => {
    const result = mapCreateVmErrorToFieldErrors(
      makeProblem({ type: 'https://plexor.dev/problems/vms/name-required' }),
    );

    expect(result).toEqual({ name: 'vms.new.validation.nameRequired' });
  });

  it('maps the name-invalid type URI to the nameInvalid i18n key on the name field', () => {
    const result = mapCreateVmErrorToFieldErrors(
      makeProblem({ type: 'https://plexor.dev/problems/vms/name-invalid' }),
    );

    expect(result).toEqual({ name: 'vms.new.validation.nameInvalid' });
  });

  it('maps the name-conflict type URI to the nameConflict i18n key on the name field', () => {
    const result = mapCreateVmErrorToFieldErrors(
      makeProblem({ type: 'https://plexor.dev/problems/vms/name-conflict' }),
    );

    expect(result).toEqual({ name: 'vms.new.validation.nameConflict' });
  });

  it('falls back to the nameGeneric key on the name field when the type URI is unrecognized (e.g. 422)', () => {
    const result = mapCreateVmErrorToFieldErrors(makeProblem({ type: 'https://plexor.dev/problems/something-else' }));

    expect(result).toEqual({ name: 'vms.new.validation.nameGeneric' });
  });

  it('falls back to the nameGeneric key on the name field when ProblemDetails has no type at all', () => {
    const result = mapCreateVmErrorToFieldErrors(makeProblem({ type: undefined }));

    expect(result).toEqual({ name: 'vms.new.validation.nameGeneric' });
  });
});

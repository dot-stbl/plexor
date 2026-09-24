import type { ProblemDetails } from '@/shared/api';

/** The only wizard field the create-VM contract can currently return a
 *  validation error for is `name` (required, format, and duplicate-name
 *  conflicts) -- see `shared/api/mocks/handlers.ts`'s `provisionVmHandler`.
 *  Extend this union + the lookup table below when the mock/contract
 *  grows more validated fields. */
export type VmCreateFieldErrors = Partial<Record<'name', string>>;

/** RFC 7807 `type` URI -> { field, i18n key }. The OpenAPI contract's
 *  `ProblemDetails` has no structured per-field `errors` map (checked
 *  `contracts/plexor.openapi.yaml` -- just type/title/status/detail/
 *  instance), so the mock backend's `type` URI is the field
 *  discriminator; a real backend should keep emitting the same `type`
 *  values for these cases so this mapper doesn't need to sniff
 *  `detail` free text. */
const TYPE_TO_FIELD_ERROR: Readonly<Record<string, { field: 'name'; key: string }>> = {
  'https://plexor.dev/problems/vms/name-required': { field: 'name', key: 'vms.new.validation.nameRequired' },
  'https://plexor.dev/problems/vms/name-invalid': { field: 'name', key: 'vms.new.validation.nameInvalid' },
  'https://plexor.dev/problems/vms/name-conflict': { field: 'name', key: 'vms.new.validation.nameConflict' },
};

/** Maps a create-VM `ProblemDetails` response to a per-field i18n error
 *  key the wizard can show inline. Only meaningful for 409/422
 *  responses -- call this only when the mutation's error status is 409
 *  or 422; anything else (5xx, network failure) should go to a toast
 *  instead, not through this mapper. Always resolves to the `name`
 *  field: it's the only field the contract validates today, so an
 *  unrecognized `type` on a 409/422 still surfaces on `name` (a
 *  generic message) rather than vanishing silently. */
export function mapCreateVmErrorToFieldErrors(problem: ProblemDetails): VmCreateFieldErrors {
  const mapped = problem.type ? TYPE_TO_FIELD_ERROR[problem.type] : undefined;
  if (mapped) {
    return { [mapped.field]: mapped.key };
  }
  return { name: 'vms.new.validation.nameGeneric' };
}

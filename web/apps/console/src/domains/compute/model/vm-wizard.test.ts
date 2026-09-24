import { describe, expect, it } from 'vitest';
import { mapVmWizardToCreateVmRequest, type VmWizardValues } from './vm-wizard';

const GIB = 1024 ** 3;

function baseValues(overrides: Partial<VmWizardValues> = {}): VmWizardValues {
  return {
    name: 'web-prod-01',
    imageId: 'img-ubuntu-22-04',
    vpc: 'prod-vpc',
    sockets: 2,
    cores: 4,
    ramBytes: 16 * GIB,
    bootDiskBytes: 64 * GIB,
    labels: [],
    ...overrides,
  };
}

describe('mapVmWizardToCreateVmRequest', () => {
  it('converts vCPU = sockets * cores, RAM bytes -> GiB, disk bytes -> GiB, and formats machineType as "<vcpu>-<ramGb>"', () => {
    const result = mapVmWizardToCreateVmRequest(baseValues({ sockets: 2, cores: 4, ramBytes: 16 * GIB }));

    expect(result.vcpu).toBe(8);
    expect(result.ramGb).toBe(16);
    expect(result.diskGb).toBe(64);
    expect(result.machineType).toBe('8-16');
  });

  it('rounds fractional GiB inputs to the nearest integer (Math.round)', () => {
    const result = mapVmWizardToCreateVmRequest(
      baseValues({ ramBytes: Math.round(4.6 * GIB), bootDiskBytes: Math.round(10.4 * GIB) }),
    );

    expect(result.ramGb).toBe(5);
    expect(result.diskGb).toBe(10);
  });

  it('passes the image field through verbatim from imageId', () => {
    const result = mapVmWizardToCreateVmRequest(baseValues({ imageId: 'img-debian-12' }));

    expect(result.image).toBe('img-debian-12');
  });

  it('maps "prod-vpc" selection to the prod vpcId + subnetId', () => {
    const result = mapVmWizardToCreateVmRequest(baseValues({ vpc: 'prod-vpc' }));

    expect(result.vpcId).toBe('vpc-prod-eu');
    expect(result.subnetId).toBe('subnet-prod-eu-compute-a');
  });

  it('maps "staging-vpc" selection to the staging vpcId + subnetId', () => {
    const result = mapVmWizardToCreateVmRequest(baseValues({ vpc: 'staging-vpc' }));

    expect(result.vpcId).toBe('vpc-staging-eu');
    expect(result.subnetId).toBe('subnet-staging-eu-a');
  });

  it('falls back to the prod-vpc network when the selection is unknown', () => {
    const result = mapVmWizardToCreateVmRequest(baseValues({ vpc: 'mars-vpc' }));

    expect(result.vpcId).toBe('vpc-prod-eu');
    expect(result.subnetId).toBe('subnet-prod-eu-compute-a');
  });

  it('returns tags = undefined when there are no label rows', () => {
    const result = mapVmWizardToCreateVmRequest(baseValues({ labels: [] }));

    expect(result.tags).toBeUndefined();
  });

  it('drops blank label rows and keeps value-less keys as bare tags, key+value as "key=value"', () => {
    const result = mapVmWizardToCreateVmRequest(
      baseValues({
        labels: [
          { key: '', value: '' },
          { key: '  ', value: '   ' },
          { key: 'env', value: 'prod' },
          { key: 'owner', value: '' },
        ],
      }),
    );

    expect(result.tags).toEqual(['env=prod', 'owner']);
  });

  it('always sets project to "default"', () => {
    const result = mapVmWizardToCreateVmRequest(baseValues());

    expect(result.project).toBe('default');
  });

  it('trims the VM name before sending it to the contract', () => {
    const result = mapVmWizardToCreateVmRequest(baseValues({ name: '  web-prod-01  ' }));

    expect(result.name).toBe('web-prod-01');
  });
});

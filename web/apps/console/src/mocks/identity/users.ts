/**
 * User / service-account roster — the identity bounded context. Small,
 * real roster so audit entries can reference an actual actor id instead
 * of an arbitrary string. `identity/auth.ts`'s `DEV_SESSION` describes
 * the same operator as `USERS[0]` (kept separate on purpose — session
 * shape vs. directory shape are different concerns).
 */
export interface MockUser {
  id: string;
  displayName: string;
  email: string;
  roles: string[];
  kind: 'human' | 'service';
}

export const USERS: readonly MockUser[] = [
  { id: 'user-dev-1', displayName: 'Alexey Sergeev', email: 'a.sergeev@plexor.local', roles: ['viewer', 'operator'], kind: 'human' },
  { id: 'user-ops-2', displayName: 'Marta Novak', email: 'm.novak@plexor.local', roles: ['viewer'], kind: 'human' },
  { id: 'service-control-plane', displayName: 'Control Plane', email: 'control-plane@plexor.local', roles: ['service'], kind: 'service' },
  { id: 'service-iam', displayName: 'IAM Service', email: 'iam@plexor.local', roles: ['service'], kind: 'service' },
];

export const USER_BY_ID: ReadonlyMap<string, MockUser> = new Map(USERS.map((u) => [u.id, u]));

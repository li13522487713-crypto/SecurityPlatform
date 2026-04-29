const appNamespace = 'atlas_app';
const legacyNamespace = 'atlas';
const defaultTenantId = '00000000-0000-0000-0000-000000000001';

function readStorage(kind: 'localStorage' | 'sessionStorage', key: string): string {
  if (typeof window === 'undefined') {
    return '';
  }

  try {
    return window[kind].getItem(key) ?? '';
  } catch {
    return '';
  }
}

function readFirst(...keys: Array<{ kind: 'localStorage' | 'sessionStorage'; key: string }>): string {
  for (const { kind, key } of keys) {
    const value = readStorage(kind, key);
    if (value) {
      return value;
    }
  }
  return '';
}

export function getLowcodeAccessToken(): string {
  return readFirst(
    { kind: 'sessionStorage', key: `${appNamespace}_access_token` },
    { kind: 'localStorage', key: `${appNamespace}_access_token` },
    { kind: 'sessionStorage', key: `${legacyNamespace}_access_token` },
    { kind: 'localStorage', key: `${legacyNamespace}_access_token` }
  );
}

export function getLowcodeTenantId(): string {
  return readFirst(
    { kind: 'localStorage', key: `${appNamespace}_tenant_id` },
    { kind: 'sessionStorage', key: `${appNamespace}_tenant_id` },
    { kind: 'localStorage', key: `${legacyNamespace}_tenant_id` },
    { kind: 'sessionStorage', key: `${legacyNamespace}_tenant_id` }
  ) || defaultTenantId;
}

export function getLowcodeUserId(): string {
  const profileJson = readFirst(
    { kind: 'sessionStorage', key: `${appNamespace}_auth_profile` },
    { kind: 'localStorage', key: `${appNamespace}_auth_profile` },
    { kind: 'sessionStorage', key: `${legacyNamespace}_auth_profile` },
    { kind: 'localStorage', key: `${legacyNamespace}_auth_profile` }
  );

  if (profileJson) {
    try {
      const profile = JSON.parse(profileJson) as { id?: string; userId?: string };
      if (profile.id) {
        return profile.id;
      }
      if (profile.userId) {
        return profile.userId;
      }
    } catch {
      return 'me';
    }
  }

  return readFirst(
    { kind: 'localStorage', key: `${appNamespace}_user_id` },
    { kind: 'sessionStorage', key: `${appNamespace}_user_id` },
    { kind: 'localStorage', key: `${legacyNamespace}_user_id` },
    { kind: 'sessionStorage', key: `${legacyNamespace}_user_id` }
  ) || 'me';
}

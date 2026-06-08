export const ADMIN_NET_ACCESS_TOKEN_KEY = 'access-token';
export const ADMIN_NET_REFRESH_TOKEN_KEY = `x-${ADMIN_NET_ACCESS_TOKEN_KEY}`;

function getStorage() {
  if (typeof window === 'undefined') {
    return undefined;
  }
  return window.localStorage;
}

function readHeader(headers: unknown, key: string) {
  const source = headers as Record<string, string | undefined>;
  const getter = headers as { get?: (...args: any[]) => null | string };
  return (
    getter.get?.(key) ??
    getter.get?.(key.toLowerCase()) ??
    source[key] ??
    source[key.toLowerCase()]
  );
}

function decodeJwtPayload(token: string): null | Record<string, unknown> {
  try {
    const [, payload] = token.split('.');
    if (!payload) {
      return null;
    }
    const normalized = payload.replaceAll('-', '+').replaceAll('_', '/');
    const json = decodeURIComponent(
      [...window.atob(normalized)]
        .map(
          (char) =>
            `%${(char.codePointAt(0) ?? 0).toString(16).padStart(2, '0')}`,
        )
        .join(''),
    );
    return JSON.parse(json);
  } catch {
    return null;
  }
}

export function isJwtExpired(token?: null | string) {
  if (!token || typeof window === 'undefined') {
    return false;
  }
  const payload = decodeJwtPayload(token);
  const exp = Number(payload?.exp);
  if (!Number.isFinite(exp)) {
    return false;
  }
  return Date.now() >= exp * 1000;
}

export function getStoredAccessToken() {
  return getStorage()?.getItem(ADMIN_NET_ACCESS_TOKEN_KEY) ?? null;
}

export function getStoredRefreshToken() {
  return getStorage()?.getItem(ADMIN_NET_REFRESH_TOKEN_KEY) ?? null;
}

export function persistAdminNetTokens(tokens: {
  accessToken?: null | string;
  refreshToken?: null | string;
}) {
  const storage = getStorage();
  if (!storage) {
    return;
  }
  if (tokens.accessToken) {
    storage.setItem(ADMIN_NET_ACCESS_TOKEN_KEY, tokens.accessToken);
  }
  if (tokens.refreshToken) {
    storage.setItem(ADMIN_NET_REFRESH_TOKEN_KEY, tokens.refreshToken);
  }
}

export function clearAdminNetTokens() {
  const storage = getStorage();
  storage?.removeItem(ADMIN_NET_ACCESS_TOKEN_KEY);
  storage?.removeItem(ADMIN_NET_REFRESH_TOKEN_KEY);
}

export function syncAdminNetTokensFromHeaders(headers: unknown) {
  const accessToken = readHeader(headers, ADMIN_NET_ACCESS_TOKEN_KEY);
  const refreshToken = readHeader(headers, ADMIN_NET_REFRESH_TOKEN_KEY);

  if (accessToken === 'invalid_token') {
    clearAdminNetTokens();
    return { invalid: true };
  }

  persistAdminNetTokens({ accessToken, refreshToken });
  return { accessToken, invalid: false, refreshToken };
}

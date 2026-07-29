import { beforeEach, describe, expect, it } from 'vitest';

import {
  ADMIN_NET_ACCESS_TOKEN_KEY,
  ADMIN_NET_REFRESH_TOKEN_KEY,
  getStoredAccessToken,
  getStoredRefreshToken,
  syncAdminNetTokensFromHeaders,
} from './token';

describe('syncAdminNetTokensFromHeaders', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('stores refreshed access and refresh tokens from plain headers', () => {
    const result = syncAdminNetTokensFromHeaders({
      [ADMIN_NET_ACCESS_TOKEN_KEY]: 'access-token-value',
      [ADMIN_NET_REFRESH_TOKEN_KEY]: 'refresh-token-value',
    });

    expect(result).toEqual({
      accessToken: 'access-token-value',
      invalid: false,
      refreshToken: 'refresh-token-value',
    });
    expect(getStoredAccessToken()).toBe('access-token-value');
    expect(getStoredRefreshToken()).toBe('refresh-token-value');
  });

  it('reads Admin.NET token headers case-insensitively', () => {
    syncAdminNetTokensFromHeaders(
      new Headers({
        'Access-Token': 'renewed-access-token',
        'X-Access-Token': 'renewed-refresh-token',
      }),
    );

    expect(getStoredAccessToken()).toBe('renewed-access-token');
    expect(getStoredRefreshToken()).toBe('renewed-refresh-token');
  });

  it('clears both tokens when the backend marks the token invalid', () => {
    window.localStorage.setItem(
      ADMIN_NET_ACCESS_TOKEN_KEY,
      'expired-access-token',
    );
    window.localStorage.setItem(
      ADMIN_NET_REFRESH_TOKEN_KEY,
      'expired-refresh-token',
    );

    expect(
      syncAdminNetTokensFromHeaders({
        [ADMIN_NET_ACCESS_TOKEN_KEY]: 'invalid_token',
      }),
    ).toEqual({ invalid: true });
    expect(getStoredAccessToken()).toBeNull();
    expect(getStoredRefreshToken()).toBeNull();
  });
});

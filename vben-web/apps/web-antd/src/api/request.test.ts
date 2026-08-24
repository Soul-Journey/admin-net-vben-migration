import { describe, expect, it } from 'vitest';

import { shouldReauthenticateForResponse } from './request';

describe('Admin.NET authentication response handling', () => {
  it('does not reauthenticate from the deliberate logout response', () => {
    expect(shouldReauthenticateForResponse('/sysAuth/logout', true)).toBe(false);
  });

  it('reauthenticates when a protected API reports an invalid token', () => {
    expect(
      shouldReauthenticateForResponse('/sysMenu/loginMenuTree', true),
    ).toBe(true);
  });

  it('ignores a delayed invalid response from the previous login session', () => {
    expect(
      shouldReauthenticateForResponse(
        '/sysMenu/loginMenuTree',
        true,
        'old-token',
        'new-token',
      ),
    ).toBe(false);
  });

  it('reauthenticates when the failed request belongs to the current session', () => {
    expect(
      shouldReauthenticateForResponse(
        '/sysMenu/loginMenuTree',
        true,
        'current-token',
        'current-token',
      ),
    ).toBe(true);
  });
});

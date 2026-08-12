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
});

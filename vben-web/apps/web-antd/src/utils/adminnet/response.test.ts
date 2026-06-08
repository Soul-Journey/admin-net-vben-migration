import { describe, expect, it } from 'vitest';

import { unwrapAdminNetResponse } from './response';

describe('unwrapAdminNetResponse', () => {
  it('returns result when Admin.NET response succeeds', () => {
    expect(
      unwrapAdminNetResponse({
        code: 200,
        result: { id: 1 },
        type: 'success',
      }),
    ).toEqual({ id: 1 });
  });

  it('passes through non-envelope responses', () => {
    expect(unwrapAdminNetResponse({ rows: [] })).toEqual({ rows: [] });
  });

  it('throws Admin.NET message when code is not successful', () => {
    expect(() =>
      unwrapAdminNetResponse({
        code: 500,
        message: 'failed',
      }),
    ).toThrow('failed');
  });
});

import { describe, expect, it } from 'vitest';

import {
  collectNavigablePaths,
  NO_ACCESS_PATH,
  resolveAccessibleHomePath,
} from './home-route';

describe('resolveAccessibleHomePath', () => {
  const routes = [
    {
      children: [
        { path: '/system/user' },
        { meta: { hideInMenu: true }, path: '/system/hidden' },
      ],
      path: '/system',
    },
    { path: '/platform/dict' },
  ];

  it('keeps the preferred page only when it is actually authorized', () => {
    expect(resolveAccessibleHomePath(routes, '/platform/dict')).toBe(
      '/platform/dict',
    );
  });

  it('uses the first authorized leaf when dashboard is not authorized', () => {
    expect(resolveAccessibleHomePath(routes, '/dashboard/home')).toBe(
      '/system/user',
    );
  });

  it('uses the no-access page when no business menu is available', () => {
    expect(
      resolveAccessibleHomePath(
        [{ meta: { hideInMenu: true }, path: '/hidden' }],
        '/dashboard/home',
      ),
    ).toBe(NO_ACCESS_PATH);
  });

  it('does not use an empty directory whose children are all hidden', () => {
    expect(
      resolveAccessibleHomePath([
        {
          children: [{ meta: { hideInMenu: true }, path: '/hidden' }],
          path: '/empty-directory',
        },
        { path: '/available' },
      ]),
    ).toBe('/available');
  });

  it('skips external and parameterized pages as automatic home pages', () => {
    expect(
      collectNavigablePaths([
        { meta: { link: 'https://example.com' }, path: '/docs' },
        { path: '/detail/:id' },
        { path: '/safe' },
      ]),
    ).toEqual(['/safe']);
  });
});

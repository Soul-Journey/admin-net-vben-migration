import { describe, expect, it } from 'vitest';

import { mapAdminNetMenusToVbenRoutes } from './menu-adapter';

describe('mapAdminNetMenusToVbenRoutes', () => {
  it('maps Admin.NET menu fields to Vben backend routes', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: 'home/index',
        id: 1,
        meta: {
          icon: 'ele-House',
          isAffix: true,
          isHide: false,
          isKeepAlive: true,
          title: 'Home',
        },
        name: 'Home',
        orderNo: 10,
        path: '/dashboard/workspace',
        type: 2,
      },
    ]);

    expect(route?.component).toBe('dashboard/workspace/index');
    expect(route?.meta?.icon).toBe('lucide:house');
    expect(route?.meta?.keepAlive).toBe(true);
    expect(route?.meta?.affixTab).toBe(true);
  });

  it('filters button and disabled menu records', () => {
    expect(
      mapAdminNetMenusToVbenRoutes([
        { id: 1, name: 'Button', path: '/button', type: 3 },
        { id: 2, name: 'Disabled', path: '/disabled', status: 2, type: 2 },
      ]),
    ).toEqual([]);
  });

  it('uses a safe placeholder for unported components', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: 'system/unknown/index',
        id: 3,
        meta: { title: 'Unknown' },
        name: 'Unknown',
        path: 'system/unknown',
        type: 2,
      },
    ]);

    expect(route?.component).toBe('adminnet/legacy-placeholder');
    expect(route?.path).toBe('/system/unknown');
  });
});

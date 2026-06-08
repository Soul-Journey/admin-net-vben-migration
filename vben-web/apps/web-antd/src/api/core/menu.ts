import type { RouteRecordStringComponent } from '@vben/types';

import type { AdminNetMenuItem } from '#/api/adminnet/types';

import { requestClient } from '#/api/request';
import { mapAdminNetMenusToVbenRoutes } from '#/utils/adminnet/menu-adapter';

export async function getAllMenusApi() {
  const menus = await requestClient.get<AdminNetMenuItem[]>(
    '/sysMenu/loginMenuTree',
  );
  return mapAdminNetMenusToVbenRoutes(menus) as RouteRecordStringComponent[];
}

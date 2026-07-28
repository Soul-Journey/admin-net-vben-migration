import type { RouteRecordStringComponent } from '@vben/types';

import type { AdminNetMenuItem } from '#/api/adminnet/types';

import { requestClient } from '#/api/request';
import { mapAdminNetMenusToVbenRoutes } from '#/utils/adminnet/menu-adapter';

export async function getAllMenusApi() {
  const menus = await requestClient.get<AdminNetMenuItem[]>(
    '/sysMenu/loginMenuTree',
  );
  const routes = mapAdminNetMenusToVbenRoutes(
    menus,
  ) as RouteRecordStringComponent[];
  appendJobDashboardRoute(routes);
  return routes;
}

function appendJobDashboardRoute(routes: RouteRecordStringComponent[]) {
  for (const route of routes) {
    const children = route.children ?? [];
    if (children.some((child) => child.path === '/platform/job')) {
      if (!children.some((child) => child.path === '/platform/job/dashboard')) {
        children.push({
          component: 'system/job/dashboard',
          meta: {
            activePath: '/platform/job',
            hideInMenu: true,
            icon: 'lucide:chart-no-axes-combined',
            keepAlive: true,
            title: '任务看板',
          },
          name: 'JobDashboard',
          path: '/platform/job/dashboard',
        });
        route.children = children;
      }
      return true;
    }
    if (appendJobDashboardRoute(children)) {
      return true;
    }
  }
  return false;
}

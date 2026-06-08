import type { RouteRecordStringComponent } from '@vben/types';

import type { AdminNetMenuItem } from '#/api/adminnet/types';

const MENU_TYPE_BUTTON = 3;
const STATUS_DISABLED = 2;

const legacyPlaceholder = 'adminnet/legacy-placeholder';

export const ADMIN_NET_COMPONENT_ALLOWLIST: Record<string, string> = {
  Layout: legacyPlaceholder,
  'approvalFlow/index': legacyPlaceholder,
  'elive/index': legacyPlaceholder,
  'home/index': 'dashboard/workspace/index',
  'home/notice/index': legacyPlaceholder,
  'layout/routerView/parent': legacyPlaceholder,
  'mqttx/index': legacyPlaceholder,
  'system/cache/index': legacyPlaceholder,
  'system/codeGen/index': legacyPlaceholder,
  'system/config/index': legacyPlaceholder,
  'system/database/index': legacyPlaceholder,
  'system/dict/index': 'system/dict/index',
  'system/file/index': legacyPlaceholder,
  'system/formDes/index': legacyPlaceholder,
  'system/infoSetting/index': legacyPlaceholder,
  'system/job/dashboard': legacyPlaceholder,
  'system/job/index': legacyPlaceholder,
  'system/ldap/index': legacyPlaceholder,
  'system/log/difflog/index': legacyPlaceholder,
  'system/log/exlog/index': legacyPlaceholder,
  'system/log/oplog/index': legacyPlaceholder,
  'system/log/vislog/index': legacyPlaceholder,
  'system/menu/index': 'system/menu/index',
  'system/notice/index': legacyPlaceholder,
  'system/onlineUser/index': legacyPlaceholder,
  'system/openAccess/index': legacyPlaceholder,
  'system/org/index': 'system/org/index',
  'system/plugin/index': legacyPlaceholder,
  'system/pos/index': 'system/pos/index',
  'system/print/index': legacyPlaceholder,
  'system/region/index': legacyPlaceholder,
  'system/role/index': 'system/role/index',
  'system/server/index': legacyPlaceholder,
  'system/stressTest/index': legacyPlaceholder,
  'system/template/index': legacyPlaceholder,
  'system/tenant/index': 'system/tenant/index',
  'system/update/index': legacyPlaceholder,
  'system/user/component/userCenter': legacyPlaceholder,
  'system/user/index': 'system/user/index',
  'system/userRegWay/index': legacyPlaceholder,
  'system/weChatPay/index': legacyPlaceholder,
  'system/weChatUser/index': legacyPlaceholder,
};

const ICON_MAP: Record<string, string> = {
  'ele-Avatar': 'lucide:user-round',
  'ele-Bell': 'lucide:bell',
  'ele-Calendar': 'lucide:calendar-days',
  'ele-ChatDotRound': 'lucide:message-circle',
  'ele-Connection': 'lucide:workflow',
  'ele-Cpu': 'lucide:cpu',
  'ele-Document': 'lucide:file-text',
  'ele-Files': 'lucide:files',
  'ele-Folder': 'lucide:folder',
  'ele-Grid': 'lucide:grid-2x2',
  'ele-House': 'lucide:house',
  'ele-HomeFilled': 'lucide:house',
  'ele-Menu': 'lucide:menu',
  'ele-Monitor': 'lucide:monitor',
  'ele-Operation': 'lucide:settings-2',
  'ele-Printer': 'lucide:printer',
  'ele-Setting': 'lucide:settings',
  'ele-Tickets': 'lucide:ticket',
  'ele-User': 'lucide:user',
  'ele-UserFilled': 'lucide:user-check',
};

function cleanPath(path?: null | string, fallback?: string) {
  const source = path?.trim() || fallback || '/adminnet/unnamed';
  const normalized = source.startsWith('/') ? source : `/${source}`;
  return normalized.replaceAll('//', '/').replace(/\/$/, '') || '/';
}

function cleanRouteName(item: AdminNetMenuItem, path: string) {
  const raw = item.name || item.meta?.title || path;
  return String(raw)
    .replaceAll(/[^A-Z_a-z0-9-]/g, '-')
    .replaceAll(/^-+|-+$/g, '')
    || `AdminNetRoute${item.id ?? Math.random().toString(36).slice(2)}`;
}

function normalizeComponent(component?: null | string) {
  const cleaned = component
    ?.replaceAll(/^\/?src\/views\//g, '')
    .replaceAll(/^\/?views\//g, '')
    .replaceAll(/^\//g, '')
    .replaceAll(/\.vue$/g, '')
    .trim();

  if (!cleaned) {
    return legacyPlaceholder;
  }

  return ADMIN_NET_COMPONENT_ALLOWLIST[cleaned] ?? legacyPlaceholder;
}

function normalizeIcon(icon?: null | string) {
  if (!icon) {
    return undefined;
  }
  return ICON_MAP[icon] ?? icon.replace(/^ele-/, 'lucide:');
}

function sortRoutes(routes: RouteRecordStringComponent[]) {
  return routes.toSorted((left, right) => {
    const leftOrder = Number(left.meta?.order ?? 0);
    const rightOrder = Number(right.meta?.order ?? 0);
    return leftOrder - rightOrder;
  });
}

function toRoute(item: AdminNetMenuItem): null | RouteRecordStringComponent {
  if (item.type === MENU_TYPE_BUTTON || item.status === STATUS_DISABLED) {
    return null;
  }

  const path = cleanPath(item.path, `/adminnet/${item.id ?? item.name}`);
  const children = (item.children ?? [])
    .map((child) => toRoute(child))
    .filter(Boolean) as RouteRecordStringComponent[];
  const title = item.meta?.title || item.name || path;
  const externalLink = item.meta?.isLink || undefined;

  return {
    children: sortRoutes(children),
    component: normalizeComponent(item.component),
    meta: {
      affixTab: item.meta?.isAffix,
      hideInMenu: item.meta?.isHide,
      icon: normalizeIcon(item.meta?.icon),
      iframeSrc: item.meta?.isIframe ? externalLink || path : undefined,
      keepAlive: item.meta?.isKeepAlive,
      link: item.meta?.isIframe ? undefined : externalLink,
      order: item.orderNo,
      title,
    },
    name: cleanRouteName(item, path),
    path,
    redirect: item.redirect || undefined,
  };
}

export function mapAdminNetMenusToVbenRoutes(
  menus: AdminNetMenuItem[] = [],
): RouteRecordStringComponent[] {
  return sortRoutes(
    menus
      .map((item) => toRoute(item))
      .filter(Boolean) as RouteRecordStringComponent[],
  );
}

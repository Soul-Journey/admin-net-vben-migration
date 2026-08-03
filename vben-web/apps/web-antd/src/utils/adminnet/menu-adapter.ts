import type { RouteRecordStringComponent } from '@vben/types';

import type { AdminNetMenuItem } from '#/api/adminnet/types';

const MENU_TYPE_BUTTON = 3;
const STATUS_DISABLED = 2;

const legacyPlaceholder = 'adminnet/legacy-placeholder';

export const ADMIN_NET_COMPONENT_ALLOWLIST: Record<string, string> = {
  Layout: legacyPlaceholder,
  'approvalFlow/index': 'approvalFlow/index',
  'about/index': 'about/index',
  'elive/index': legacyPlaceholder,
  'home/index': 'dashboard/workspace/index',
  'home/notice/index': 'home/notice/index',
  'layout/routerView/parent': legacyPlaceholder,
  'mqttx/index': legacyPlaceholder,
  'system/cache/index': 'system/cache/index',
  'system/codeGen/index': 'system/codeGen/index',
  'system/config/index': 'system/config/index',
  'system/database/index': 'system/database/index',
  'system/dict/index': 'system/dict/index',
  'system/file/index': 'system/file/index',
  'system/formDes/index': 'system/formDes/index',
  'system/infoSetting/index': 'system/infoSetting/index',
  'system/job/dashboard': 'system/job/dashboard',
  'system/job/index': 'system/job/index',
  'system/ldap/index': 'system/ldap/index',
  'system/log/difflog/index': 'system/log/difflog/index',
  'system/log/exlog/index': 'system/log/exlog/index',
  'system/log/oplog/index': 'system/log/oplog/index',
  'system/log/vislog/index': 'system/log/vislog/index',
  'system/menu/index': 'system/menu/index',
  'system/notice/index': 'system/notice/index',
  'system/onlineUser/index': legacyPlaceholder,
  'system/openAccess/index': 'system/openAccess/index',
  'system/org/index': 'system/org/index',
  'system/plugin/index': 'system/plugin/index',
  'system/pos/index': 'system/pos/index',
  'system/print/index': 'system/print/index',
  'system/region/index': 'system/region/index',
  'system/role/index': 'system/role/index',
  'system/server/index': 'system/server/index',
  'system/stressTest/index': 'system/stressTest/index',
  'system/template/index': 'system/template/index',
  'system/tenant/index': 'system/tenant/index',
  'system/update/index': 'system/update/index',
  'system/user/component/userCenter': 'system/user/component/userCenter',
  'system/user/index': 'system/user/index',
  'system/userRegWay/index': 'system/userRegWay/index',
  'system/weChatPay/index': 'system/weChatPay/index',
  'system/weChatUser/index': 'system/weChatUser/index',
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

const ROUTE_ICON_MAP: Record<string, string> = {
  '/about': 'lucide:info',
  '/dashboard': 'lucide:layout-dashboard',
  '/dashboard/home': 'lucide:house',
  '/dashboard/notice': 'lucide:mail',
  '/develop': 'lucide:code-xml',
  '/develop/api': 'lucide:braces',
  '/develop/codeGen': 'lucide:braces',
  '/develop/database': 'lucide:database',
  '/develop/formDes': 'lucide:panels-top-left',
  '/develop/stressTest': 'lucide:gauge',
  '/doc': 'lucide:book-open',
  '/doc/SqlSugar': 'lucide:database',
  '/doc/admin': 'lucide:book-open-check',
  '/doc/element': 'lucide:panels-top-left',
  '/doc/furion': 'lucide:blocks',
  '/log': 'lucide:scroll-text',
  '/log/difflog': 'lucide:file-diff',
  '/log/exlog': 'lucide:triangle-alert',
  '/log/oplog': 'lucide:clipboard-list',
  '/log/vislog': 'lucide:mouse-pointer-click',
  '/platform': 'lucide:sliders-horizontal',
  '/platform/approvalFlow': 'lucide:workflow',
  '/platform/cache': 'lucide:database-zap',
  '/platform/config': 'lucide:settings-2',
  '/platform/dict': 'lucide:book-open',
  '/platform/file': 'lucide:folder-open',
  '/platform/infoSetting': 'lucide:settings',
  '/platform/job': 'lucide:timer',
  '/platform/menu': 'lucide:list-tree',
  '/platform/openAccess': 'lucide:waypoints',
  '/platform/plugin': 'lucide:plug',
  '/platform/print': 'lucide:printer',
  '/platform/region': 'lucide:map',
  '/platform/regWay': 'lucide:user-round-plus',
  '/platform/server': 'lucide:monitor-cog',
  '/platform/template': 'lucide:files',
  '/platform/tenant': 'lucide:building-2',
  '/platform/update': 'lucide:refresh-cw',
  '/platform/wechatpay': 'lucide:wallet-cards',
  '/system': 'lucide:shield-check',
  '/system/ldap': 'lucide:network',
  '/system/notice': 'lucide:bell',
  '/system/org': 'lucide:building',
  '/system/pos': 'lucide:briefcase-business',
  '/system/role': 'lucide:users-round',
  '/system/user': 'lucide:user-round',
  '/system/userCenter': 'lucide:circle-user-round',
  '/system/weChatUser': 'lucide:messages-square',
};

const DEFAULT_MENU_ICON = 'lucide:circle-dot';

function cleanPath(path?: null | string, fallback?: string) {
  const source = path?.trim() || fallback || '/adminnet/unnamed';
  const normalized = source.startsWith('/') ? source : `/${source}`;
  return normalized.replaceAll('//', '/').replace(/\/$/, '') || '/';
}

function cleanRouteName(item: AdminNetMenuItem, path: string) {
  const raw = item.name || item.meta?.title || path;
  return (
    String(raw)
      .replaceAll(/[^A-Z_a-z0-9-]/g, '-')
      .replaceAll(/^-+|-+$/g, '') ||
    `AdminNetRoute${item.id ?? Math.random().toString(36).slice(2)}`
  );
}

function normalizeComponent(
  component?: null | string,
  localPageComponents?: ReadonlySet<string>,
) {
  const cleaned = component
    ?.replaceAll(/^\/?src\/views\//g, '')
    .replaceAll(/^\/?views\//g, '')
    .replaceAll(/^\//g, '')
    .replaceAll(/\.vue$/g, '')
    .trim();

  if (!cleaned) {
    return legacyPlaceholder;
  }

  return (
    ADMIN_NET_COMPONENT_ALLOWLIST[cleaned] ??
    (localPageComponents?.has(cleaned) ? cleaned : legacyPlaceholder)
  );
}

function normalizeIcon(icon: null | string | undefined, path: string) {
  const routeIcon = ROUTE_ICON_MAP[path];
  if (!icon) {
    return routeIcon ?? DEFAULT_MENU_ICON;
  }

  const mappedLegacyIcon = ICON_MAP[icon];
  if (mappedLegacyIcon) {
    return mappedLegacyIcon;
  }

  // Unmapped Element Plus names cannot be converted to Lucide by prefix alone.
  // Prefer the route-specific icon so legacy menu data never renders a blank slot.
  if (icon.startsWith('ele-')) {
    return routeIcon ?? DEFAULT_MENU_ICON;
  }

  return icon.includes(':') ? icon : (routeIcon ?? DEFAULT_MENU_ICON);
}

function normalizeExternalLink(link?: null | string) {
  const value = link?.trim();
  if (!value) return undefined;
  try {
    const url = new URL(value);
    if (!['http:', 'https:'].includes(url.protocol)) return undefined;
    if (url.username || url.password) return undefined;
    return value;
  } catch {
    return undefined;
  }
}

function sortRoutes(routes: RouteRecordStringComponent[]) {
  return routes.toSorted((left, right) => {
    const leftOrder = Number(left.meta?.order ?? 0);
    const rightOrder = Number(right.meta?.order ?? 0);
    return leftOrder - rightOrder;
  });
}

function toRoute(
  item: AdminNetMenuItem,
  localPageComponents?: ReadonlySet<string>,
): null | RouteRecordStringComponent {
  if (item.type === MENU_TYPE_BUTTON || item.status === STATUS_DISABLED) {
    return null;
  }

  const path = cleanPath(item.path, `/adminnet/${item.id ?? item.name}`);
  const children = (item.children ?? [])
    .map((child) => toRoute(child, localPageComponents))
    .filter(Boolean) as RouteRecordStringComponent[];
  const title = item.meta?.title || item.name || path;
  const externalLink = normalizeExternalLink(item.meta?.isLink);

  return {
    children: sortRoutes(children),
    component: normalizeComponent(item.component, localPageComponents),
    meta: {
      affixTab: item.meta?.isAffix,
      hideInMenu: item.meta?.isHide,
      icon: normalizeIcon(item.meta?.icon, path),
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
  localPageComponents?: ReadonlySet<string>,
): RouteRecordStringComponent[] {
  return sortRoutes(
    menus
      .map((item) => toRoute(item, localPageComponents))
      .filter(Boolean) as RouteRecordStringComponent[],
  );
}

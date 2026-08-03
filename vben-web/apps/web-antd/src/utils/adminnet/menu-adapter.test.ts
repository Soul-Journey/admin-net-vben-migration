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

  it('adds a visual fallback icon without changing backend menu data', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: 'system/user/index',
        id: 20,
        meta: { title: '账号管理' },
        name: 'sysUser',
        path: '/system/user',
        type: 2,
      },
    ]);

    expect(route?.meta?.icon).toBe('lucide:user-round');
  });

  it('replaces unsupported legacy Element icons with semantic route icons', () => {
    const routes = mapAdminNetMenusToVbenRoutes([
      {
        component: 'system/role/index',
        id: 21,
        meta: { icon: 'ele-Help', title: '角色管理' },
        name: 'sysRole',
        path: '/system/role',
        type: 2,
      },
      {
        component: 'system/org/index',
        id: 22,
        meta: { icon: 'ele-OfficeBuilding', title: '机构管理' },
        name: 'sysOrg',
        path: '/system/org',
        type: 2,
      },
      {
        component: 'system/pos/index',
        id: 23,
        meta: { icon: 'ele-Mug', title: '职位管理' },
        name: 'sysPos',
        path: '/system/pos',
        type: 2,
      },
      {
        component: 'system/config/index',
        id: 26,
        meta: { icon: 'ele-DocumentCopy', title: '参数配置' },
        name: 'sysConfig',
        path: '/platform/config',
        type: 2,
      },
    ]);

    expect(routes.map((route) => route.meta?.icon)).toEqual([
      'lucide:users-round',
      'lucide:building',
      'lucide:briefcase-business',
      'lucide:settings-2',
    ]);
  });

  it('keeps valid Iconify values and gives unknown menus a visible default', () => {
    const routes = mapAdminNetMenusToVbenRoutes([
      {
        component: 'Layout',
        id: 24,
        meta: { icon: 'lucide:sparkles', title: '自定义菜单' },
        name: 'customIcon',
        path: '/custom/icon',
        type: 1,
      },
      {
        component: 'Layout',
        id: 25,
        meta: { icon: 'ele-UnknownIcon', title: '未知菜单' },
        name: 'unknownIcon',
        path: '/custom/unknown',
        type: 1,
      },
    ]);

    expect(routes.map((route) => route.meta?.icon)).toEqual([
      'lucide:sparkles',
      'lucide:circle-dot',
    ]);
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

  it('allows a generated component only when the file exists in the local Vben page map', () => {
    const [route] = mapAdminNetMenusToVbenRoutes(
      [
        {
          component: '/business/customer/index',
          id: 30,
          meta: { title: '客户管理' },
          name: 'customer',
          path: '/business/customer',
          type: 2,
        },
      ],
      new Set(['business/customer/index']),
    );

    expect(route?.component).toBe('business/customer/index');
  });

  it('routes the migrated template module to its Vben page', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: '/system/template/index',
        id: 4,
        meta: { title: '模板管理' },
        name: 'sysTemplate',
        path: '/platform/template',
        type: 2,
      },
    ]);

    expect(route?.component).toBe('system/template/index');
    expect(route?.path).toBe('/platform/template');
  });

  it('routes the migrated plugin module to its Vben page', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: '/system/plugin/index',
        id: 5,
        meta: { title: '动态插件' },
        name: 'sysPlugin',
        path: '/platform/plugin',
        type: 2,
      },
    ]);

    expect(route?.component).toBe('system/plugin/index');
    expect(route?.path).toBe('/platform/plugin');
  });

  it('routes the migrated open access module to its Vben page', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: '/system/openAccess/index',
        id: 6,
        meta: { title: '开放接口' },
        name: 'sysOpenAccess',
        path: '/platform/openAccess',
        type: 2,
      },
    ]);

    expect(route?.component).toBe('system/openAccess/index');
    expect(route?.path).toBe('/platform/openAccess');
  });

  it('routes the migrated system information module to its Vben page', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: '/system/infoSetting/index',
        id: 7,
        meta: { title: '系统配置' },
        name: 'sysInfoSetting',
        path: '/platform/infoSetting',
        type: 2,
      },
    ]);

    expect(route?.component).toBe('system/infoSetting/index');
    expect(route?.path).toBe('/platform/infoSetting');
  });

  it('routes the migrated WeChat Pay module to its Vben page', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: '/system/weChatPay/index',
        id: 8,
        meta: { title: '微信支付' },
        name: 'sysWechatPay',
        path: '/platform/wechatpay',
        type: 2,
      },
    ]);

    expect(route?.component).toBe('system/weChatPay/index');
    expect(route?.path).toBe('/platform/wechatpay');
  });

  it('routes the migrated system update module to its Vben page', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: '/system/update/index',
        id: 9,
        meta: { title: '系统更新' },
        name: 'sysUpdate',
        path: '/platform/update',
        type: 2,
      },
    ]);

    expect(route?.component).toBe('system/update/index');
    expect(route?.path).toBe('/platform/update');
  });

  it('routes the migrated stress test module to its Vben page', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: '/system/stressTest/index',
        id: 10,
        meta: { title: '接口压测' },
        name: 'sysStressTest',
        path: '/develop/stressTest',
        type: 2,
      },
    ]);

    expect(route?.component).toBe('system/stressTest/index');
    expect(route?.path).toBe('/develop/stressTest');
  });

  it('routes the migrated database module to its Vben page', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: '/system/database/index',
        id: 11,
        meta: { title: '库表管理' },
        name: 'sysDatabase',
        path: '/develop/database',
        type: 2,
      },
    ]);

    expect(route?.component).toBe('system/database/index');
    expect(route?.path).toBe('/develop/database');
  });

  it('routes the migrated code generation module to its Vben page', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: '/system/codeGen/index',
        id: 12,
        meta: { title: '代码生成' },
        name: 'sysCodeGen',
        path: '/develop/codeGen',
        type: 2,
      },
    ]);

    expect(route?.component).toBe('system/codeGen/index');
    expect(route?.path).toBe('/develop/codeGen');
  });

  it('routes the migrated form designer module to its Vben page', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: '/system/formDes/index',
        id: 13,
        meta: { title: '表单设计' },
        name: 'sysFormDesigner',
        path: '/develop/formDes',
        type: 2,
      },
    ]);

    expect(route?.component).toBe('system/formDes/index');
    expect(route?.path).toBe('/develop/formDes');
  });

  it('routes the migrated approval flow module to its Vben page', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: '/approvalFlow/index',
        id: 14,
        meta: { title: '审批流程' },
        name: 'approvalFlow',
        path: '/platform/approvalFlow',
        type: 2,
      },
    ]);

    expect(route?.component).toBe('approvalFlow/index');
    expect(route?.path).toBe('/platform/approvalFlow');
  });

  it('routes the received notice module to its Vben page', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: '/home/notice/index',
        id: 15,
        meta: { title: '站内信' },
        name: 'notice',
        path: '/dashboard/notice',
        type: 2,
      },
    ]);

    expect(route?.component).toBe('home/notice/index');
    expect(route?.path).toBe('/dashboard/notice');
  });

  it('routes the Admin.NET about module to its Vben page', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: '/about/index',
        id: 16,
        meta: { title: '关于项目' },
        name: 'about',
        path: '/about',
        type: 2,
      },
    ]);

    expect(route?.component).toBe('about/index');
    expect(route?.meta?.icon).toBe('lucide:info');
    expect(route?.path).toBe('/about');
  });

  it('preserves normal external links without embedding them', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: 'layout/routerView/link',
        id: 17,
        meta: {
          isIframe: false,
          isLink: 'https://example.com/docs',
          title: '外部文档',
        },
        name: 'externalDocs',
        path: '/doc/external',
        type: 2,
      },
    ]);

    expect(route?.meta?.link).toBe('https://example.com/docs');
    expect(route?.meta?.iframeSrc).toBeUndefined();
  });

  it('preserves trusted backend iframe routes', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: 'layout/routerView/iframe',
        id: 18,
        meta: {
          isIframe: true,
          isLink: 'http://localhost:5005',
          title: '系统接口',
        },
        name: 'systemApi',
        path: '/develop/api',
        type: 2,
      },
    ]);

    expect(route?.meta?.iframeSrc).toBe('http://localhost:5005');
    expect(route?.meta?.link).toBeUndefined();
  });

  it('rejects dangerous external-link protocols', () => {
    const [route] = mapAdminNetMenusToVbenRoutes([
      {
        component: 'layout/routerView/link',
        id: 19,
        meta: {
          isIframe: false,
          isLink: 'javascript:alert(1)',
          title: '危险链接',
        },
        name: 'unsafeLink',
        path: '/doc/unsafe',
        type: 2,
      },
    ]);

    expect(route?.meta?.link).toBeUndefined();
    expect(route?.meta?.iframeSrc).toBeUndefined();
  });
});

import {
  defineOverridesPreferences,
  definePreferencesExtension,
} from '@vben/preferences';

interface WebAntdPreferencesExtension {
  defaultTableSize: number;
  enableFormFullscreen: boolean;
  reportTitle: string;
  tenantMode: 'multi' | 'single';
}

export const overridesPreferences = defineOverridesPreferences({
  app: {
    accessMode: 'backend',
    compact: true,
    contentPadding: 12,
    defaultHomePath: '/dashboard/home',
    enableCheckUpdates: false,
    enableRefreshToken: false,
    loginExpiredMode: 'page',
    name: import.meta.env.VITE_APP_TITLE,
    watermark: true,
    watermarkContent: 'Admin.NET',
  },
  breadcrumb: {
    showHome: true,
  },
  copyright: {
    companyName: 'Admin.NET',
    companySiteLink: 'https://gitee.com/zuohuaijun/Admin.NET',
    date: '2026',
    enable: true,
  },
  navigation: {
    accordion: true,
    split: true,
    styleType: 'plain',
  },
  sidebar: {
    width: 232,
  },
  tabbar: {
    height: 34,
    styleType: 'brisk',
  },
  theme: {
    colorDestructive: 'hsl(350 72% 52%)',
    colorPrimary: 'hsl(217 92% 51%)',
    colorSuccess: 'hsl(158 64% 38%)',
    colorWarning: 'hsl(38 92% 50%)',
    fontSize: 14,
    mode: 'light',
    radius: '0.375',
  },
});

export const preferencesExtension =
  definePreferencesExtension<WebAntdPreferencesExtension>({
    tabLabel: 'preferences.antd.tabLabel',
    title: 'preferences.antd.title',
    fields: [
      {
        component: 'switch',
        defaultValue: true,
        key: 'enableFormFullscreen',
        label: 'preferences.antd.fields.enableFormFullscreen.label',
        tip: 'preferences.antd.fields.enableFormFullscreen.tip',
      },
      {
        component: 'select',
        defaultValue: 'single',
        key: 'tenantMode',
        label: 'preferences.antd.fields.tenantMode.label',
        options: [
          {
            label: 'preferences.antd.fields.tenantMode.options.single.label',
            value: 'single',
          },
          {
            label: 'preferences.antd.fields.tenantMode.options.multi.label',
            value: 'multi',
          },
        ],
      },
      {
        component: 'number',
        componentProps: {
          max: 200,
          min: 10,
          step: 10,
        },
        defaultValue: 20,
        key: 'defaultTableSize',
        label: 'preferences.antd.fields.defaultTableSize.label',
      },
      {
        component: 'input',
        defaultValue: '',
        key: 'reportTitle',
        label: 'preferences.antd.fields.reportTitle.label',
        placeholder: 'preferences.antd.fields.reportTitle.placeholder',
      },
    ],
  });

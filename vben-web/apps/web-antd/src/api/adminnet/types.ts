import type { RouteRecordStringComponent, UserInfo } from '@vben/types';

export interface AdminNetResult<T = unknown> {
  code?: number;
  errors?: unknown;
  extras?: unknown;
  message?: null | string;
  result?: T;
  time?: string;
  type?: null | string;
}

export interface AdminNetLoginParams {
  account: string;
  code?: null | string;
  codeId?: number;
  password: string;
  tenantId: number;
}

export interface AdminNetLoginResult {
  accessToken?: null | string;
  refreshToken?: null | string;
}

export interface AdminNetUserInfoRaw {
  account?: null | string;
  accountType?: number;
  address?: null | string;
  avatar?: null | string;
  buttons?: null | string[];
  email?: null | string;
  id?: number;
  idCardNum?: null | string;
  introduction?: null | string;
  orgId?: number;
  orgName?: null | string;
  orgType?: null | string;
  phone?: null | string;
  posName?: null | string;
  realName?: null | string;
  roleIds?: null | number[];
  signature?: null | string;
  tenantId?: null | number;
  watermarkText?: null | string;
}

export interface AdminNetMenuMeta {
  icon?: null | string;
  isAffix?: boolean;
  isHide?: boolean;
  isIframe?: boolean;
  isKeepAlive?: boolean;
  isLink?: null | string;
  title?: null | string;
}

export interface AdminNetMenuItem {
  children?: AdminNetMenuItem[] | null;
  component?: null | string;
  id?: number;
  meta?: AdminNetMenuMeta | null;
  name?: null | string;
  orderNo?: number;
  path?: null | string;
  permission?: null | string;
  pid?: number;
  redirect?: null | string;
  status?: number;
  type?: number;
}

export type AdminNetRoute = RouteRecordStringComponent;

export type AdminNetUserInfo = UserInfo & {
  accessCodes: string[];
  account?: string;
  accountType?: number;
  email?: string;
  orgId?: number;
  orgName?: string;
  phone?: string;
  posName?: string;
  roles: string[];
  tenantId?: null | number;
  watermarkText?: string;
};

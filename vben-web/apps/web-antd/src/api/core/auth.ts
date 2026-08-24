import type { AdminNetLoginParams } from '#/api/adminnet/types';

import { sm2 } from 'sm-crypto-v2';

import { requestClient } from '#/api/request';

export namespace AuthApi {
  export interface LoginParams {
    account?: string;
    code?: string;
    codeId?: number;
    password?: string;
    tenantId?: number;
    username?: string;
  }

  export interface PhoneLoginParams {
    code?: string;
    phone?: string;
    phoneNumber?: string;
    tenantId?: number;
  }

  export interface LoginResult {
    accessToken: string;
    refreshToken?: string;
  }
}

function encryptPassword(password: string) {
  const publicKey = import.meta.env.VITE_SM_PUBLIC_KEY;
  if (!publicKey) throw new Error('密码加密公钥未配置，已阻止明文登录');
  return sm2.doEncrypt(password, publicKey, 1);
}

function normalizeLoginParams(data: AuthApi.LoginParams): AdminNetLoginParams {
  return {
    account: data.account || data.username || '',
    code: data.code ?? null,
    codeId: data.codeId ?? 0,
    password: encryptPassword(data.password || ''),
    tenantId: data.tenantId ?? -1,
  };
}

function normalizePhoneLoginParams(data: AuthApi.PhoneLoginParams) {
  return {
    code: data.code || '',
    phone: data.phone || data.phoneNumber || '',
    tenantId: data.tenantId ?? -1,
  };
}

export async function loginApi(data: AuthApi.LoginParams) {
  return requestClient.post<AuthApi.LoginResult>(
    '/sysAuth/login',
    normalizeLoginParams(data),
  );
}

export async function loginPhoneApi(data: AuthApi.PhoneLoginParams) {
  return requestClient.post<AuthApi.LoginResult>(
    '/sysAuth/loginPhone',
    normalizePhoneLoginParams(data),
  );
}

export async function sendSmsCodeApi(phoneNumber: string) {
  await requestClient.post<unknown>(
    `/sysSms/sendSms/${encodeURIComponent(phoneNumber)}`,
  );
}

export async function logoutApi() {
  return requestClient.post('/sysAuth/logout', undefined, {
    responseReturn: 'body',
  });
}

export async function getAccessCodesApi() {
  return requestClient.get<string[]>('/sysMenu/ownBtnPermList');
}

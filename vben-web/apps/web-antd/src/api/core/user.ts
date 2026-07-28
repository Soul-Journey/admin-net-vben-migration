import type { UserInfo } from '@vben/types';

import type {
  AdminNetUserInfo,
  AdminNetUserInfoRaw,
} from '#/api/adminnet/types';

import { requestClient } from '#/api/request';

function mapAdminNetUserInfo(raw: AdminNetUserInfoRaw): AdminNetUserInfo {
  const roles = (raw.roleIds ?? []).map(String);
  const accessCodes = raw.buttons ?? [];

  return {
    accessCodes,
    account: raw.account ?? '',
    accountType: raw.accountType,
    avatar: raw.avatar || '/upload/logo.png',
    desc: raw.introduction ?? raw.signature ?? '',
    email: raw.email ?? '',
    homePath: '/dashboard/home',
    orgId: raw.orgId,
    orgName: raw.orgName ?? '',
    phone: raw.phone ?? '',
    posName: raw.posName ?? '',
    realName: raw.realName ?? raw.account ?? '',
    roles,
    tenantId: raw.tenantId,
    token: '',
    userId: String(raw.id ?? ''),
    username: raw.account ?? '',
    watermarkText: raw.watermarkText ?? '',
  };
}

export async function getUserInfoApi() {
  const raw = await requestClient.get<AdminNetUserInfoRaw>('/sysAuth/userInfo');
  return mapAdminNetUserInfo(raw) as UserInfo;
}

import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface PageWechatUserParams {
  mobile?: string;
  nickName?: string;
  page: number;
  pageSize: number;
}

export interface SysWechatUserRecord {
  avatar?: string;
  city?: string;
  country?: string;
  createTime?: string;
  createUserName?: string;
  id: number;
  language?: string;
  mobile?: string;
  nickName?: string;
  openId: string;
  platformType: number;
  province?: string;
  sex?: number;
  unionId?: string;
  updateTime?: string;
  updateUserName?: string;
  userId?: number;
}

export type SaveWechatUserParams = Omit<
  SysWechatUserRecord,
  | 'createTime'
  | 'createUserName'
  | 'id'
  | 'updateTime'
  | 'updateUserName'
  | 'userId'
> & { id?: number };

function toRecord(value: unknown): RawRecord {
  return value && typeof value === 'object' ? (value as RawRecord) : {};
}

function toNumber(value: unknown) {
  if (typeof value === 'number') return value;
  if (typeof value === 'string' && value.trim()) {
    const result = Number(value);
    return Number.isNaN(result) ? undefined : result;
  }
  return undefined;
}

function toStringValue(value: unknown) {
  return typeof value === 'string' ? value : undefined;
}

function normalizeWechatUser(value: unknown): SysWechatUserRecord {
  const item = toRecord(value);
  return {
    avatar: toStringValue(item.avatar ?? item.Avatar),
    city: toStringValue(item.city ?? item.City),
    country: toStringValue(item.country ?? item.Country),
    createTime: toStringValue(item.createTime ?? item.CreateTime),
    createUserName: toStringValue(item.createUserName ?? item.CreateUserName),
    id: toNumber(item.id ?? item.Id) ?? 0,
    language: toStringValue(item.language ?? item.Language),
    mobile: toStringValue(item.mobile ?? item.Mobile),
    nickName: toStringValue(item.nickName ?? item.NickName),
    openId: toStringValue(item.openId ?? item.OpenId) ?? '',
    platformType: toNumber(item.platformType ?? item.PlatformType) ?? 1,
    province: toStringValue(item.province ?? item.Province),
    sex: toNumber(item.sex ?? item.Sex),
    unionId: toStringValue(item.unionId ?? item.UnionId),
    updateTime: toStringValue(item.updateTime ?? item.UpdateTime),
    updateUserName: toStringValue(item.updateUserName ?? item.UpdateUserName),
    userId: toNumber(item.userId ?? item.UserId),
  };
}

export async function pageWechatUsersApi(params: PageWechatUserParams) {
  const data = await requestClient.post<AdminNetPagedList<unknown>>(
    '/sysWechatUser/page',
    params,
  );
  return {
    ...data,
    items: Array.isArray(data.items)
      ? data.items.map((item) => normalizeWechatUser(item))
      : [],
  } as AdminNetPagedList<SysWechatUserRecord>;
}

export function addWechatUserApi(params: SaveWechatUserParams) {
  return requestClient.post<unknown>('/sysWechatUser/add', params);
}

export function updateWechatUserApi(params: SaveWechatUserParams) {
  return requestClient.post<unknown>('/sysWechatUser/update', params);
}

export function deleteWechatUserApi(id: number) {
  return requestClient.post<unknown>('/sysWechatUser/delete', { id });
}

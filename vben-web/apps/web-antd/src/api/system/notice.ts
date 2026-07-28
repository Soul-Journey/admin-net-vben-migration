import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface PageNoticeParams {
  page: number;
  pageSize: number;
  title?: string;
  type?: number;
}

export interface SysNoticeRecord {
  cancelTime?: string;
  content: string;
  createTime?: string;
  createUserId?: number;
  createUserName?: string;
  id: number;
  publicTime?: string;
  publicUserName?: string;
  status?: number;
  title: string;
  type?: number;
  updateTime?: string;
  updateUserName?: string;
}

export interface ReceivedNoticeRecord {
  id: number;
  notice: SysNoticeRecord;
  noticeId: number;
  readStatus: number;
  readTime?: string;
  userId: number;
}

export type SaveNoticeParams = Partial<SysNoticeRecord> & {
  content: string;
  title: string;
  type: number;
};

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

function normalizeNotice(value: unknown): SysNoticeRecord {
  const item = toRecord(value);
  return {
    cancelTime: toStringValue(item.cancelTime ?? item.CancelTime),
    content: toStringValue(item.content ?? item.Content) ?? '',
    createTime: toStringValue(item.createTime ?? item.CreateTime),
    createUserId: toNumber(item.createUserId ?? item.CreateUserId),
    createUserName: toStringValue(item.createUserName ?? item.CreateUserName),
    id: toNumber(item.id ?? item.Id) ?? 0,
    publicTime: toStringValue(item.publicTime ?? item.PublicTime),
    publicUserName: toStringValue(item.publicUserName ?? item.PublicUserName),
    status: toNumber(item.status ?? item.Status),
    title: toStringValue(item.title ?? item.Title) ?? '',
    type: toNumber(item.type ?? item.Type),
    updateTime: toStringValue(item.updateTime ?? item.UpdateTime),
    updateUserName: toStringValue(item.updateUserName ?? item.UpdateUserName),
  };
}

function normalizeReceivedNotice(value: unknown): ReceivedNoticeRecord {
  const item = toRecord(value);
  return {
    id: toNumber(item.id ?? item.Id) ?? 0,
    notice: normalizeNotice(item.sysNotice ?? item.SysNotice),
    noticeId: toNumber(item.noticeId ?? item.NoticeId) ?? 0,
    readStatus: toNumber(item.readStatus ?? item.ReadStatus) ?? 0,
    readTime: toStringValue(item.readTime ?? item.ReadTime),
    userId: toNumber(item.userId ?? item.UserId) ?? 0,
  };
}

export async function pageNoticesApi(params: PageNoticeParams) {
  const data = await requestClient.post<AdminNetPagedList<unknown>>(
    '/sysNotice/page',
    params,
  );
  return {
    ...data,
    items: Array.isArray(data.items)
      ? data.items.map((item) => normalizeNotice(item))
      : [],
  } as AdminNetPagedList<SysNoticeRecord>;
}

export async function pageReceivedNoticesApi(params: PageNoticeParams) {
  const data = await requestClient.post<AdminNetPagedList<unknown>>(
    '/sysNotice/pageReceived',
    params,
  );
  return {
    ...data,
    items: Array.isArray(data.items)
      ? data.items.map((item) => normalizeReceivedNotice(item))
      : [],
  } as AdminNetPagedList<ReceivedNoticeRecord>;
}

export function setNoticeReadApi(id: number) {
  return requestClient.post<unknown>('/sysNotice/setRead', { id });
}

export function addNoticeApi(params: SaveNoticeParams) {
  return requestClient.post<unknown>('/sysNotice/add', params);
}

export function updateNoticeApi(params: SaveNoticeParams & { id: number }) {
  return requestClient.post<unknown>('/sysNotice/update', params);
}

export function deleteNoticeApi(id: number) {
  return requestClient.post<unknown>('/sysNotice/delete', { id });
}

export function publishNoticeApi(id: number) {
  return requestClient.post<unknown>('/sysNotice/public', { id });
}

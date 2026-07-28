import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface TemplateQuery {
  code?: string;
  groupName?: string;
  name?: string;
  page: number;
  pageSize: number;
  type?: number;
}

export interface SysTemplateRecord {
  code: string;
  content: string;
  createTime?: string;
  createUserName?: string;
  groupName: string;
  id: number;
  name: string;
  orderNo: number;
  remark?: string;
  tenantId?: number;
  type: number;
  updateTime?: string;
  updateUserName?: string;
}

export type SaveTemplateParams = Omit<
  SysTemplateRecord,
  | 'createTime'
  | 'createUserName'
  | 'id'
  | 'tenantId'
  | 'updateTime'
  | 'updateUserName'
> & {
  id?: number;
};

function recordOf(value: unknown): RawRecord {
  return value && typeof value === 'object' ? (value as RawRecord) : {};
}

function numberOf(value: unknown, fallback = 0) {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function textOf(value: unknown) {
  return value === undefined || value === null ? undefined : String(value);
}

function normalizeTemplate(value: unknown): SysTemplateRecord {
  const item = recordOf(value);
  return {
    code: textOf(item.code ?? item.Code) ?? '',
    content: textOf(item.content ?? item.Content) ?? '',
    createTime: textOf(item.createTime ?? item.CreateTime),
    createUserName: textOf(item.createUserName ?? item.CreateUserName),
    groupName: textOf(item.groupName ?? item.GroupName) ?? '',
    id: numberOf(item.id ?? item.Id),
    name: textOf(item.name ?? item.Name) ?? '',
    orderNo: numberOf(item.orderNo ?? item.OrderNo, 100),
    remark: textOf(item.remark ?? item.Remark),
    tenantId: numberOf(item.tenantId ?? item.TenantId) || undefined,
    type: numberOf(item.type ?? item.Type, 1),
    updateTime: textOf(item.updateTime ?? item.UpdateTime),
    updateUserName: textOf(item.updateUserName ?? item.UpdateUserName),
  };
}

export async function pageTemplatesApi(params: TemplateQuery) {
  const data = await requestClient.post<AdminNetPagedList<unknown>>(
    '/sysTemplate/page',
    params,
  );
  return {
    ...data,
    items: Array.isArray(data.items)
      ? data.items.map((item) => normalizeTemplate(item))
      : [],
  } as AdminNetPagedList<SysTemplateRecord>;
}

export function listTemplateGroupsApi() {
  return requestClient.get<string[]>('/sysTemplate/groupList');
}

export function renderTemplateApi(
  content: string,
  data: Record<string, string>,
) {
  return requestClient.post<string>('/sysTemplate/render', { content, data });
}

export function addTemplateApi(params: SaveTemplateParams) {
  return requestClient.post<unknown>('/sysTemplate/add', params);
}

export function updateTemplateApi(params: SaveTemplateParams & { id: number }) {
  return requestClient.post<unknown>('/sysTemplate/update', params);
}

export function deleteTemplateApi(id: number) {
  return requestClient.post<unknown>('/sysTemplate/delete', { id });
}

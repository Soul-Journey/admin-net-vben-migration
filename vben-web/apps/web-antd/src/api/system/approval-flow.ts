import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

type RawRecord = Record<string, unknown>;

export interface ApprovalFlowQuery {
  code?: string;
  keyword?: string;
  name?: string;
  page: number;
  pageSize: number;
  remark?: string;
}

export interface ApprovalFlowRecord {
  code?: string;
  createOrgName?: string;
  createTime?: string;
  createUserName?: string;
  flowJson?: string;
  formJson?: string;
  id: number;
  name: string;
  remark?: string;
  status?: number;
  updateTime?: string;
  updateUserName?: string;
}

export interface SaveApprovalFlowParams {
  code?: string;
  id?: number;
  name: string;
  remark?: string;
  status: number;
}

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

function normalizeApprovalFlow(value: unknown): ApprovalFlowRecord {
  const item = recordOf(value);
  return {
    code: textOf(item.code ?? item.Code),
    createOrgName: textOf(item.createOrgName ?? item.CreateOrgName),
    createTime: textOf(item.createTime ?? item.CreateTime),
    createUserName: textOf(item.createUserName ?? item.CreateUserName),
    flowJson: textOf(item.flowJson ?? item.FlowJson),
    formJson: textOf(item.formJson ?? item.FormJson),
    id: numberOf(item.id ?? item.Id),
    name: textOf(item.name ?? item.Name) ?? '',
    remark: textOf(item.remark ?? item.Remark),
    status: numberOf(item.status ?? item.Status, 1),
    updateTime: textOf(item.updateTime ?? item.UpdateTime),
    updateUserName: textOf(item.updateUserName ?? item.UpdateUserName),
  };
}

export async function pageApprovalFlowsApi(params: ApprovalFlowQuery) {
  const data = await requestClient.post<AdminNetPagedList<unknown>>(
    '/approvalFlow/page',
    params,
  );
  return {
    ...data,
    items: Array.isArray(data.items)
      ? data.items.map((item) => normalizeApprovalFlow(item))
      : [],
  } as AdminNetPagedList<ApprovalFlowRecord>;
}

export async function getApprovalFlowDetailApi(id: number) {
  const data = await requestClient.get<unknown>('/approvalFlow/detail', {
    params: { id },
  });
  return normalizeApprovalFlow(data);
}

export function addApprovalFlowApi(params: SaveApprovalFlowParams) {
  return requestClient.post<number>('/approvalFlow/add', params);
}

export function updateApprovalFlowApi(
  params: SaveApprovalFlowParams & { id: number },
) {
  return requestClient.post<unknown>('/approvalFlow/update', params);
}

export function deleteApprovalFlowApi(id: number) {
  return requestClient.post<unknown>('/approvalFlow/delete', { id });
}

export function updateApprovalFlowFormApi(id: number, json: string) {
  return requestClient.post<unknown>('/approvalFlow/updateForm', { id, json });
}

export function updateApprovalFlowDesignApi(id: number, json: string) {
  return requestClient.post<unknown>('/approvalFlow/updateFlow', { id, json });
}

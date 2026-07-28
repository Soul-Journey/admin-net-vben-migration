import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

export interface PageOperationLogParams {
  account?: string;
  actionName?: string;
  controllerName?: string;
  elapsed?: number;
  endTime?: string;
  field?: string;
  order?: string;
  page: number;
  pageSize: number;
  remoteIp?: string;
  startTime?: string;
  status?: string;
  tenantId?: number;
}

export interface OperationLogRecord {
  account?: string;
  actionName?: string;
  browser?: string;
  controllerName?: string;
  createTime?: string;
  displayTitle?: string;
  elapsed?: number;
  eventId?: number;
  exception?: string;
  httpMethod?: string;
  id: number;
  latitude?: number;
  location?: string;
  logDateTime?: string;
  logLevel?: number;
  longitude?: number;
  message?: string;
  os?: string;
  realName?: string;
  remoteIp?: string;
  requestParam?: string;
  requestUrl?: string;
  returnResult?: string;
  status?: string;
  tenantId?: number;
  threadId?: number;
  traceId?: string;
}

export function pageOperationLogsApi(params: PageOperationLogParams) {
  return requestClient.post<AdminNetPagedList<OperationLogRecord>>(
    '/sysLogOp/page',
    params,
  );
}

export function getOperationLogDetailApi(id: number) {
  return requestClient.get<OperationLogRecord>(`/sysLogOp/detail/${id}`);
}

export function clearOperationLogsApi(tenantId?: number) {
  return requestClient.post<number>('/sysLogOp/clear', {
    tenantId: tenantId ?? 0,
  });
}

export function exportOperationLogsApi(params: {
  endTime?: string;
  startTime?: string;
  tenantId?: number;
}) {
  return requestClient.download<Blob>('/sysLogOp/export', {
    data: params,
    method: 'POST',
  });
}

export interface PageVisitLogParams {
  account?: string;
  actionName?: string;
  elapsed?: number;
  endTime?: string;
  page: number;
  pageSize: number;
  remoteIp?: string;
  startTime?: string;
  status?: string;
  tenantId?: number;
}

export interface VisitLogRecord {
  account?: string;
  actionName?: string;
  browser?: string;
  controllerName?: string;
  displayTitle?: string;
  elapsed?: number;
  id: number;
  latitude?: number;
  location?: string;
  logDateTime?: string;
  logLevel?: number;
  longitude?: number;
  os?: string;
  realName?: string;
  remoteIp?: string;
  status?: string;
  tenantId?: number;
}

export function pageVisitLogsApi(params: PageVisitLogParams) {
  return requestClient.post<AdminNetPagedList<VisitLogRecord>>(
    '/sysLogVis/page',
    params,
  );
}

export function clearVisitLogsApi(tenantId?: number) {
  return requestClient.post<number>('/sysLogVis/clear', {
    tenantId: tenantId ?? 0,
  });
}

export function pageExceptionLogsApi(params: PageOperationLogParams) {
  return requestClient.post<AdminNetPagedList<OperationLogRecord>>(
    '/sysLogEx/page',
    params,
  );
}

export function getExceptionLogDetailApi(id: number) {
  return requestClient.get<OperationLogRecord>(`/sysLogEx/detail/${id}`);
}

export function clearExceptionLogsApi(tenantId?: number) {
  return requestClient.post<number>('/sysLogEx/clear', {
    tenantId: tenantId ?? 0,
  });
}

export function exportExceptionLogsApi(params: {
  endTime?: string;
  startTime?: string;
  tenantId?: number;
}) {
  return requestClient.download<Blob>('/sysLogEx/export', {
    data: params,
    method: 'POST',
  });
}

export interface DiffLogRecord {
  businessData?: string;
  createTime?: string;
  diffData?: string;
  diffType?: string;
  elapsed?: number;
  id: number;
  parameters?: string;
  sql?: string;
  tenantId?: number;
}

export function pageDiffLogsApi(params: {
  endTime?: string;
  page: number;
  pageSize: number;
  startTime?: string;
  tenantId?: number;
}) {
  return requestClient.post<AdminNetPagedList<DiffLogRecord>>(
    '/sysLogDiff/page',
    params,
  );
}

export function getDiffLogDetailApi(id: number) {
  return requestClient.get<DiffLogRecord>(`/sysLogDiff/detail/${id}`);
}

export function clearDiffLogsApi(tenantId?: number) {
  return requestClient.post<number>('/sysLogDiff/clear', {
    tenantId: tenantId ?? 0,
  });
}

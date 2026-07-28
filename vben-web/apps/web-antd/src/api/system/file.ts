import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

export interface PageFileParams {
  endTime?: string;
  fileName?: string;
  page: number;
  pageSize: number;
  startTime?: string;
  suffix?: string;
  tenantId?: number;
}

export interface SysFileRecord {
  belongId?: number;
  bucketName?: string;
  createTime?: string;
  createUserName?: string;
  fileName?: string;
  filePath?: string;
  fileType?: string;
  id: number;
  isPublic?: boolean;
  relationId?: number;
  relationName?: string;
  remark?: string;
  sizeInfo?: string;
  sizeKb?: number;
  suffix?: string;
  tenantId?: number;
  updateTime?: string;
  updateUserName?: string;
  url?: string;
}

export interface UpdateFileParams {
  belongId?: number;
  fileName: string;
  fileType?: string;
  id: number;
  isPublic: boolean;
  relationId?: number;
  relationName?: string;
}

export function pageFilesApi(params: PageFileParams) {
  return requestClient.post<AdminNetPagedList<SysFileRecord>>(
    '/sysFile/page',
    params,
  );
}

export function uploadFileApi(file: File, fileType: string, isPublic: boolean) {
  const data = new FormData();
  data.append('file', file);
  data.append('fileType', fileType);
  data.append('isPublic', String(isPublic));
  return requestClient.post<SysFileRecord>('/sysFile/uploadFile', data);
}

export function updateFileApi(params: UpdateFileParams) {
  return requestClient.post<unknown>('/sysFile/update', params);
}

export function deleteFileApi(id: number) {
  return requestClient.post<unknown>('/sysFile/delete', { id });
}

export function previewFileApi(id: number) {
  return requestClient.download<Blob>(`/sysFile/preview/${id}`);
}

export function downloadFileApi(record: SysFileRecord) {
  return requestClient.download<Blob>('/sysFile/downloadFile', {
    data: { id: record.id },
    method: 'POST',
  });
}

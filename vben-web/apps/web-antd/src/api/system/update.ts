import { requestClient } from '#/api/request';

export interface SystemBackupRecord {
  createTime?: string;
  fileName: string;
}

export interface SystemUpdateConfigurationStatus {
  accessTokenConfigured: boolean;
  backendOutputConfigured: boolean;
  backendOutputExists: boolean;
  backupCount: number;
  branch?: string;
  enabled: boolean;
  publishConfigured: boolean;
  readyForRestore: boolean;
  readyForUpdate: boolean;
  repository?: string;
  runtimeIdentifier?: string;
  targetFramework?: string;
  updateInterval: number;
}

export function getSystemUpdateConfigurationStatusApi() {
  return requestClient.get<SystemUpdateConfigurationStatus>(
    '/sysUpdate/configurationStatus',
  );
}

export function listSystemBackupsApi() {
  return requestClient.post<SystemBackupRecord[]>('/sysUpdate/list');
}

export function listSystemUpdateLogsApi() {
  return requestClient.get<string[]>('/sysUpdate/logs');
}

export function executeSystemUpdateApi() {
  return requestClient.post<unknown>('/sysUpdate/update', undefined, {
    timeout: 0,
  });
}

export function restoreSystemBackupApi(fileName: string) {
  return requestClient.post<unknown>(
    '/sysUpdate/restore',
    { fileName },
    {
      timeout: 0,
    },
  );
}

export function clearSystemUpdateLogsApi() {
  return requestClient.get<unknown>('/sysUpdate/clear');
}

export function getSystemUpdateWebhookKeyApi() {
  return requestClient.get<string>('/sysUpdate/webHookKey');
}

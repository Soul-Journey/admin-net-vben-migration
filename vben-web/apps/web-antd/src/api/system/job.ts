import type { AdminNetPagedList } from './user';

import { requestClient } from '#/api/request';

export interface JobDetailRecord {
  assemblyName?: string;
  concurrent: boolean;
  createType: number;
  description?: string;
  groupName?: string;
  id?: number;
  includeAnnotation?: boolean;
  jobId: string;
  jobType?: string;
  properties?: string;
  scriptCode?: string;
  updatedTime?: string;
}

export interface JobTriggerRecord {
  args?: string;
  assemblyName?: string;
  description?: string;
  endTime?: string;
  id?: number;
  jobId: string;
  lastRunTime?: string;
  maxNumberOfErrors?: number;
  maxNumberOfRuns?: number;
  nextRunTime?: string;
  numberOfErrors?: number;
  numberOfRuns?: number;
  numRetries?: number;
  resetOnlyOnce?: boolean;
  retryTimeout?: number;
  runOnStart?: boolean;
  startNow?: boolean;
  startTime?: string;
  status?: number;
  triggerId: string;
  triggerType?: string;
  updatedTime?: string;
}

export interface JobDetailOutput {
  jobDetail: JobDetailRecord;
  jobTriggers: JobTriggerRecord[];
}

export interface JobExecutionRecord {
  createdTime?: string;
  elapsedTime?: number;
  id: number;
  jobId: string;
  lastRunTime?: string;
  nextRunTime?: string;
  numberOfRuns?: number;
  result?: string;
  status?: number;
  triggerId: string;
}

export interface JobClusterRecord {
  clusterId: string;
  description?: string;
  id: number;
  status?: number;
  updatedTime?: string;
}

export function pageJobsApi(params: {
  description?: string;
  groupName?: string;
  jobId?: string;
  page: number;
  pageSize: number;
}) {
  return requestClient.post<AdminNetPagedList<JobDetailOutput>>(
    '/sysJob/pageJobDetail',
    params,
  );
}
export function listJobGroupsApi() {
  return requestClient.post<string[]>('/sysJob/listJobGroup');
}
export function addJobApi(data: JobDetailRecord) {
  return requestClient.post('/sysJob/addJobDetail', data);
}
export function updateJobApi(data: JobDetailRecord) {
  return requestClient.post('/sysJob/updateJobDetail', data);
}
export function deleteJobApi(jobId: string) {
  return requestClient.post('/sysJob/deleteJobDetail', { jobId });
}
export function addJobTriggerApi(data: JobTriggerRecord) {
  return requestClient.post('/sysJob/addJobTrigger', data);
}
export function updateJobTriggerApi(data: JobTriggerRecord) {
  return requestClient.post('/sysJob/updateJobTrigger', data);
}
export function deleteJobTriggerApi(jobId: string, triggerId: string) {
  return requestClient.post('/sysJob/deleteJobTrigger', { jobId, triggerId });
}
export function runJobApi(jobId: string) {
  return requestClient.post('/sysJob/runJob', { jobId });
}
export function startJobApi(jobId: string) {
  return requestClient.post('/sysJob/startJob', { jobId });
}
export function pauseJobApi(jobId: string) {
  return requestClient.post('/sysJob/pauseJob', { jobId });
}
export function cancelJobApi(jobId: string) {
  return requestClient.post('/sysJob/cancelJob', { jobId });
}
export function startAllJobsApi() {
  return requestClient.post('/sysJob/startAllJob');
}
export function pauseAllJobsApi() {
  return requestClient.post('/sysJob/pauseAllJob');
}
export function persistAllJobsApi() {
  return requestClient.post('/sysJob/persistAll');
}
export function wakeSchedulerApi() {
  return requestClient.post('/sysJob/cancelSleep');
}
export function startJobTriggerApi(jobId: string, triggerId: string) {
  return requestClient.post('/sysJob/startTrigger', { jobId, triggerId });
}
export function pauseJobTriggerApi(jobId: string, triggerId: string) {
  return requestClient.post('/sysJob/pauseTrigger', { jobId, triggerId });
}
export function pageJobRecordsApi(params: {
  jobId?: string;
  page: number;
  pageSize: number;
  triggerId?: string;
}) {
  return requestClient.post<AdminNetPagedList<JobExecutionRecord>>(
    '/sysJob/pageJobTriggerRecord',
    params,
  );
}
export function listJobClustersApi() {
  return requestClient.get<JobClusterRecord[]>('/sysJob/jobClusterList');
}

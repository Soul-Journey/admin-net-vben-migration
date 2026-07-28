import { requestClient } from '#/api/request';

export interface ServerBaseInfo {
  environment?: string;
  frameworkDescription?: string;
  hostName?: string;
  localIp?: string;
  osArchitecture?: string;
  processorCount?: string;
  remoteIp?: string;
  stage?: string;
  systemOs?: string;
  sysRunTime?: string;
  wwwroot?: string;
}

export interface ServerUsageInfo {
  cpuRate?: string;
  freeRam?: string;
  ramRate?: string;
  runTime?: string;
  startTime?: string;
  totalRam?: string;
  usedRam?: string;
}

export interface ServerDiskInfo {
  availableFreeSpace: number;
  availablePercent: number;
  diskName: string;
  totalSize: number;
  typeName?: string;
  used: number;
}

export interface ServerAssemblyInfo {
  name?: string;
  version?: string;
}

export function getServerBaseApi() {
  return requestClient.get<ServerBaseInfo>('/sysServer/serverBase');
}
export function getServerUsageApi() {
  return requestClient.get<ServerUsageInfo>('/sysServer/serverUsed');
}
export function getServerDisksApi() {
  return requestClient.get<ServerDiskInfo[]>('/sysServer/serverDisk');
}
export function getServerAssembliesApi() {
  return requestClient.get<ServerAssemblyInfo[]>('/sysServer/assemblyList');
}

import { requestClient } from '#/api/request';

export interface StressTestEndpointRecord {
  displayName: string;
  groupName?: string;
  method: 'GET' | 'POST';
  route: string;
}

export interface StressTestParams {
  headers?: Record<string, string>;
  maxDegreeOfParallelism: number;
  numberOfRequests: number;
  numberOfRounds: number;
  pathParameters?: Record<string, string>;
  queryParameters?: Record<string, string>;
  requestMethod: 'GET' | 'POST';
  requestParameters?: Array<{ key: string; value: string }>;
  requestUri: string;
}

export interface StressTestResult {
  averageResponseTime: number;
  failedRequests: number;
  maxResponseTime: number;
  minResponseTime: number;
  percentile10ResponseTime: number;
  percentile25ResponseTime: number;
  percentile50ResponseTime: number;
  percentile75ResponseTime: number;
  percentile90ResponseTime: number;
  percentile99ResponseTime: number;
  percentile999ResponseTime: number;
  queriesPerSecond: number;
  successfulRequests: number;
  timedOut: boolean;
  totalRequests: number;
  totalTimeInSeconds: number;
}

export function listStressTestEndpointsApi() {
  return requestClient.get<StressTestEndpointRecord[]>(
    '/sysCommon/stressTestEndpoints',
  );
}

export function executeStressTestApi(params: StressTestParams) {
  return requestClient.post<StressTestResult>('/sysCommon/stressTest', params, {
    timeout: 40_000,
  });
}

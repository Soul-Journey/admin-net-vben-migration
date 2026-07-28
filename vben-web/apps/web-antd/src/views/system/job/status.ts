interface TriggerStatusMeta {
  abnormal?: boolean;
  color: string;
  hint: string;
  label: string;
}

const STATUS_META: Record<number, TriggerStatusMeta> = {
  0: {
    color: 'orange',
    hint: '任务已进入调度队列，正在等待可用执行时间。',
    label: '等待执行',
  },
  1: {
    color: 'green',
    hint: '触发器工作正常，等待下一次调度。',
    label: '就绪',
  },
  2: { color: 'blue', hint: '任务当前正在执行。', label: '运行中' },
  3: {
    color: 'default',
    hint: '触发器已暂停，不会产生新的任务实例。',
    label: '已暂停',
  },
  4: {
    abnormal: true,
    color: 'red',
    hint: '任务暂时无法继续执行，需要检查运行条件。',
    label: '执行阻塞',
  },
  5: {
    color: 'cyan',
    hint: '任务发生过失败，调度器已经将它恢复为可再次执行的就绪状态。',
    label: '失败后就绪',
  },
  6: {
    color: 'default',
    hint: '触发器已归档，不再参与调度。',
    label: '已归档',
  },
  7: {
    abnormal: true,
    color: 'red',
    hint: '任务执行异常终止，需要查看执行结果和后端日志。',
    label: '执行崩溃',
  },
  8: {
    color: 'geekblue',
    hint: '触发器已达到允许执行的最大次数；启动时只运行一次的任务出现此状态属于正常情况。',
    label: '已达次数上限',
  },
  9: {
    color: 'default',
    hint: '根据当前调度规则无法计算出下一次触发时间。',
    label: '无触发时间',
  },
  10: {
    color: 'default',
    hint: '触发器尚未启动，不会产生新的任务实例。',
    label: '未启动',
  },
  11: {
    abnormal: true,
    color: 'red',
    hint: '调度器无法识别当前触发器类型，需要检查触发器配置和程序集。',
    label: '未知触发器',
  },
  12: {
    abnormal: true,
    color: 'red',
    hint: '调度器找不到作业处理程序，需要检查作业类型和程序集。',
    label: '未知处理程序',
  },
};

function getStatus(status?: number) {
  return STATUS_META[status ?? -1];
}

export function triggerMeta(status?: number) {
  const meta = getStatus(status);
  return meta ? [meta.label, meta.color] : [`状态 ${status ?? '-'}`, 'default'];
}

export function triggerStatusHint(status?: number) {
  return getStatus(status)?.hint ?? '调度器返回的未知状态。';
}

export function isAbnormalTriggerStatus(status?: number) {
  return getStatus(status)?.abnormal === true;
}

<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, reactive, ref } from 'vue';

import { IconifyIcon } from '@vben/icons';

import LogicFlow, {
  CircleNode,
  CircleNodeModel,
  PolylineEdge,
  PolylineEdgeModel,
  RectNode,
  RectNodeModel,
} from '@logicflow/core';
import { BpmnElement } from '@logicflow/extension';
import {
  Button,
  Empty,
  Input,
  message,
  Modal,
  Space,
  Tooltip,
} from 'ant-design-vue';

import '@logicflow/core/dist/index.css';
import '@logicflow/extension/lib/style/index.css';

defineOptions({ name: 'AdminNetApprovalFlowDesigner' });

const props = defineProps<{
  initialJson?: string;
  saving?: boolean;
}>();

const emit = defineEmits<{
  save: [json: string];
}>();

type SelectedElement = {
  id: string;
  text: string;
  type: string;
};

type GraphData = {
  edges: unknown[];
  nodes: unknown[];
};

const canvasRef = ref<HTMLDivElement>();
const jsonOpen = ref(false);
const graphJson = ref('');
const selected = reactive<SelectedElement>({ id: '', text: '', type: '' });
let logicFlow: InstanceType<typeof LogicFlow> | undefined;
let nodeSequence = 0;

const palette = [
  { icon: 'lucide:circle-play', label: '开始', type: 'bpmn:startEvent' },
  { icon: 'lucide:user-round-check', label: '人工审批', type: 'bpmn:userTask' },
  { icon: 'lucide:diamond', label: '条件分支', type: 'bpmn:exclusiveGateway' },
  { icon: 'lucide:cpu', label: '系统任务', type: 'task-node' },
  { icon: 'lucide:circle-stop', label: '结束', type: 'bpmn:endEvent' },
];

function parseGraph(json?: string): GraphData {
  if (!json?.trim()) return { edges: [], nodes: [] };
  try {
    const parsed = JSON.parse(json) as Partial<GraphData>;
    return {
      edges: Array.isArray(parsed.edges) ? parsed.edges : [],
      nodes: Array.isArray(parsed.nodes) ? parsed.nodes : [],
    };
  } catch {
    message.warning('原流程 JSON 无法解析，已打开空白画布；取消可保留原数据');
    return { edges: [], nodes: [] };
  }
}

function registerLegacyTypes(instance: InstanceType<typeof LogicFlow>) {
  instance.register({
    model: RectNodeModel,
    type: 'task-node',
    view: RectNode,
  });
  instance.register({
    model: CircleNodeModel,
    type: 'start-node',
    view: CircleNode,
  });
  instance.register({
    model: CircleNodeModel,
    type: 'end-node',
    view: CircleNode,
  });
  instance.register({
    model: RectNodeModel,
    type: 'user-node',
    view: RectNode,
  });
  instance.register({ model: RectNodeModel, type: 'sql-node', view: RectNode });
  instance.register({
    model: PolylineEdgeModel,
    type: 'edge-sql',
    view: PolylineEdge,
  });
}

function textValue(value: unknown) {
  if (typeof value === 'string') return value;
  if (value && typeof value === 'object' && 'value' in value) {
    const text = (value as { value?: unknown }).value;
    return typeof text === 'string' ? text : '';
  }
  return '';
}

function selectElement(event: unknown) {
  const data =
    event && typeof event === 'object' && 'data' in event
      ? (event as { data?: Record<string, unknown> }).data
      : undefined;
  selected.id = typeof data?.id === 'string' ? data.id : '';
  selected.type = typeof data?.type === 'string' ? data.type : '';
  selected.text = textValue(data?.text);
}

function clearSelected() {
  selected.id = '';
  selected.type = '';
  selected.text = '';
}

function initializeDesigner() {
  if (!canvasRef.value) return;
  logicFlow = new LogicFlow({
    container: canvasRef.value,
    grid: { size: 12, type: 'dot' },
    keyboard: { enabled: true },
    plugins: [BpmnElement],
    snapline: true,
  });
  registerLegacyTypes(logicFlow);
  logicFlow.setTheme({
    anchor: { fill: '#fff', r: 4, stroke: '#4f6bfe' },
    baseEdge: { stroke: '#73829a', strokeWidth: 1.5 },
    nodeText: { color: '#26344a', fontSize: 13 },
    outline: { stroke: '#4f6bfe', strokeWidth: 1.5 },
    snapline: { stroke: '#4f6bfe', strokeWidth: 1 },
  });
  logicFlow.on('node:click', selectElement);
  logicFlow.on('edge:click', selectElement);
  logicFlow.on('blank:click', clearSelected);
  logicFlow.render(parseGraph(props.initialJson) as never);
  nextTick(() => logicFlow?.fitView(44, 44));
}

function addNode(type: string, label: string) {
  if (!logicFlow || !canvasRef.value) return;
  const offset = nodeSequence++ % 6;
  logicFlow.addNode({
    text: label,
    type,
    x: Math.max(260, canvasRef.value.clientWidth / 2 - 120 + offset * 38),
    y: 150 + offset * 64,
  });
}

function updateSelectedText() {
  if (logicFlow && selected.id)
    logicFlow.updateText(selected.id, selected.text.trim());
}

function removeSelected() {
  if (!logicFlow || !selected.id) return;
  if (
    selected.type.includes('edge') ||
    selected.type.includes('sequenceFlow')
  ) {
    logicFlow.deleteEdge(selected.id);
  } else {
    logicFlow.deleteNode(selected.id);
  }
  clearSelected();
}

function showJson() {
  graphJson.value = JSON.stringify(
    logicFlow?.getGraphData() ?? { edges: [], nodes: [] },
    null,
    2,
  );
  jsonOpen.value = true;
}

function save() {
  const data = logicFlow?.getGraphData() ?? { edges: [], nodes: [] };
  emit('save', JSON.stringify(data));
}

onMounted(() => nextTick(initializeDesigner));
onBeforeUnmount(() => {
  logicFlow?.destroy();
  logicFlow = undefined;
});
</script>

<template>
  <div class="designer-shell">
    <div class="designer-toolbar">
      <div class="toolbar-group">
        <Tooltip title="撤销">
          <Button size="small" @click="logicFlow?.undo()">
            <template #icon>
              <IconifyIcon icon="lucide:undo-2" />
            </template>
          </Button>
        </Tooltip>
        <Tooltip title="重做">
          <Button size="small" @click="logicFlow?.redo()">
            <template #icon>
              <IconifyIcon icon="lucide:redo-2" />
            </template>
          </Button>
        </Tooltip>
        <Tooltip title="放大">
          <Button size="small" @click="logicFlow?.zoom(true)">
            <template #icon>
              <IconifyIcon icon="lucide:zoom-in" />
            </template>
          </Button>
        </Tooltip>
        <Tooltip title="缩小">
          <Button size="small" @click="logicFlow?.zoom(false)">
            <template #icon>
              <IconifyIcon icon="lucide:zoom-out" />
            </template>
          </Button>
        </Tooltip>
        <Tooltip title="适应画布">
          <Button size="small" @click="logicFlow?.fitView(44, 44)">
            <template #icon>
              <IconifyIcon icon="lucide:scan" />
            </template>
          </Button>
        </Tooltip>
      </div>
      <Space>
        <Button size="small" @click="showJson">
          <template #icon><IconifyIcon icon="lucide:braces" /></template>查看
          JSON
        </Button>
        <Button :loading="saving" size="small" type="primary" @click="save">
          <template #icon><IconifyIcon icon="lucide:save" /></template>保存流程
        </Button>
      </Space>
    </div>

    <div class="designer-body">
      <aside class="palette-panel">
        <div class="side-title">流程节点</div>
        <p>点击添加，再拖动节点并连接锚点</p>
        <button
          v-for="item in palette"
          :key="item.type"
          class="palette-item"
          type="button"
          @click="addNode(item.type, item.label)"
        >
          <IconifyIcon :icon="item.icon" />
          <span>{{ item.label }}</span>
        </button>
      </aside>

      <div ref="canvasRef" class="flow-canvas"></div>

      <aside class="property-panel">
        <template v-if="selected.id">
          <div class="side-title">所选元素</div>
          <div class="property-label">类型</div>
          <code>{{ selected.type }}</code>
          <div class="property-label">显示名称</div>
          <Input
            v-model:value="selected.text"
            :maxlength="64"
            @blur="updateSelectedText"
            @press-enter="updateSelectedText"
          />
          <Button danger class="delete-button" @click="removeSelected">
            <template #icon><IconifyIcon icon="lucide:trash-2" /></template
            >删除所选元素
          </Button>
        </template>
        <Empty
          v-else
          :image="Empty.PRESENTED_IMAGE_SIMPLE"
          description="点击节点或连线后编辑"
        />
      </aside>
    </div>

    <Modal
      v-model:open="jsonOpen"
      :footer="null"
      title="流程 JSON（只读）"
      :width="760"
    >
      <Input.TextArea
        :value="graphJson"
        class="json-view"
        readonly
        :rows="20"
      />
    </Modal>
  </div>
</template>

<style scoped>
.designer-shell {
  overflow: hidden;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border));
  border-radius: 8px;
}

.designer-toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  height: 48px;
  padding: 0 12px;
  background: hsl(var(--muted) / 28%);
  border-bottom: 1px solid hsl(var(--border));
}

.toolbar-group {
  display: flex;
  gap: 6px;
}

.designer-body {
  display: grid;
  grid-template-columns: 168px minmax(500px, 1fr) 220px;
  height: calc(100vh - 190px);
  min-height: 520px;
}

.palette-panel,
.property-panel {
  padding: 14px 12px;
  background: hsl(var(--background));
}

.palette-panel {
  border-right: 1px solid hsl(var(--border));
}

.property-panel {
  border-left: 1px solid hsl(var(--border));
}

.side-title {
  font-size: 14px;
  font-weight: 650;
  color: hsl(var(--foreground));
}

.palette-panel p {
  margin: 4px 0 12px;
  font-size: 12px;
  line-height: 1.5;
  color: hsl(var(--muted-foreground));
}

.palette-item {
  display: flex;
  gap: 9px;
  align-items: center;
  width: 100%;
  height: 38px;
  padding: 0 10px;
  margin-bottom: 7px;
  color: hsl(var(--foreground));
  cursor: pointer;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.palette-item:hover {
  color: #3e58e8;
  background: rgb(79 107 254 / 6%);
  border-color: #4f6bfe;
}

.palette-item svg {
  width: 17px;
  height: 17px;
}

.flow-canvas {
  min-width: 0;
  height: 100%;
  background: #fbfcfe;
}

.property-label {
  margin: 15px 0 6px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.property-panel code {
  display: block;
  padding: 6px 8px;
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 11px;
  white-space: nowrap;
  background: hsl(var(--muted));
  border-radius: 4px;
}

.delete-button {
  width: 100%;
  margin-top: 18px;
}

.json-view {
  font-family: Consolas, monospace;
  font-size: 12px;
}

:deep(.lf-graph) {
  background: transparent;
}

@media (max-width: 1100px) {
  .designer-body {
    grid-template-columns: 150px minmax(520px, 1fr);
    overflow-x: auto;
  }

  .property-panel {
    display: none;
  }
}
</style>

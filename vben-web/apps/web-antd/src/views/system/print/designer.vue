<script setup lang="ts">
import type { FormInstance } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type { SavePrintParams, SysPrintRecord } from '#/api';

import { nextTick, reactive, ref } from 'vue';
import {
  defaultElementTypeProvider as DefaultElementTypeProvider,
  disAutoConnect,
  hiprint,
} from 'vue-plugin-hiprint';
import printLockUrl from 'vue-plugin-hiprint/dist/print-lock.css?url';

import { IconifyIcon } from '@vben/icons';

import {
  Button,
  Form,
  Input,
  InputNumber,
  message,
  Modal,
  Radio,
  Select,
  Space,
  Tabs,
  Tooltip,
} from 'ant-design-vue';

defineOptions({ name: 'AdminNetPrintDesigner' });

const emit = defineEmits<{
  save: [value: SavePrintParams];
}>();

const open = ref(false);
const previewOpen = ref(false);
const initializing = ref(false);
const saving = ref(false);
const formRef = ref<FormInstance>();
const previewRef = ref<HTMLElement>();
const printTemplate = ref<any>();
const currentId = ref<number>();

const form = reactive<SavePrintParams>({
  clientServiceAddress: '',
  name: '',
  orderNo: 100,
  printDataDemo: '{\n  "title": "Admin.NET 打印预览"\n}',
  printParam: '',
  printType: 1,
  remark: '',
  status: 1,
  template: '',
});

const paper = reactive({ height: 296.6, type: 'A4', width: 210 });
const scale = ref(1);

const paperOptions = [
  { label: 'A3 横向', value: '420,296.6' },
  { label: 'A4 纵向', value: '210,296.6' },
  { label: 'A5 横向', value: '210,147.6' },
  { label: 'B4 纵向', value: '250,352.6' },
  { label: 'B5 横向', value: '250,175.6' },
  { label: '4R 相纸', value: '152,102' },
  { label: '6R 相纸', value: '203,152' },
];

const rules: Record<string, Rule[]> = {
  name: [{ message: '请输入模板名称', required: true, trigger: 'blur' }],
};

disAutoConnect();

function ensurePrintStyles() {
  if (document.querySelector('link[data-adminnet-print-lock]')) return;
  const link = document.createElement('link');
  link.dataset.adminnetPrintLock = 'true';
  link.href = printLockUrl;
  link.media = 'print';
  link.rel = 'stylesheet';
  document.head.append(link);
}

function resetForm(record?: SysPrintRecord) {
  currentId.value = record?.id;
  Object.assign(form, {
    clientServiceAddress: record?.clientServiceAddress ?? '',
    name: record?.name ?? '',
    orderNo: record?.orderNo ?? 100,
    printDataDemo:
      record?.printDataDemo ?? '{\n  "title": "Admin.NET 打印预览"\n}',
    printParam: record?.printParam ?? '',
    printType: record?.printType ?? 1,
    remark: record?.remark ?? '',
    status: record?.status ?? 1,
    template: record?.template ?? '',
    tenantId: record?.tenantId,
  });
  scale.value = 1;
}

async function openDesigner(record?: SysPrintRecord, tenantId?: number) {
  ensurePrintStyles();
  resetForm(record);
  if (!record && tenantId) form.tenantId = tenantId;
  open.value = true;
  initializing.value = true;
  await nextTick();
  buildDesigner();
  initializing.value = false;
}

function buildDesigner() {
  const tools = document.querySelector('.hiprint-elements');
  const canvas = document.querySelector('#hiprint-print-template');
  if (!tools || !canvas) return;

  tools.innerHTML = '';
  canvas.innerHTML = '';
  hiprint.init({ lang: 'cn', providers: [new DefaultElementTypeProvider()] });
  hiprint.PrintElementTypeManager.build('.hiprint-elements', 'defaultModule');

  let template: Record<string, unknown> = {};
  if (form.template) {
    try {
      template = JSON.parse(form.template);
    } catch {
      message.warning('原模板 JSON 无法解析，已打开空白画布');
    }
  }

  printTemplate.value = new hiprint.PrintTemplate({
    fontList: [
      { title: '微软雅黑', value: 'Microsoft YaHei' },
      { title: '宋体', value: 'SimSun' },
      { title: '黑体', value: 'SimHei' },
      { title: 'Arial', value: 'Arial' },
    ],
    history: true,
    paginationContainer: '.hiprint-pagination',
    settingContainer: '#hiprint-option-setting',
    template,
  });
  printTemplate.value.design('#hiprint-print-template');

  const firstPanel = printTemplate.value.getJson()?.panels?.[0];
  if (firstPanel) {
    paper.width = Number(firstPanel.width) || 210;
    paper.height = Number(firstPanel.height) || 296.6;
  }
}

function setPaper(value: unknown) {
  if (typeof value !== 'string') return;
  const [width, height] = value.split(',').map(Number);
  if (!width || !height || !printTemplate.value) return;
  paper.width = width;
  paper.height = height;
  printTemplate.value.setPaper(width, height);
}

function applyCustomPaper() {
  if (paper.width <= 0 || paper.height <= 0) {
    message.warning('纸张宽高必须大于 0');
    return;
  }
  printTemplate.value?.setPaper(paper.width, paper.height);
}

function changeScale(value: unknown) {
  const nextScale = typeof value === 'number' ? value : Number(value) || 1;
  scale.value = nextScale;
  printTemplate.value?.zoom(nextScale);
}

function parsePreviewData() {
  try {
    return form.printDataDemo ? JSON.parse(form.printDataDemo) : {};
  } catch {
    message.error('测试数据不是有效的 JSON');
    return undefined;
  }
}

async function showPreview() {
  const data = parsePreviewData();
  if (data === undefined || !printTemplate.value) return;
  previewOpen.value = true;
  await nextTick();
  if (!previewRef.value) return;
  previewRef.value.innerHTML = '';
  const html = printTemplate.value.getHtml(data);
  if (html?.[0]) previewRef.value.append(html[0]);
}

function browserPrint() {
  const data = parsePreviewData();
  if (data === undefined) return;
  printTemplate.value?.print(data);
}

function exportPdf() {
  const data = parsePreviewData();
  if (data === undefined) return;
  printTemplate.value?.toPdf(data, form.name || '打印模板');
}

function formatDemoData() {
  const data = parsePreviewData();
  if (data !== undefined) form.printDataDemo = JSON.stringify(data, null, 2);
}

function clearCanvas() {
  Modal.confirm({
    centered: true,
    content: '清空后无法通过关闭窗口恢复当前画布，确定继续吗？',
    okButtonProps: { danger: true },
    okText: '清空画布',
    onOk: () => printTemplate.value?.clear(),
    title: '清空打印模板',
  });
}

async function submit() {
  try {
    await formRef.value?.validate();
  } catch {
    return;
  }
  const template = printTemplate.value?.getJson();
  if (!template?.panels?.length) {
    message.warning('打印模板至少需要一个纸张面板');
    return;
  }
  template.panels[0].index = template.panels[0].index ?? 0;
  saving.value = true;
  emit('save', {
    ...form,
    id: currentId.value,
    name: form.name.trim(),
    template: JSON.stringify(template),
  });
}

function finishSave(success: boolean) {
  saving.value = false;
  if (success) open.value = false;
}

defineExpose({ finishSave, openDesigner });
</script>

<template>
  <Modal
    v-model:open="open"
    :closable="!saving"
    :footer="null"
    :keyboard="!saving"
    :mask-closable="false"
    class="print-designer-modal"
    destroy-on-close
    width="calc(100vw - 24px)"
  >
    <template #title>
      <div class="designer-title">
        <IconifyIcon icon="lucide:printer" />
        <span>{{ currentId ? '编辑打印模板' : '新增打印模板' }}</span>
        <small>拖拽组件到纸张，选中组件后在右侧调整属性</small>
      </div>
    </template>

    <div class="designer-toolbar">
      <Select
        :options="paperOptions"
        placeholder="选择纸张"
        style="width: 132px"
        @change="setPaper"
      />
      <InputNumber v-model:value="paper.width" :min="20" addon-after="mm" />
      <span class="paper-times">×</span>
      <InputNumber v-model:value="paper.height" :min="20" addon-after="mm" />
      <Button @click="applyCustomPaper">应用尺寸</Button>
      <InputNumber
        :max="3"
        :min="0.5"
        :step="0.1"
        :value="scale"
        addon-before="缩放"
        @change="changeScale"
      />
      <Tooltip title="旋转纸张">
        <Button aria-label="旋转纸张" @click="printTemplate?.rotatePaper()">
          <IconifyIcon icon="lucide:rotate-cw" />
        </Button>
      </Tooltip>
      <Tooltip title="预览">
        <Button aria-label="预览" @click="showPreview">
          <IconifyIcon icon="lucide:eye" />
        </Button>
      </Tooltip>
      <Tooltip title="浏览器打印">
        <Button aria-label="浏览器打印" @click="browserPrint">
          <IconifyIcon icon="lucide:printer" />
        </Button>
      </Tooltip>
      <Tooltip title="导出 PDF">
        <Button aria-label="导出 PDF" @click="exportPdf">
          <IconifyIcon icon="lucide:file-down" />
        </Button>
      </Tooltip>
      <Tooltip title="清空画布">
        <Button danger aria-label="清空画布" @click="clearCanvas">
          <IconifyIcon icon="lucide:trash-2" />
        </Button>
      </Tooltip>
      <div class="toolbar-spacer"></div>
      <Button :disabled="saving" @click="open = false">取消</Button>
      <Button :loading="saving" type="primary" @click="submit">
        保存模板
      </Button>
    </div>

    <div v-if="initializing" class="designer-loading">正在初始化设计器...</div>
    <div class="designer-grid">
      <aside class="element-panel">
        <div class="side-title">可用组件</div>
        <div class="side-hint">拖到中间纸张使用</div>
        <div class="hiprint-elements rect-printElement-types"></div>
      </aside>
      <main class="canvas-panel">
        <div class="hiprint-pagination"></div>
        <div id="hiprint-print-template"></div>
      </main>
      <aside class="setting-panel">
        <Tabs size="small">
          <Tabs.TabPane key="properties" tab="组件属性">
            <div id="hiprint-option-setting"></div>
          </Tabs.TabPane>
          <Tabs.TabPane key="template" tab="模板设置">
            <Form ref="formRef" :model="form" :rules="rules" layout="vertical">
              <Form.Item label="模板名称" name="name">
                <Input v-model:value="form.name" :maxlength="64" />
              </Form.Item>
              <div class="setting-row">
                <Form.Item label="排序">
                  <InputNumber v-model:value="form.orderNo" :min="0" />
                </Form.Item>
                <Form.Item label="状态">
                  <Radio.Group v-model:value="form.status">
                    <Radio :value="1">启用</Radio>
                    <Radio :value="2">禁用</Radio>
                  </Radio.Group>
                </Form.Item>
              </div>
              <Form.Item label="打印方式">
                <Radio.Group v-model:value="form.printType">
                  <Radio :value="1">浏览器打印</Radio>
                  <Radio :value="2">客户端打印</Radio>
                </Radio.Group>
              </Form.Item>
              <Form.Item label="客户端服务地址">
                <Input
                  v-model:value="form.clientServiceAddress"
                  placeholder="仅客户端打印时填写"
                />
              </Form.Item>
              <Form.Item label="打印参数">
                <Input.TextArea
                  v-model:value="form.printParam"
                  :auto-size="{ minRows: 2, maxRows: 4 }"
                  placeholder="可选 JSON 或客户端参数"
                />
              </Form.Item>
              <Form.Item label="备注">
                <Input.TextArea
                  v-model:value="form.remark"
                  :auto-size="{ minRows: 2, maxRows: 3 }"
                  :maxlength="128"
                />
              </Form.Item>
            </Form>
          </Tabs.TabPane>
          <Tabs.TabPane key="data" tab="测试数据">
            <Button class="format-button" size="small" @click="formatDemoData">
              格式化 JSON
            </Button>
            <Input.TextArea
              v-model:value="form.printDataDemo"
              :auto-size="{ minRows: 18, maxRows: 28 }"
              class="data-editor"
            />
          </Tabs.TabPane>
        </Tabs>
      </aside>
    </div>
  </Modal>

  <Modal
    v-model:open="previewOpen"
    :footer="null"
    title="打印预览"
    width="min(900px, calc(100vw - 32px))"
  >
    <div ref="previewRef" class="preview-content"></div>
    <div class="preview-footer">
      <Space>
        <Button @click="previewOpen = false">关闭</Button>
        <Button @click="exportPdf">
          <template #icon><IconifyIcon icon="lucide:file-down" /></template>
          导出 PDF
        </Button>
        <Button type="primary" @click="browserPrint">
          <template #icon><IconifyIcon icon="lucide:printer" /></template>
          打印
        </Button>
      </Space>
    </div>
  </Modal>
</template>

<style scoped>
.designer-title {
  display: flex;
  gap: 8px;
  align-items: center;
}

.designer-title small {
  font-size: 12px;
  font-weight: 400;
  color: hsl(var(--muted-foreground));
}

.designer-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  min-height: 46px;
  padding: 7px 10px;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.paper-times {
  color: hsl(var(--muted-foreground));
}

.toolbar-spacer {
  flex: 1;
  min-width: 16px;
}

.designer-loading {
  padding: 10px;
  color: hsl(var(--muted-foreground));
  text-align: center;
}

.designer-grid {
  display: grid;
  grid-template-columns: 220px minmax(520px, 1fr) 320px;
  gap: 8px;
  height: calc(100vh - 142px);
  min-height: 560px;
  margin-top: 8px;
}

.element-panel,
.canvas-panel,
.setting-panel {
  min-width: 0;
  overflow: auto;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.element-panel,
.setting-panel {
  padding: 10px;
}

.canvas-panel {
  padding: 18px;
  background: #eef1f5;
}

.side-title {
  font-size: 14px;
  font-weight: 650;
}

.side-hint {
  margin: 2px 0 10px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.setting-row {
  display: grid;
  grid-template-columns: 100px 1fr;
  gap: 12px;
}

.format-button {
  margin-bottom: 8px;
}

.data-editor :deep(textarea) {
  font-family: Consolas, monospace;
}

.preview-content {
  max-height: 70vh;
  padding: 18px;
  overflow: auto;
  background: #eef1f5;
}

.preview-footer {
  display: flex;
  justify-content: flex-end;
  padding-top: 12px;
}

:global(.print-designer-modal) {
  top: 12px;
  max-width: none;
  padding-bottom: 0;
}

:global(.print-designer-modal .ant-modal-content) {
  padding: 12px;
}

:deep(.hiprint-elements .hiprint-printElement-type > li > ul > li > a) {
  height: auto;
  color: hsl(var(--primary));
  border-radius: 4px;
  box-shadow: none !important;
}

:deep(.hiprint-option-item-submitBtn) {
  background: hsl(var(--primary));
}

@media (max-width: 1100px) {
  .designer-grid {
    grid-template-columns: 190px minmax(500px, 1fr) 280px;
  }
}
</style>

<script setup lang="ts">
import { getCurrentInstance, ref } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { Alert, Button, Modal, Tag, Tooltip } from 'ant-design-vue';
import ElementPlus from 'element-plus';
import VForm3 from 'vform3-builds';

import 'element-plus/dist/index.css';
import 'vform3-builds/dist/designer.style.css';

defineOptions({ name: 'AdminNetSystemFormDesigner' });

const app = getCurrentInstance()?.appContext.app;
if (app && !app.component('ElButton')) {
  app.use(ElementPlus);
}
if (app && !app.component('VFormDesigner')) {
  app.use(VForm3);
}

const VFormDesigner = VForm3.VFormDesigner;
const helpOpen = ref(false);
</script>

<template>
  <main class="form-designer-page">
    <header class="workspace-bar">
      <div class="min-w-0">
        <div class="flex flex-wrap items-center gap-2">
          <h1>表单设计</h1>
          <Tag color="blue">VForm 3</Tag>
          <Tag>与旧版 JSON 兼容</Tag>
        </div>
        <p>拖拽字段并配置属性，完成后请导出 JSON 保存设计成果</p>
      </div>

      <Tooltip title="查看保存方式和使用边界">
        <Button aria-label="使用说明" shape="circle" @click="helpOpen = true">
          <template #icon>
            <IconifyIcon icon="lucide:circle-help" />
          </template>
        </Button>
      </Tooltip>
    </header>

    <section class="designer-shell">
      <VFormDesigner class="vform-workspace" />
    </section>

    <Modal
      v-model:open="helpOpen"
      centered
      :footer="null"
      title="表单设计说明"
      :width="560"
    >
      <div class="help-content">
        <Alert
          show-icon
          type="warning"
          message="只导入来源可信的表单 JSON"
          description="VForm 支持自定义函数和校验脚本，导入陌生文件并预览时，文件中的脚本可能在当前后台页面执行。"
        />
        <div>
          <strong>怎么使用</strong>
          <p>
            从左侧拖入字段，在右侧调整标题、校验和布局，中间区域就是最终表单结构。
          </p>
        </div>
        <div>
          <strong>怎么保存</strong>
          <p>
            当前模块和旧版一样，没有服务端保存接口。离开页面前请点击“导出
            JSON”，下次通过“导入”继续编辑。
          </p>
        </div>
        <div>
          <strong>兼容范围</strong>
          <p>
            沿用旧版 VForm 3.0.10，旧版导出的表单 JSON 可以直接导入，新版导出的
            JSON 也能回到旧版使用。
          </p>
        </div>
      </div>
    </Modal>
  </main>
</template>

<style scoped>
.form-designer-page {
  display: flex;
  flex-direction: column;
  gap: 10px;
  height: calc(100vh - 112px);
  min-height: 660px;
  padding: 12px;
  overflow: hidden;
  background: hsl(var(--background));
}

.workspace-bar {
  display: flex;
  flex: none;
  gap: 16px;
  align-items: center;
  justify-content: space-between;
  min-height: 58px;
  padding: 9px 12px;
  background: hsl(var(--card));
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.workspace-bar h1 {
  margin: 0;
  font-size: 17px;
  font-weight: 650;
  line-height: 24px;
  letter-spacing: 0;
}

.workspace-bar p {
  margin: 3px 0 0;
  font-size: 13px;
  line-height: 20px;
  color: hsl(var(--muted-foreground));
}

.designer-shell {
  flex: 1;
  min-height: 0;
  overflow: hidden;
  background: #fff;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.vform-workspace {
  width: 100%;
  height: 100%;
  overflow: hidden !important;
}

.help-content {
  display: grid;
  gap: 14px;
}

.help-content > div {
  padding: 12px 14px;
  background: hsl(var(--muted) / 35%);
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.help-content strong {
  font-size: 14px;
}

.help-content p {
  margin: 5px 0 0;
  font-size: 13px;
  line-height: 1.7;
  color: hsl(var(--muted-foreground));
}

@media (max-width: 900px) {
  .form-designer-page {
    height: calc(100vh - 104px);
    min-height: 620px;
    overflow: auto;
  }

  .designer-shell {
    min-width: 980px;
    min-height: 640px;
  }
}
</style>

<style>
.form-designer-page .v-form-designer-container,
.form-designer-page .el-container {
  height: 100%;
}

.form-designer-page .el-header.main-header {
  display: none;
}

.form-designer-page .el-aside.side-panel,
.form-designer-page .el-aside.setting-panel {
  border-color: #e5e7eb;
}

.form-designer-page .el-aside.side-panel {
  width: 240px !important;
}

.form-designer-page .el-aside.setting-panel {
  width: 280px !important;
}

.form-designer-page .el-container.center-layout-container {
  min-width: 620px;
}

.form-designer-page .widget-collapse .container-widget-item,
.form-designer-page .widget-collapse .field-widget-item {
  width: 104px !important;
}

.form-designer-page .container-widget-item .svg-icon,
.form-designer-page .field-widget-item .svg-icon {
  display: inline-block;
  vertical-align: -0.15em;
}

.form-designer-page .container-widget-item > span,
.form-designer-page .field-widget-item > span {
  display: inline-flex;
  align-items: center;
  height: 28px;
  line-height: 28px;
}

.form-designer-page .side-scroll-bar .el-scrollbar__wrap,
.form-designer-page .setting-scroll-bar .el-scrollbar__wrap {
  overflow-x: hidden;
}

.form-designer-page .el-button,
.form-designer-page .el-input__wrapper,
.form-designer-page .el-select__wrapper,
.form-designer-page .el-dialog {
  border-radius: 6px;
}

.dark .form-designer-page .designer-shell {
  color-scheme: light;
}
</style>

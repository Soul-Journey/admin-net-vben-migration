<script setup lang="ts">
import type { FormInstance, UploadProps } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type { SaveSystemInfoParams, SystemInfoRecord } from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { IconifyIcon } from '@vben/icons';
import { useUserStore } from '@vben/stores';

import {
  Alert,
  Avatar,
  Button,
  Form,
  Input,
  message,
  Radio,
  Select,
  Skeleton,
  Upload,
} from 'ant-design-vue';

import { getSystemInfoApi, saveSystemInfoApi } from '#/api';
import { applySystemBranding } from '#/store';

defineOptions({ name: 'AdminNetSystemInfoSetting' });

const ENABLED = 1;
const DISABLED = 2;
const SUPER_ADMIN_ACCOUNT = 999;
const SYS_ADMIN_ACCOUNT = 888;

const userStore = useUserStore();
const loading = ref(true);
const saving = ref(false);
const formRef = ref<FormInstance>();
const logoPreview = ref('');
const registrationWays = ref<SystemInfoRecord['wayList']>([]);
const formState = reactive<SaveSystemInfoParams>({
  captcha: DISABLED,
  copyright: '',
  enableReg: DISABLED,
  icp: '',
  icpUrl: '',
  logoBase64: '',
  logoFileName: '',
  regWayId: undefined,
  secondVer: DISABLED,
  title: '',
  viceDesc: '',
  viceTitle: '',
  watermark: '',
});

const accountType = computed(() =>
  Number((userStore.userInfo as any)?.accountType),
);
const canEdit = computed(() =>
  [SUPER_ADMIN_ACCOUNT, SYS_ADMIN_ACCOUNT].includes(accountType.value),
);
const registrationEnabled = computed(() => formState.enableReg === ENABLED);
const registrationOptions = computed(() =>
  registrationWays.value.map((item) => ({
    label: item.label,
    value: item.value,
  })),
);

function validateIcpUrl(_rule: Rule, value: string) {
  if (!value) return Promise.reject(new Error('请输入备案链接'));
  if (value.length > 32)
    return Promise.reject(new Error('备案链接不能超过 32 个字符'));
  try {
    const parsed = new URL(value);
    if (!['http:', 'https:'].includes(parsed.protocol)) {
      throw new Error('Unsupported protocol');
    }
  } catch {
    return Promise.reject(
      new Error('请输入以 http:// 或 https:// 开头的完整链接'),
    );
  }
  return Promise.resolve();
}

function validateRegistrationWay(_rule: Rule, value?: number) {
  if (registrationEnabled.value && !value) {
    return Promise.reject(new Error('启用用户注册后必须选择注册方案'));
  }
  return Promise.resolve();
}

const rules: Record<string, Rule[]> = {
  copyright: [
    { required: true, message: '请输入版权说明', trigger: 'blur' },
    { max: 64, message: '版权说明不能超过 64 个字符', trigger: 'blur' },
  ],
  icp: [
    { required: true, message: '请输入 ICP 备案号', trigger: 'blur' },
    { max: 32, message: '备案号不能超过 32 个字符', trigger: 'blur' },
  ],
  icpUrl: [{ validator: validateIcpUrl, trigger: 'blur' }],
  regWayId: [{ validator: validateRegistrationWay, trigger: 'change' }],
  title: [
    { required: true, message: '请输入系统主标题', trigger: 'blur' },
    { max: 32, message: '主标题不能超过 32 个字符', trigger: 'blur' },
  ],
  viceDesc: [
    { required: true, message: '请输入系统描述', trigger: 'blur' },
    { max: 64, message: '系统描述不能超过 64 个字符', trigger: 'blur' },
  ],
  viceTitle: [
    { required: true, message: '请输入系统副标题', trigger: 'blur' },
    { max: 32, message: '副标题不能超过 32 个字符', trigger: 'blur' },
  ],
  watermark: [{ max: 32, message: '水印不能超过 32 个字符', trigger: 'blur' }],
};

function fileToBase64(file: File) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.addEventListener('error', reject);
    reader.addEventListener('load', () => resolve(String(reader.result)));
    reader.readAsDataURL(file);
  });
}

const beforeLogoUpload: UploadProps['beforeUpload'] = async (file) => {
  const rawFile = file as File;
  if (!['image/jpeg', 'image/png'].includes(rawFile.type)) {
    message.error('系统图标仅支持 PNG、JPG 或 JPEG 格式');
    return Upload.LIST_IGNORE;
  }
  if (rawFile.size > 2 * 1024 * 1024) {
    message.error('系统图标不能超过 2MB');
    return Upload.LIST_IGNORE;
  }

  logoPreview.value = URL.createObjectURL(rawFile);
  formState.logoBase64 = await fileToBase64(rawFile);
  formState.logoFileName = rawFile.name;
  return false;
};

function applyInfo(info: SystemInfoRecord) {
  registrationWays.value = info.wayList ?? [];
  logoPreview.value = info.logo || '';
  Object.assign(formState, {
    captcha: info.captcha ?? DISABLED,
    copyright: info.copyright || '',
    enableReg: info.enableReg ?? DISABLED,
    icp: info.icp || '',
    icpUrl: info.icpUrl || '',
    logoBase64: '',
    logoFileName: '',
    regWayId: info.regWayId || undefined,
    secondVer: info.secondVer ?? DISABLED,
    title: info.title || '',
    viceDesc: info.viceDesc || '',
    viceTitle: info.viceTitle || '',
    watermark: info.watermark || '',
  });
}

async function loadInfo() {
  loading.value = true;
  try {
    applyInfo(await getSystemInfoApi());
  } finally {
    loading.value = false;
  }
}

async function saveInfo() {
  await formRef.value?.validate();
  saving.value = true;
  try {
    const payload: SaveSystemInfoParams = {
      ...formState,
      regWayId: registrationEnabled.value ? formState.regWayId : undefined,
    };
    await saveSystemInfoApi(payload);
    const latest = await getSystemInfoApi();
    applyInfo(latest);
    applySystemBranding(latest);
    message.success('系统信息已保存，标题、Logo 和水印已同步更新');
  } finally {
    saving.value = false;
  }
}

onMounted(loadInfo);
</script>

<template>
  <div class="info-page">
    <section class="page-panel">
      <header class="panel-heading">
        <div>
          <h2>系统配置</h2>
          <p>维护当前租户的品牌信息、页面水印、备案信息和用户注册入口</p>
        </div>
        <Button
          v-if="canEdit"
          type="primary"
          :loading="saving"
          @click="saveInfo"
        >
          <template #icon><IconifyIcon icon="lucide:save" /></template>
          保存配置
        </Button>
      </header>

      <Skeleton v-if="loading" active :paragraph="{ rows: 10 }" />
      <template v-else>
        <Alert
          v-if="!canEdit"
          class="permission-alert"
          message="当前账号可查看系统配置，但只有超级管理员或系统管理员可以修改。"
          show-icon
          type="info"
        />

        <div class="content-grid">
          <Form
            ref="formRef"
            class="info-form"
            layout="vertical"
            :disabled="!canEdit"
            :model="formState"
            :rules="rules"
          >
            <div class="form-section">
              <div class="section-title">
                <IconifyIcon icon="lucide:palette" />
                <div>
                  <strong>品牌信息</strong
                  ><span>用于登录页、侧栏标题和浏览器页面</span>
                </div>
              </div>

              <Form.Item label="系统图标">
                <Upload
                  accept=".jpg,.jpeg,.png"
                  :before-upload="beforeLogoUpload"
                  :max-count="1"
                  :show-upload-list="false"
                >
                  <div class="logo-upload">
                    <Avatar
                      v-if="logoPreview"
                      shape="square"
                      :size="56"
                      :src="logoPreview"
                    />
                    <IconifyIcon v-else icon="lucide:image-plus" />
                    <div><b>更换图标</b><small>PNG/JPG，不超过 2MB</small></div>
                  </div>
                </Upload>
              </Form.Item>

              <div class="form-grid">
                <Form.Item label="系统主标题" name="title">
                  <Input
                    v-model:value="formState.title"
                    :maxlength="32"
                    show-count
                  />
                </Form.Item>
                <Form.Item label="系统副标题" name="viceTitle">
                  <Input
                    v-model:value="formState.viceTitle"
                    :maxlength="32"
                    show-count
                  />
                </Form.Item>
              </div>
              <Form.Item label="系统描述" name="viceDesc">
                <Input.TextArea
                  v-model:value="formState.viceDesc"
                  :auto-size="{ minRows: 2, maxRows: 3 }"
                  :maxlength="64"
                  show-count
                />
              </Form.Item>
              <Form.Item label="页面水印" name="watermark">
                <Input
                  v-model:value="formState.watermark"
                  :maxlength="32"
                  placeholder="留空即关闭页面水印"
                  show-count
                />
              </Form.Item>
            </div>

            <div class="form-section">
              <div class="section-title">
                <IconifyIcon icon="lucide:badge-check" />
                <div>
                  <strong>版权与备案</strong
                  ><span>显示在登录页底部，并提供备案跳转</span>
                </div>
              </div>
              <Form.Item label="版权说明" name="copyright">
                <Input
                  v-model:value="formState.copyright"
                  :maxlength="64"
                  show-count
                />
              </Form.Item>
              <div class="form-grid">
                <Form.Item label="ICP 备案号" name="icp">
                  <Input v-model:value="formState.icp" :maxlength="32" />
                </Form.Item>
                <Form.Item label="备案链接" name="icpUrl">
                  <Input v-model:value="formState.icpUrl" :maxlength="32" />
                </Form.Item>
              </div>
            </div>

            <div class="form-section">
              <div class="section-title">
                <IconifyIcon icon="lucide:user-plus" />
                <div>
                  <strong>用户注册</strong
                  ><span>控制登录页是否开放自助注册入口</span>
                </div>
              </div>
              <div class="form-grid registration-grid">
                <Form.Item label="允许用户注册" name="enableReg">
                  <Radio.Group v-model:value="formState.enableReg">
                    <Radio :value="ENABLED">启用</Radio>
                    <Radio :value="DISABLED">关闭</Radio>
                  </Radio.Group>
                </Form.Item>
                <Form.Item
                  v-if="registrationEnabled"
                  label="默认注册方案"
                  name="regWayId"
                >
                  <Select
                    v-model:value="formState.regWayId"
                    allow-clear
                    :options="registrationOptions"
                    :placeholder="
                      registrationOptions.length > 0
                        ? '请选择注册方案'
                        : '当前租户暂无注册方案'
                    "
                  />
                </Form.Item>
              </div>
              <Alert
                v-if="registrationEnabled && registrationOptions.length === 0"
                message="请先到“注册方案”页面创建并启用一个方案，再开放用户注册。"
                show-icon
                type="warning"
              />
            </div>
          </Form>

          <aside class="preview-panel">
            <div class="preview-heading">
              <div><b>实时预览</b><span>保存后应用到当前租户</span></div>
              <span class="preview-status">当前配置</span>
            </div>
            <div class="brand-preview">
              <div class="brand-bar">
                <Avatar
                  v-if="logoPreview"
                  shape="square"
                  :size="34"
                  :src="logoPreview"
                />
                <div v-else class="brand-logo">
                  <IconifyIcon icon="lucide:blocks" />
                </div>
                <b>{{ formState.title || '系统主标题' }}</b>
              </div>
              <div class="preview-body">
                <div class="preview-logo">
                  <Avatar
                    v-if="logoPreview"
                    shape="square"
                    :size="54"
                    :src="logoPreview"
                  />
                  <IconifyIcon v-else icon="lucide:blocks" />
                </div>
                <h3>{{ formState.viceTitle || '系统副标题' }}</h3>
                <p>{{ formState.viceDesc || '系统描述将显示在这里' }}</p>
                <span v-if="formState.watermark" class="watermark-preview">
                  {{ formState.watermark }}
                </span>
              </div>
              <footer>
                <span>{{ formState.copyright || '版权说明' }}</span>
                <a :href="formState.icpUrl || undefined" target="_blank">
                  {{ formState.icp || 'ICP备案号' }}
                </a>
              </footer>
            </div>
            <div class="preview-note">
              <IconifyIcon icon="lucide:info" />
              <span
                >Logo、标题和水印保存后立即生效；其他已打开的浏览器页面刷新后生效。</span
              >
            </div>
          </aside>
        </div>
      </template>
    </section>
  </div>
</template>

<style scoped>
.info-page {
  min-height: 100%;
  padding: 12px;
  background: hsl(var(--background-deep));
}

.page-panel {
  min-height: calc(100vh - 120px);
  padding: 16px 18px 20px;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border));
  border-radius: 8px;
}

.panel-heading {
  display: flex;
  gap: 16px;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 14px;
}

.panel-heading h2 {
  margin: 0;
  font-size: 17px;
  font-weight: 650;
}

.panel-heading p {
  margin: 4px 0 0;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.permission-alert {
  margin-bottom: 14px;
}

.content-grid {
  display: grid;
  grid-template-columns: minmax(560px, 1fr) minmax(300px, 380px);
  gap: 18px;
  align-items: start;
}

.info-form {
  min-width: 0;
}

.form-section {
  padding: 14px 0 4px;
  border-top: 1px solid hsl(var(--border));
}

.form-section:first-child {
  padding-top: 4px;
  border-top: 0;
}

.section-title {
  display: flex;
  gap: 9px;
  align-items: center;
  margin-bottom: 14px;
  color: hsl(var(--foreground));
}

.section-title > svg {
  width: 17px;
  height: 17px;
  color: hsl(var(--primary));
}

.section-title div {
  display: flex;
  gap: 10px;
  align-items: baseline;
}

.section-title strong {
  font-size: 14px;
}

.section-title span {
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 14px;
}

.logo-upload {
  display: flex;
  gap: 12px;
  align-items: center;
  min-width: 230px;
  padding: 8px 12px;
  border: 1px dashed hsl(var(--border));
  border-radius: 7px;
  transition:
    border-color 0.2s,
    background 0.2s;
}

.logo-upload:hover {
  background: hsl(var(--primary) / 4%);
  border-color: hsl(var(--primary));
}

.logo-upload > svg {
  width: 28px;
  height: 28px;
  color: hsl(var(--muted-foreground));
}

.logo-upload div {
  display: flex;
  flex-direction: column;
}

.logo-upload b {
  font-size: 13px;
}

.logo-upload small {
  margin-top: 3px;
  font-size: 11px;
  color: hsl(var(--muted-foreground));
}

.preview-panel {
  position: sticky;
  top: 12px;
  padding-left: 18px;
  border-left: 1px solid hsl(var(--border));
}

.preview-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
}

.preview-heading div {
  display: flex;
  flex-direction: column;
}

.preview-heading b {
  font-size: 14px;
}

.preview-heading span {
  font-size: 11px;
  color: hsl(var(--muted-foreground));
}

.preview-status {
  padding: 2px 7px;
  color: hsl(var(--primary)) !important;
  background: hsl(var(--primary) / 6%);
  border: 1px solid hsl(var(--primary) / 30%);
  border-radius: 5px;
}

.brand-preview {
  overflow: hidden;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border));
  border-radius: 8px;
  box-shadow: 0 8px 26px rgb(15 23 42 / 7%);
}

.brand-bar {
  display: flex;
  gap: 10px;
  align-items: center;
  height: 58px;
  padding: 0 14px;
  border-bottom: 1px solid hsl(var(--border));
}

.brand-bar b {
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 14px;
  white-space: nowrap;
}

.brand-logo {
  display: grid;
  place-items: center;
  width: 34px;
  height: 34px;
  color: white;
  background: hsl(var(--primary));
  border-radius: 7px;
}

.preview-body {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 230px;
  padding: 28px 22px;
  overflow: hidden;
  text-align: center;
  background: linear-gradient(
    145deg,
    hsl(var(--primary) / 8%),
    hsl(var(--background)) 65%
  );
}

.preview-logo {
  display: grid;
  place-items: center;
  width: 62px;
  height: 62px;
  color: hsl(var(--primary));
}

.preview-logo > svg {
  width: 46px;
  height: 46px;
}

.preview-body h3 {
  z-index: 1;
  margin: 13px 0 5px;
  font-size: 18px;
}

.preview-body p {
  z-index: 1;
  margin: 0;
  font-size: 12px;
  line-height: 1.6;
  color: hsl(var(--muted-foreground));
}

.watermark-preview {
  position: absolute;
  right: -18px;
  bottom: 38px;
  font-size: 20px;
  font-weight: 700;
  color: hsl(var(--foreground) / 8%);
  transform: rotate(-28deg);
}

.brand-preview footer {
  display: flex;
  gap: 8px;
  align-items: center;
  justify-content: space-between;
  min-height: 48px;
  padding: 8px 12px;
  font-size: 10px;
  color: hsl(var(--muted-foreground));
  border-top: 1px solid hsl(var(--border));
}

.brand-preview footer a {
  color: hsl(var(--primary));
}

.preview-note {
  display: flex;
  gap: 7px;
  margin-top: 10px;
  font-size: 11px;
  line-height: 1.55;
  color: hsl(var(--muted-foreground));
}

.preview-note svg {
  flex: 0 0 auto;
  margin-top: 2px;
}

:deep(.ant-form-item) {
  margin-bottom: 14px;
}

:deep(.ant-form-item-label) {
  padding-bottom: 4px;
}

@media (max-width: 980px) {
  .content-grid {
    grid-template-columns: 1fr;
  }

  .preview-panel {
    position: static;
    padding: 14px 0 0;
    border-top: 1px solid hsl(var(--border));
    border-left: 0;
  }
}

@media (max-width: 640px) {
  .info-page {
    padding: 8px;
  }

  .page-panel {
    padding: 14px 12px;
  }

  .panel-heading {
    flex-direction: column;
    align-items: stretch;
  }

  .form-grid {
    grid-template-columns: 1fr;
  }

  .section-title div {
    flex-direction: column;
    gap: 2px;
    align-items: flex-start;
  }
}
</style>

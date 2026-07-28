<script setup lang="ts">
import type { FormInstance, TreeProps } from 'ant-design-vue';
import type { Dayjs } from 'dayjs';

import type { PersonalInfoRecord, UpdatePersonalInfoParams } from '#/api';

import {
  computed,
  nextTick,
  onBeforeUnmount,
  onMounted,
  reactive,
  ref,
  watch,
} from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';

import {
  Avatar,
  Button,
  Col,
  DatePicker,
  Empty,
  Form,
  Input,
  InputNumber,
  message,
  Modal,
  Radio,
  Row,
  Space,
  Tabs,
  Tree,
} from 'ant-design-vue';
import dayjs from 'dayjs';
import SignaturePad from 'signature_pad';

import {
  changePersonalPasswordApi,
  getOrgListApi,
  getPersonalInfoApi,
  resolvePersonalFileUrl,
  updatePersonalInfoApi,
  uploadAvatarApi,
  uploadSignatureApi,
} from '#/api';
import { useAuthStore } from '#/store';

defineOptions({ name: 'AdminNetUserCenter' });

const IMAGE_LIMIT = 5 * 1024 * 1024;
const { hasAccessByCodes } = useAccess();
const authStore = useAuthStore();

const loading = ref(false);
const saveLoading = ref(false);
const avatarLoading = ref(false);
const signatureLoading = ref(false);
const passwordLoading = ref(false);
const activeTab = ref('profile');
const profileFormRef = ref<FormInstance>();
const passwordFormRef = ref<FormInstance>();
const avatarInputRef = ref<HTMLInputElement>();
const signatureInputRef = ref<HTMLInputElement>();
const signatureCanvasRef = ref<HTMLCanvasElement>();
const signatureOpen = ref(false);
const signatureWidth = ref(1.4);
const signatureColor = ref('#111827');
const birthdayValue = ref<Dayjs>();
const info = ref<PersonalInfoRecord>({ account: '', id: 0, realName: '' });
const formState = reactive<UpdatePersonalInfoParams>({ realName: '', sex: 1 });
const passwordState = reactive({
  confirmPassword: '',
  newPassword: '',
  oldPassword: '',
});
const orgTreeData = ref<TreeProps['treeData']>([]);
const expandedOrgKeys = ref<(number | string)[]>([]);
const selectedOrgKeys = computed(() =>
  info.value.orgId ? [info.value.orgId] : [],
);
let signaturePad: SignaturePad | undefined;

const avatarUrl = computed(
  () => resolvePersonalFileUrl(info.value.avatar) || '/upload/logo.png',
);
const signatureUrl = computed(() =>
  resolvePersonalFileUrl(info.value.signature),
);

function can(code: string) {
  return hasAccessByCodes([code]);
}

function assignProfile(record: PersonalInfoRecord) {
  info.value = record;
  Object.assign(formState, {
    address: record.address,
    birthday: record.birthday?.slice(0, 10),
    email: record.email,
    introduction: record.introduction,
    nickName: record.nickName,
    phone: record.phone,
    realName: record.realName,
    remark: record.remark,
    sex: record.sex ?? 1,
  });
  birthdayValue.value = formState.birthday
    ? dayjs(formState.birthday)
    : undefined;
}

async function loadProfile() {
  loading.value = true;
  try {
    assignProfile(await getPersonalInfoApi());
  } finally {
    loading.value = false;
  }
}

function toTreeData(
  nodes: Awaited<ReturnType<typeof getOrgListApi>>,
): TreeProps['treeData'] {
  return nodes.map((node) => ({
    children: node.children ? toTreeData(node.children) : undefined,
    key: node.id,
    title: `${node.name}${node.code ? ` · ${node.code}` : ''}`,
  }));
}

function findOrgPath(
  nodes: Awaited<ReturnType<typeof getOrgListApi>>,
  id?: number,
  path: number[] = [],
): number[] {
  for (const node of nodes) {
    const nextPath = [...path, node.id];
    if (node.id === id) return nextPath;
    const childPath = findOrgPath(node.children ?? [], id, nextPath);
    if (childPath.length > 0) return childPath;
  }
  return [];
}

async function loadOrgTree() {
  const nodes = await getOrgListApi();
  orgTreeData.value = toTreeData(nodes);
  const currentPath = findOrgPath(nodes, info.value.orgId);
  expandedOrgKeys.value =
    currentPath.length > 0
      ? currentPath.slice(0, -1)
      : nodes.map((node) => node.id);
}

async function saveProfile() {
  await profileFormRef.value?.validate();
  formState.birthday = birthdayValue.value?.format('YYYY-MM-DD');
  saveLoading.value = true;
  try {
    await updatePersonalInfoApi({ ...formState });
    await Promise.all([loadProfile(), authStore.fetchUserInfo()]);
    message.success('个人资料已保存');
  } finally {
    saveLoading.value = false;
  }
}

function validateImage(file: File, signature = false) {
  if (!file.type.startsWith('image/')) {
    message.warning('请选择图片文件');
    return false;
  }
  if (signature && file.type !== 'image/png') {
    message.warning('手写签名请使用 PNG 图片');
    return false;
  }
  if (file.size > IMAGE_LIMIT) {
    message.warning('图片大小不能超过 5MB');
    return false;
  }
  return true;
}

async function handleAvatarFile(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  if (!file || !validateImage(file)) return;
  avatarLoading.value = true;
  try {
    await uploadAvatarApi(file);
    await Promise.all([loadProfile(), authStore.fetchUserInfo()]);
    message.success('头像已更新');
  } finally {
    avatarLoading.value = false;
  }
}

async function handleSignatureFile(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  if (!file || !validateImage(file, true)) return;
  signatureLoading.value = true;
  try {
    await uploadSignatureApi(file);
    await loadProfile();
    message.success('电子签名已更新');
  } finally {
    signatureLoading.value = false;
  }
}

function initializeSignaturePad() {
  const canvas = signatureCanvasRef.value;
  if (!canvas) return;
  const ratio = Math.max(window.devicePixelRatio || 1, 1);
  canvas.width = canvas.clientWidth * ratio;
  canvas.height = 240 * ratio;
  canvas.getContext('2d')?.scale(ratio, ratio);
  signaturePad?.off();
  signaturePad = new SignaturePad(canvas, {
    maxWidth: signatureWidth.value + 1.2,
    minWidth: signatureWidth.value,
    penColor: signatureColor.value,
  });
}

async function openSignaturePad() {
  signatureOpen.value = true;
  await nextTick();
  initializeSignaturePad();
}

function undoSignature() {
  const data = signaturePad?.toData() ?? [];
  if (data.length > 0) signaturePad?.fromData(data.slice(0, -1));
}

async function saveDrawnSignature() {
  if (!signaturePad || signaturePad.isEmpty()) {
    message.warning('请先绘制签名');
    return;
  }
  signatureLoading.value = true;
  try {
    const response = await fetch(signaturePad.toDataURL('image/png'));
    const blob = await response.blob();
    await uploadSignatureApi(
      new File([blob], `${info.value.account || 'signature'}.png`, {
        type: 'image/png',
      }),
    );
    signatureOpen.value = false;
    await loadProfile();
    message.success('电子签名已保存');
  } finally {
    signatureLoading.value = false;
  }
}

function resetPasswordForm() {
  passwordState.oldPassword = '';
  passwordState.newPassword = '';
  passwordState.confirmPassword = '';
  passwordFormRef.value?.clearValidate();
}

async function changePassword() {
  await passwordFormRef.value?.validate();
  passwordLoading.value = true;
  try {
    await changePersonalPasswordApi(
      passwordState.oldPassword,
      passwordState.newPassword,
    );
    message.success('密码已修改，请重新登录');
    await authStore.logout(false);
  } finally {
    passwordLoading.value = false;
  }
}

watch(signatureColor, (value) => {
  if (signaturePad) signaturePad.penColor = value;
});

watch(signatureWidth, (value) => {
  if (signaturePad) {
    signaturePad.minWidth = value;
    signaturePad.maxWidth = value + 1.2;
  }
});

watch(activeTab, async (tab) => {
  if (tab === 'organization' && !orgTreeData.value?.length) await loadOrgTree();
});

onMounted(loadProfile);
onBeforeUnmount(() => signaturePad?.off());
</script>

<template>
  <div class="user-center-page">
    <aside class="profile-summary">
      <div class="avatar-wrap">
        <Avatar :size="88" :src="avatarUrl">
          {{ info.realName.slice(0, 1) }}
        </Avatar>
        <Button
          v-if="can('sysFile:uploadAvatar')"
          :loading="avatarLoading"
          class="avatar-action"
          shape="circle"
          size="small"
          title="更换头像"
          @click="avatarInputRef?.click()"
        >
          <template #icon><IconifyIcon icon="lucide:camera" /></template>
        </Button>
        <input
          ref="avatarInputRef"
          accept="image/*"
          hidden
          type="file"
          @change="handleAvatarFile"
        />
      </div>
      <div class="real-name">{{ info.realName || info.account }}</div>
      <div class="account-name">@{{ info.account }}</div>

      <div class="identity-list">
        <div>
          <IconifyIcon icon="lucide:building-2" /><span>{{
            info.orgName || '未分配机构'
          }}</span>
        </div>
        <div>
          <IconifyIcon icon="lucide:briefcase-business" /><span>{{
            info.posName || '未分配职位'
          }}</span>
        </div>
        <div>
          <IconifyIcon icon="lucide:map-pin" /><span>{{
            info.address || '未填写地址'
          }}</span>
        </div>
      </div>

      <div class="signature-block">
        <div class="section-label">电子签名</div>
        <div class="signature-preview">
          <img v-if="signatureUrl" :src="signatureUrl" alt="电子签名" />
          <Empty
            v-else
            :image-style="{ height: '40px' }"
            description="暂未设置"
          />
        </div>
        <Space v-if="can('sysFile:uploadSignature')" wrap>
          <Button
            :loading="signatureLoading"
            size="small"
            @click="openSignaturePad"
          >
            <template #icon><IconifyIcon icon="lucide:pen-line" /></template>
            手写
          </Button>
          <Button
            :loading="signatureLoading"
            size="small"
            @click="signatureInputRef?.click()"
          >
            <template #icon><IconifyIcon icon="lucide:upload" /></template>
            上传 PNG
          </Button>
        </Space>
        <input
          ref="signatureInputRef"
          accept="image/png"
          hidden
          type="file"
          @change="handleSignatureFile"
        />
      </div>
    </aside>

    <section class="profile-content">
      <Tabs v-model:active-key="activeTab">
        <Tabs.TabPane key="profile" tab="基本资料">
          <Form
            ref="profileFormRef"
            :model="formState"
            class="profile-form"
            layout="vertical"
          >
            <Row :gutter="16">
              <Col :md="12" :xs="24">
                <Form.Item
                  label="真实姓名"
                  name="realName"
                  :rules="[{ required: true, message: '请输入真实姓名' }]"
                >
                  <Input
                    v-model:value="formState.realName"
                    :maxlength="32"
                    allow-clear
                  />
                </Form.Item>
              </Col>
              <Col :md="12" :xs="24">
                <Form.Item label="昵称" name="nickName">
                  <Input
                    v-model:value="formState.nickName"
                    :maxlength="32"
                    allow-clear
                  />
                </Form.Item>
              </Col>
              <Col :md="12" :xs="24">
                <Form.Item label="手机号码" name="phone">
                  <Input
                    v-model:value="formState.phone"
                    :maxlength="16"
                    allow-clear
                  />
                </Form.Item>
              </Col>
              <Col :md="12" :xs="24">
                <Form.Item
                  label="邮箱"
                  name="email"
                  :rules="[{ type: 'email', message: '请输入正确的邮箱地址' }]"
                >
                  <Input
                    v-model:value="formState.email"
                    :maxlength="64"
                    allow-clear
                  />
                </Form.Item>
              </Col>
              <Col :md="12" :xs="24">
                <Form.Item label="出生日期" name="birthday">
                  <DatePicker
                    v-model:value="birthdayValue"
                    class="full-width"
                    format="YYYY-MM-DD"
                  />
                </Form.Item>
              </Col>
              <Col :md="12" :xs="24">
                <Form.Item label="性别" name="sex">
                  <Radio.Group v-model:value="formState.sex">
                    <Radio :value="1">男</Radio>
                    <Radio :value="2">女</Radio>
                  </Radio.Group>
                </Form.Item>
              </Col>
              <Col :span="24">
                <Form.Item label="地址" name="address">
                  <Input.TextArea
                    v-model:value="formState.address"
                    :auto-size="{ minRows: 2, maxRows: 3 }"
                    :maxlength="256"
                    show-count
                  />
                </Form.Item>
              </Col>
              <Col :span="24">
                <Form.Item label="个人简介" name="introduction">
                  <Input.TextArea
                    v-model:value="formState.introduction"
                    :auto-size="{ minRows: 2, maxRows: 4 }"
                    :maxlength="512"
                    show-count
                  />
                </Form.Item>
              </Col>
              <Col :span="24">
                <Form.Item label="备注" name="remark">
                  <Input.TextArea
                    v-model:value="formState.remark"
                    :auto-size="{ minRows: 2, maxRows: 3 }"
                    :maxlength="256"
                    show-count
                  />
                </Form.Item>
              </Col>
            </Row>
            <div class="form-actions">
              <Button
                v-if="can('sysUser:baseInfo')"
                :loading="saveLoading"
                type="primary"
                @click="saveProfile"
              >
                <template #icon><IconifyIcon icon="lucide:save" /></template>
                保存基本资料
              </Button>
            </div>
          </Form>
        </Tabs.TabPane>

        <Tabs.TabPane key="organization" tab="组织信息">
          <div class="org-head">
            <div>
              <div class="org-title">所属组织</div>
              <div class="org-subtitle">
                当前账号所在机构已自动定位，树默认只展开到当前位置。
              </div>
            </div>
          </div>
          <Tree
            v-if="orgTreeData?.length"
            v-model:expanded-keys="expandedOrgKeys"
            :auto-expand-parent="true"
            :selected-keys="selectedOrgKeys"
            :tree-data="orgTreeData"
            block-node
            class="org-tree"
          />
          <Empty v-else description="暂无组织数据" />
        </Tabs.TabPane>

        <Tabs.TabPane key="password" tab="修改密码">
          <Form
            ref="passwordFormRef"
            :model="passwordState"
            class="password-form"
            layout="vertical"
          >
            <Form.Item
              label="当前密码"
              name="oldPassword"
              :rules="[{ required: true, message: '请输入当前密码' }]"
            >
              <Input.Password
                v-model:value="passwordState.oldPassword"
                autocomplete="current-password"
              />
            </Form.Item>
            <Form.Item
              label="新密码"
              name="newPassword"
              :rules="[
                { required: true, min: 5, message: '新密码至少 5 个字符' },
              ]"
            >
              <Input.Password
                v-model:value="passwordState.newPassword"
                autocomplete="new-password"
              />
            </Form.Item>
            <Form.Item
              label="确认新密码"
              name="confirmPassword"
              :rules="[
                { required: true, message: '请再次输入新密码' },
                {
                  validator: async () => {
                    if (
                      passwordState.confirmPassword !==
                      passwordState.newPassword
                    )
                      throw new Error('两次输入的密码不一致');
                  },
                },
              ]"
            >
              <Input.Password
                v-model:value="passwordState.confirmPassword"
                autocomplete="new-password"
              />
            </Form.Item>
            <div class="password-note">
              密码修改成功后会退出当前会话，需要使用新密码重新登录。
            </div>
            <Space>
              <Button @click="resetPasswordForm">重置</Button>
              <Button
                v-if="can('sysUser:changePwd')"
                :loading="passwordLoading"
                type="primary"
                @click="changePassword"
              >
                确认修改
              </Button>
            </Space>
          </Form>
        </Tabs.TabPane>
      </Tabs>
    </section>

    <Modal
      v-model:open="signatureOpen"
      :footer="null"
      :mask-closable="false"
      centered
      destroy-on-close
      title="手写电子签名"
      :width="620"
    >
      <canvas ref="signatureCanvasRef" class="signature-canvas"></canvas>
      <div class="signature-tools">
        <Space wrap>
          <span>画笔粗细</span>
          <InputNumber
            v-model:value="signatureWidth"
            :max="3"
            :min="0.6"
            :step="0.2"
            size="small"
          />
          <span>颜色</span>
          <input
            v-model="signatureColor"
            aria-label="画笔颜色"
            class="color-input"
            type="color"
          />
        </Space>
        <Space>
          <Button @click="undoSignature">撤销</Button>
          <Button @click="signaturePad?.clear()">清空</Button>
          <Button
            :loading="signatureLoading"
            type="primary"
            @click="saveDrawnSignature"
          >
            保存签名
          </Button>
        </Space>
      </div>
    </Modal>
  </div>
</template>

<style scoped>
.user-center-page {
  display: grid;
  grid-template-columns: 270px minmax(0, 1fr);
  gap: 12px;
  min-height: 100%;
  padding: 12px;
  background: hsl(var(--muted) / 35%);
}

.profile-summary,
.profile-content {
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border) / 72%);
  border-radius: 8px;
}

.profile-summary {
  padding: 24px 18px;
  text-align: center;
}

.avatar-wrap {
  position: relative;
  display: inline-flex;
}

.avatar-action {
  position: absolute;
  right: -2px;
  bottom: 2px;
  box-shadow: 0 2px 8px rgb(15 23 42 / 16%);
}

.real-name {
  margin-top: 12px;
  font-size: 18px;
  font-weight: 700;
  color: hsl(var(--foreground));
}

.account-name {
  margin-top: 2px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.identity-list {
  display: grid;
  gap: 10px;
  padding: 16px 0;
  margin-top: 22px;
  text-align: left;
  border-block: 1px solid hsl(var(--border));
}

.identity-list > div {
  display: flex;
  gap: 9px;
  align-items: center;
  min-width: 0;
  font-size: 13px;
  color: hsl(var(--muted-foreground));
}

.identity-list span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.signature-block {
  margin-top: 18px;
  text-align: left;
}

.section-label {
  margin-bottom: 8px;
  font-size: 13px;
  font-weight: 600;
  color: hsl(var(--foreground));
}

.signature-preview {
  display: grid;
  place-items: center;
  height: 118px;
  margin-bottom: 10px;
  overflow: hidden;
  background: #fff;
  border: 1px dashed hsl(var(--border));
  border-radius: 6px;
}

.signature-preview img {
  max-width: 100%;
  max-height: 100%;
  object-fit: contain;
}

.profile-content {
  min-width: 0;
  padding: 4px 18px 18px;
}

.profile-form {
  max-width: 860px;
  padding-top: 6px;
}

.full-width {
  width: 100%;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  padding-top: 4px;
}

.password-form {
  max-width: 460px;
  padding-top: 10px;
}

.password-note {
  padding: 9px 12px;
  margin-bottom: 16px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
  background: hsl(var(--muted) / 28%);
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.org-head {
  margin: 6px 0 12px;
}

.org-title {
  font-size: 14px;
  font-weight: 700;
}

.org-subtitle {
  margin-top: 4px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.org-tree {
  min-height: 360px;
  padding: 12px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.signature-canvas {
  display: block;
  width: 100%;
  height: 240px;
  touch-action: none;
  background: #fff;
  border: 1px dashed hsl(var(--border));
  border-radius: 6px;
}

.signature-tools {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  margin-top: 12px;
}

.color-input {
  width: 30px;
  height: 26px;
  padding: 2px;
  cursor: pointer;
  background: #fff;
  border: 1px solid hsl(var(--border));
  border-radius: 5px;
}

@media (max-width: 760px) {
  .user-center-page {
    grid-template-columns: 1fr;
  }

  .profile-summary {
    padding-block: 18px;
  }

  .signature-tools {
    flex-direction: column;
    align-items: flex-start;
  }
}
</style>

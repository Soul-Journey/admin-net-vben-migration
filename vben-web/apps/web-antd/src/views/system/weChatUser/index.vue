<script setup lang="ts">
import type { FormInstance, TableColumnsType } from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';

import type { SaveWechatUserParams, SysWechatUserRecord } from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';

import {
  Avatar,
  Button,
  Col,
  Descriptions,
  Form,
  Input,
  message,
  Modal,
  Popover,
  Radio,
  Row,
  Select,
  Space,
  Table,
  Tag,
} from 'ant-design-vue';

import {
  addWechatUserApi,
  deleteWechatUserApi,
  getDictDataByCodeApi,
  pageWechatUsersApi,
  updateWechatUserApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetSystemWechatUser' });

type PlatformOption = { color?: string; label: string; value: number };

const { hasAccessByCodes } = useAccess();
const loading = ref(false);
const submitLoading = ref(false);
const modalOpen = ref(false);
const formRef = ref<FormInstance>();
const records = ref<SysWechatUserRecord[]>([]);
const platformOptions = ref<PlatformOption[]>([]);
const total = ref(0);
const formState = reactive<SaveWechatUserParams>({
  openId: '',
  platformType: 1,
});
const query = reactive({
  mobile: '',
  nickName: '',
  page: 1,
  pageSize: 50,
});

const columns: TableColumnsType<SysWechatUserRecord> = [
  { key: 'index', title: '序号', width: 58 },
  { dataIndex: 'avatar', key: 'avatar', title: '头像', width: 62 },
  {
    dataIndex: 'nickName',
    ellipsis: true,
    key: 'nickName',
    title: '昵称',
    width: 130,
  },
  { key: 'platformType', title: '平台', width: 110 },
  { dataIndex: 'mobile', key: 'mobile', title: '手机号码', width: 126 },
  {
    dataIndex: 'openId',
    ellipsis: true,
    key: 'openId',
    title: 'OpenId',
    width: 210,
  },
  {
    dataIndex: 'unionId',
    ellipsis: true,
    key: 'unionId',
    title: 'UnionId',
    width: 180,
  },
  { key: 'sex', title: '性别', width: 72 },
  { key: 'region', title: '地区', width: 180 },
  { key: 'binding', title: '系统账号', width: 100 },
  { key: 'modifyRecord', title: '修改记录', width: 104 },
  { fixed: 'right', key: 'actions', title: '操作', width: 122 },
];

const formRules: Record<string, Rule[]> = {
  openId: [{ message: '请输入 OpenId', required: true, trigger: 'blur' }],
  platformType: [
    {
      message: '请选择平台类型',
      required: true,
      trigger: 'change',
      type: 'number',
    },
  ],
};

const modalTitle = computed(() =>
  formState.id ? '编辑第三方账号' : '新增第三方账号',
);

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asRecord(value: unknown) {
  return value as SysWechatUserRecord;
}

function optionLabel(value: number) {
  return (
    platformOptions.value.find((item) => item.value === value)?.label ??
    `未知(${value})`
  );
}

function optionColor(value: number) {
  return (
    platformOptions.value.find((item) => item.value === value)?.color || 'blue'
  );
}

function regionText(record: SysWechatUserRecord) {
  return (
    [record.country, record.province, record.city]
      .filter(Boolean)
      .join(' / ') || '未填写'
  );
}

function resetForm(values: SaveWechatUserParams) {
  for (const key of Object.keys(formState))
    delete formState[key as keyof SaveWechatUserParams];
  Object.assign(formState, values);
}

async function loadPlatforms() {
  const items = await getDictDataByCodeApi('PlatformTypeEnum', 1);
  platformOptions.value = items.map((item) => ({
    color: item.tagType,
    label: item.label,
    value: Number(item.value),
  }));
}

async function loadRecords() {
  if (!can('sysWechatUser:page')) return;
  loading.value = true;
  try {
    const result = await pageWechatUsersApi({
      mobile: query.mobile || undefined,
      nickName: query.nickName || undefined,
      page: query.page,
      pageSize: query.pageSize,
    });
    records.value = result.items ?? [];
    total.value = result.total ?? 0;
  } finally {
    loading.value = false;
  }
}

async function handleQuery() {
  query.page = 1;
  await loadRecords();
}

async function resetQuery() {
  query.mobile = '';
  query.nickName = '';
  await handleQuery();
}

function openCreate() {
  resetForm({ openId: '', platformType: 1, sex: undefined });
  modalOpen.value = true;
}

function openEdit(record: SysWechatUserRecord) {
  resetForm({
    avatar: record.avatar,
    city: record.city,
    country: record.country,
    id: record.id,
    language: record.language,
    mobile: record.mobile,
    nickName: record.nickName,
    openId: record.openId,
    platformType: record.platformType,
    province: record.province,
    sex: record.sex,
    unionId: record.unionId,
  });
  modalOpen.value = true;
}

async function submitRecord() {
  await formRef.value?.validate();
  submitLoading.value = true;
  try {
    if (formState.id) {
      await updateWechatUserApi({ ...formState });
      message.success('第三方账号已更新');
    } else {
      await addWechatUserApi({ ...formState });
      message.success('第三方账号已新增');
    }
    modalOpen.value = false;
    await loadRecords();
  } finally {
    submitLoading.value = false;
  }
}

function confirmDelete(record: SysWechatUserRecord) {
  Modal.confirm({
    centered: true,
    content: `确定删除“${record.nickName || record.openId}”吗？删除后，该 OpenId 对应的第三方登录或绑定关系将失效。`,
    okButtonProps: { danger: true },
    okText: '删除',
    onOk: async () => {
      await deleteWechatUserApi(record.id);
      message.success('第三方账号已删除');
      await loadRecords();
    },
    title: '删除第三方账号',
  });
}

onMounted(async () => {
  await Promise.all([loadPlatforms(), loadRecords()]);
});
</script>

<template>
  <div class="wechat-user-page">
    <section class="panel">
      <div class="panel-head">
        <div>
          <div class="panel-title">第三方账号</div>
          <div class="panel-subtitle">
            管理微信、QQ、支付宝和 Gitee 等外部身份绑定
          </div>
        </div>
      </div>

      <Form :model="query" class="query-form" layout="inline">
        <Form.Item label="昵称">
          <Input
            v-model:value="query.nickName"
            allow-clear
            placeholder="第三方昵称"
            @press-enter="handleQuery"
          />
        </Form.Item>
        <Form.Item label="手机号码">
          <Input
            v-model:value="query.mobile"
            allow-clear
            placeholder="手机号码"
            @press-enter="handleQuery"
          />
        </Form.Item>
        <Form.Item>
          <Space>
            <Button
              v-if="can('sysWechatUser:page')"
              :loading="loading"
              type="primary"
              @click="handleQuery"
            >
              <template #icon><IconifyIcon icon="lucide:search" /></template>
              查询
            </Button>
            <Button @click="resetQuery">
              <template #icon>
                <IconifyIcon icon="lucide:rotate-ccw" />
              </template>
              重置
            </Button>
            <Button
              v-if="can('sysWechatUser:add')"
              type="primary"
              @click="openCreate"
            >
              <template #icon><IconifyIcon icon="lucide:plus" /></template>
              新增
            </Button>
          </Space>
        </Form.Item>
      </Form>

      <Table
        :columns="columns"
        :data-source="records"
        :loading="loading"
        :pagination="{
          ...ADMIN_PAGINATION_PROPS,
          current: query.page,
          pageSize: query.pageSize,
          showTotal: (value: number) => `共 ${value} 条`,
          total,
        }"
        :scroll="{ x: 1500 }"
        row-key="id"
        size="small"
        @change="
          (pagination: any) => {
            query.page = pagination.current;
            query.pageSize = pagination.pageSize;
            loadRecords();
          }
        "
      >
        <template #bodyCell="{ column, index, record }">
          <template v-if="column.key === 'index'">
            {{ (query.page - 1) * query.pageSize + index + 1 }}
          </template>
          <template v-else-if="column.key === 'avatar'">
            <Avatar :size="28" :src="asRecord(record).avatar">
              {{ (asRecord(record).nickName || '?').slice(0, 1) }}
            </Avatar>
          </template>
          <template v-else-if="column.key === 'platformType'">
            <Tag :color="optionColor(asRecord(record).platformType)">
              {{ optionLabel(asRecord(record).platformType) }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'sex'">
            <span>{{
              asRecord(record).sex === 0
                ? '男'
                : asRecord(record).sex === 1
                  ? '女'
                  : '未知'
            }}</span>
          </template>
          <template v-else-if="column.key === 'region'">
            <span class="muted-text" :title="regionText(asRecord(record))">{{
              regionText(asRecord(record))
            }}</span>
          </template>
          <template v-else-if="column.key === 'binding'">
            <Tag :color="asRecord(record).userId ? 'green' : 'default'">
              {{ asRecord(record).userId ? '已绑定' : '未绑定' }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'modifyRecord'">
            <Popover
              overlay-class-name="wechat-record-popover"
              placement="bottom"
              trigger="hover"
            >
              <template #content>
                <Descriptions
                  :column="2"
                  bordered
                  class="modify-record"
                  layout="vertical"
                  size="small"
                >
                  <Descriptions.Item label="创建者">
                    {{ asRecord(record).createUserName || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item label="创建时间">
                    {{ asRecord(record).createTime || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item label="修改者">
                    {{ asRecord(record).updateUserName || '无' }}
                  </Descriptions.Item>
                  <Descriptions.Item label="修改时间">
                    {{ asRecord(record).updateTime || '无' }}
                  </Descriptions.Item>
                </Descriptions>
              </template>
              <Button size="small" type="link">
                <template #icon><IconifyIcon icon="lucide:info" /></template>
                详情
              </Button>
            </Popover>
          </template>
          <template v-else-if="column.key === 'actions'">
            <Space :size="2">
              <Button
                v-if="can('sysWechatUser:update')"
                size="small"
                type="link"
                @click.stop="openEdit(asRecord(record))"
              >
                <template #icon>
                  <IconifyIcon icon="lucide:square-pen" />
                </template>
                编辑
              </Button>
              <Button
                v-if="can('sysWechatUser:delete')"
                danger
                size="small"
                type="link"
                @click.stop="confirmDelete(asRecord(record))"
              >
                <template #icon><IconifyIcon icon="lucide:trash-2" /></template>
                删除
              </Button>
            </Space>
          </template>
        </template>
      </Table>
    </section>

    <Modal
      v-model:open="modalOpen"
      :body-style="{ padding: '14px 20px' }"
      :footer="null"
      :mask-closable="false"
      :title="modalTitle"
      centered
      destroy-on-close
      :width="680"
      @cancel="formRef?.clearValidate()"
    >
      <Form
        ref="formRef"
        :model="formState"
        :rules="formRules"
        layout="vertical"
      >
        <Row :gutter="16">
          <Col :span="8">
            <Form.Item label="平台类型" name="platformType">
              <Select
                v-model:value="formState.platformType"
                :options="platformOptions"
              />
            </Form.Item>
          </Col>
          <Col :span="16">
            <Form.Item label="OpenId" name="openId">
              <Input
                v-model:value="formState.openId"
                :maxlength="64"
                placeholder="第三方平台用户标识"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="昵称" name="nickName">
              <Input
                v-model:value="formState.nickName"
                :maxlength="64"
                allow-clear
                placeholder="第三方昵称"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="UnionId" name="unionId">
              <Input
                v-model:value="formState.unionId"
                :maxlength="64"
                allow-clear
                placeholder="跨应用统一标识"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="手机号码" name="mobile">
              <Input
                v-model:value="formState.mobile"
                :maxlength="16"
                allow-clear
                placeholder="手机号码"
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="性别" name="sex">
              <Radio.Group v-model:value="formState.sex">
                <Radio :value="0">男</Radio>
                <Radio :value="1">女</Radio>
                <Radio :value="null">未知</Radio>
              </Radio.Group>
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="头像地址" name="avatar">
              <Input
                v-model:value="formState.avatar"
                :maxlength="256"
                allow-clear
                placeholder="https://..."
              />
            </Form.Item>
          </Col>
          <Col :span="12">
            <Form.Item label="语言" name="language">
              <Input
                v-model:value="formState.language"
                :maxlength="64"
                allow-clear
                placeholder="例如 zh_CN"
              />
            </Form.Item>
          </Col>
          <Col :span="8">
            <Form.Item label="国家" name="country">
              <Input
                v-model:value="formState.country"
                :maxlength="64"
                allow-clear
              />
            </Form.Item>
          </Col>
          <Col :span="8">
            <Form.Item label="省份" name="province">
              <Input
                v-model:value="formState.province"
                :maxlength="64"
                allow-clear
              />
            </Form.Item>
          </Col>
          <Col :span="8">
            <Form.Item label="城市" name="city">
              <Input
                v-model:value="formState.city"
                :maxlength="64"
                allow-clear
              />
            </Form.Item>
          </Col>
        </Row>
      </Form>
      <div class="form-note">
        令牌和会话密钥由第三方登录流程自动维护，不会显示或通过本页面修改。
      </div>
      <div class="modal-footer">
        <Space>
          <Button @click="modalOpen = false">取消</Button>
          <Button :loading="submitLoading" type="primary" @click="submitRecord">
            确定
          </Button>
        </Space>
      </div>
    </Modal>
  </div>
</template>

<style scoped>
.wechat-user-page {
  min-height: 100%;
  padding: 12px;
  background: hsl(var(--muted) / 35%);
}

.panel {
  min-width: 0;
  padding: 12px;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border) / 72%);
  border-radius: 8px;
}

.panel-head {
  margin-bottom: 10px;
}

.panel-title {
  font-size: 14px;
  font-weight: 700;
  color: hsl(var(--foreground));
}

.panel-subtitle {
  margin-top: 4px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
}

.query-form {
  gap: 8px 0;
  margin-bottom: 10px;
}

.muted-text {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  color: hsl(var(--muted-foreground));
  white-space: nowrap;
}

.form-note {
  padding: 9px 12px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
  background: hsl(var(--muted) / 28%);
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  padding-top: 12px;
  margin-top: 14px;
  border-top: 1px solid hsl(var(--border));
}

:global(.wechat-record-popover .ant-popover-inner) {
  width: 390px;
  padding: 10px;
  background: #fff;
}

:global(.wechat-record-popover .modify-record .ant-descriptions-item-label),
:global(.wechat-record-popover .modify-record .ant-descriptions-item-content) {
  padding: 6px 8px;
  font-size: 12px;
}

:global(.wechat-user-page .ant-table-cell) {
  vertical-align: middle;
}

:global(.wechat-user-page .ant-btn-link) {
  padding-inline: 4px;
}
</style>

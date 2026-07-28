<script setup lang="ts">
import type {
  FormInstance,
  TableColumnsType,
  TablePaginationConfig,
} from 'ant-design-vue';
import type { Rule } from 'ant-design-vue/es/form';
import type { Dayjs } from 'dayjs';

import type {
  CreateWechatNativePayParams,
  WechatPayConfigurationStatus,
  WechatPayRecord,
  WechatRefundRecord,
} from '#/api';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { IconifyIcon } from '@vben/icons';

import { useQRCode } from '@vueuse/integrations/useQRCode';
import {
  Alert,
  Button,
  Checkbox,
  DatePicker,
  Descriptions,
  Empty,
  Form,
  Input,
  InputNumber,
  message,
  Modal,
  Space,
  Table,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  createWechatNativePayApi,
  createWechatRefundApi,
  getWechatPayConfigurationStatusApi,
  getWechatPayInfoApi,
  listWechatRefundsApi,
  pageWechatPaysApi,
  syncWechatPayInfoApi,
} from '#/api';
import { ADMIN_PAGINATION_PROPS } from '#/utils/pagination';

defineOptions({ name: 'AdminNetWechatPay' });

type CreateFormState = CreateWechatNativePayParams & { amountYuan?: number };

const { hasAccessByCodes } = useAccess();
const loading = ref(false);
const creating = ref(false);
const refunding = ref(false);
const configurationLoading = ref(false);
const createModalOpen = ref(false);
const refundModalOpen = ref(false);
const refundListOpen = ref(false);
const detailOpen = ref(false);
const guideOpen = ref(false);
const qrcodeModalOpen = ref(false);
const createConfirmed = ref(false);
const createFormRef = ref<FormInstance>();
const records = ref<WechatPayRecord[]>([]);
const refunds = ref<WechatRefundRecord[]>([]);
const currentRecord = ref<WechatPayRecord>();
const currentDetail = ref<WechatPayRecord>();
const qrcodeText = ref('');
const qrcodeDataUrl = useQRCode(qrcodeText, {
  errorCorrectionLevel: 'H',
  margin: 2,
  width: 280,
});

const emptyConfiguration: WechatPayConfigurationStatus = {
  appIdConfigured: false,
  certificateFileConfigured: false,
  certificateSerialNumberConfigured: false,
  merchantIdConfigured: false,
  merchantV3SecretConfigured: false,
  payCallbackConfigured: false,
  readyForPayment: false,
  readyForRefund: false,
  refundCallbackConfigured: false,
};
const configuration = reactive<WechatPayConfigurationStatus>({
  ...emptyConfiguration,
});

const query = reactive({
  createTimeRange: undefined as [Dayjs, Dayjs] | undefined,
  keyword: '',
  page: 1,
  pageSize: 50,
});
const total = ref(0);
const createForm = reactive<CreateFormState>({
  amountYuan: undefined,
  attachment: '',
  businessId: undefined,
  description: '',
  goodsTag: '',
  tags: '',
  total: 0,
});
const refundForm = reactive({
  confirmation: '',
  reason: '',
});

const configurationItems = computed(() => [
  { key: 'appId', label: 'AppId', ready: configuration.appIdConfigured },
  {
    key: 'merchant',
    label: '商户号',
    ready: configuration.merchantIdConfigured,
  },
  {
    key: 'v3',
    label: 'APIv3 密钥',
    ready: configuration.merchantV3SecretConfigured,
  },
  {
    key: 'serial',
    label: '证书序列号',
    ready: configuration.certificateSerialNumberConfigured,
  },
  {
    key: 'certificate',
    label: '商户证书',
    ready: configuration.certificateFileConfigured,
  },
  {
    key: 'payCallback',
    label: '支付回调',
    ready: configuration.payCallbackConfigured,
  },
  {
    key: 'refundCallback',
    label: '退款回调',
    ready: configuration.refundCallbackConfigured,
  },
]);

const columns: TableColumnsType<WechatPayRecord> = [
  { key: 'index', title: '序号', width: 58 },
  {
    dataIndex: 'outTradeNumber',
    key: 'outTradeNumber',
    title: '商户订单号',
    width: 205,
  },
  {
    dataIndex: 'description',
    ellipsis: true,
    key: 'description',
    title: '商品描述',
    width: 180,
  },
  { key: 'total', title: '订单金额', width: 105 },
  { key: 'tradeState', title: '支付状态', width: 108 },
  { key: 'business', title: '业务关联', width: 155 },
  { dataIndex: 'createTime', key: 'createTime', title: '创建时间', width: 170 },
  {
    dataIndex: 'successTime',
    key: 'successTime',
    title: '完成时间',
    width: 170,
  },
  { fixed: 'right', key: 'actions', title: '操作', width: 230 },
];

const refundColumns: TableColumnsType<WechatRefundRecord> = [
  {
    dataIndex: 'outRefundNumber',
    key: 'outRefundNumber',
    title: '商户退款号',
    width: 210,
  },
  { key: 'refund', title: '退款金额', width: 100 },
  { key: 'tradeState', title: '状态', width: 100 },
  {
    dataIndex: 'reason',
    ellipsis: true,
    key: 'reason',
    title: '退款原因',
    width: 180,
  },
  {
    dataIndex: 'userReceivedAccount',
    ellipsis: true,
    key: 'userReceivedAccount',
    title: '退款入账账户',
    width: 180,
  },
  { dataIndex: 'createTime', key: 'createTime', title: '申请时间', width: 165 },
  {
    dataIndex: 'successTime',
    key: 'successTime',
    title: '完成时间',
    width: 165,
  },
];

const createRules: Record<string, Rule[]> = {
  amountYuan: [
    { required: true, message: '请输入订单金额', trigger: 'change' },
  ],
  description: [
    { required: true, message: '请输入商品描述', trigger: 'blur' },
    { max: 127, message: '商品描述不能超过 127 个字符', trigger: 'blur' },
  ],
};

function can(code: string) {
  return hasAccessByCodes([code]);
}

function asPayRecord(record: unknown) {
  return record as WechatPayRecord;
}

function money(cents?: number) {
  return `¥${((cents ?? 0) / 100).toFixed(2)}`;
}

function valueText(value?: null | number | string) {
  return value === undefined || value === null || value === ''
    ? '无'
    : String(value);
}

function paymentState(value?: string) {
  const states: Record<string, { color: string; text: string }> = {
    CLOSED: { color: 'default', text: '已关闭' },
    NOTPAY: { color: 'default', text: '待支付' },
    PAYERROR: { color: 'error', text: '支付失败' },
    REFUND: { color: 'orange', text: '已退款' },
    REVOKED: { color: 'default', text: '已撤销' },
    SUCCESS: { color: 'success', text: '支付成功' },
    USERPAYING: { color: 'processing', text: '支付中' },
  };
  return states[value || ''] ?? { color: 'default', text: value || '待支付' };
}

function refundState(value?: string) {
  const states: Record<string, { color: string; text: string }> = {
    ABNORMAL: { color: 'error', text: '异常' },
    CLOSED: { color: 'default', text: '已关闭' },
    PROCESSING: { color: 'processing', text: '处理中' },
    SUCCESS: { color: 'success', text: '退款成功' },
  };
  return (
    states[value || ''] ?? { color: 'processing', text: value || '已申请' }
  );
}

async function loadConfiguration() {
  configurationLoading.value = true;
  try {
    Object.assign(configuration, await getWechatPayConfigurationStatusApi());
  } finally {
    configurationLoading.value = false;
  }
}

async function loadRecords() {
  loading.value = true;
  try {
    const range = query.createTimeRange;
    const result = await pageWechatPaysApi({
      createTimeRange: range
        ? [range[0].format('YYYY-MM-DD'), range[1].format('YYYY-MM-DD')]
        : undefined,
      keyword: query.keyword.trim() || undefined,
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
  query.keyword = '';
  query.createTimeRange = undefined;
  query.page = 1;
  await loadRecords();
}

function handleTableChange(pagination: TablePaginationConfig) {
  query.page = pagination.current ?? 1;
  query.pageSize = pagination.pageSize ?? 50;
  void loadRecords();
}

function openCreate() {
  Object.assign(createForm, {
    amountYuan: undefined,
    attachment: '',
    businessId: undefined,
    description: '',
    goodsTag: '',
    tags: '',
    total: 0,
  });
  createConfirmed.value = false;
  createModalOpen.value = true;
}

function openQrcode(content: string) {
  qrcodeText.value = content;
  qrcodeModalOpen.value = true;
}

async function submitCreate() {
  await createFormRef.value?.validate();
  if (!createConfirmed.value) {
    message.warning('请先确认该操作会调用真实微信支付下单接口');
    return;
  }
  const amountYuan = createForm.amountYuan ?? 0;
  const totalInCents = Math.round(amountYuan * 100);
  if (totalInCents <= 0) {
    message.error('订单金额必须大于 0 元');
    return;
  }

  creating.value = true;
  try {
    const result = await createWechatNativePayApi({
      attachment: createForm.attachment?.trim() || undefined,
      businessId: createForm.businessId,
      description: createForm.description.trim(),
      goodsTag: createForm.goodsTag?.trim() || undefined,
      tags: createForm.tags?.trim() || undefined,
      total: totalInCents,
    });
    createModalOpen.value = false;
    openQrcode(result.qrcodeUrl);
    message.success('扫码支付订单已创建，请在二维码有效期内完成测试');
    await loadRecords();
  } finally {
    creating.value = false;
  }
}

async function openDetail(record: WechatPayRecord) {
  currentDetail.value = await getWechatPayInfoApi(record.outTradeNumber);
  detailOpen.value = true;
}

function syncOrder(record: WechatPayRecord) {
  Modal.confirm({
    content:
      '系统将调用微信支付查询接口，并用微信返回结果更新本地订单状态。该操作不会扣款或退款。',
    okText: '查询并同步',
    title: `同步订单 ${record.outTradeNumber}`,
    async onOk() {
      await syncWechatPayInfoApi(record.outTradeNumber);
      message.success('订单状态已从微信支付同步');
      await loadRecords();
    },
  });
}

async function openRefunds(record: WechatPayRecord) {
  if (!record.transactionId) {
    message.info('订单尚无微信支付订单号，当前没有退款记录');
    return;
  }
  currentRecord.value = record;
  refunds.value = await listWechatRefundsApi(record.transactionId);
  refundListOpen.value = true;
}

function openRefund(record: WechatPayRecord) {
  currentRecord.value = record;
  refundForm.reason = '';
  refundForm.confirmation = '';
  refundModalOpen.value = true;
}

async function submitRefund() {
  const record = currentRecord.value;
  if (!record) return;
  if (!refundForm.reason.trim()) {
    message.error('请输入退款原因');
    return;
  }
  if (refundForm.confirmation !== '退款') {
    message.error('请输入“退款”确认本次资金操作');
    return;
  }
  refunding.value = true;
  try {
    await createWechatRefundApi({
      reason: refundForm.reason.trim(),
      refund: record.total,
      total: record.total,
      tradeId: record.outTradeNumber,
    });
    refundModalOpen.value = false;
    message.success('全额退款申请已提交，请关注微信处理结果');
    await loadRecords();
  } finally {
    refunding.value = false;
  }
}

onMounted(async () => {
  await Promise.all([loadConfiguration(), loadRecords()]);
});
</script>

<template>
  <div class="pay-page">
    <section class="page-panel">
      <header class="panel-heading">
        <div>
          <h2>微信支付</h2>
          <p>
            查询扫码支付订单、核对微信状态和处理退款；金额统一以人民币元显示
          </p>
        </div>
        <Space>
          <Button @click="guideOpen = true">
            <template #icon><IconifyIcon icon="lucide:circle-help" /></template>
            使用说明
          </Button>
          <Tooltip
            :title="
              configuration.readyForPayment
                ? ''
                : '微信支付配置未就绪，暂不能创建订单'
            "
          >
            <Button
              v-if="can('sysWechatPay:payTransactionNative')"
              type="primary"
              :disabled="!configuration.readyForPayment"
              @click="openCreate"
            >
              <template #icon><IconifyIcon icon="lucide:qr-code" /></template>
              创建扫码支付测试单
            </Button>
          </Tooltip>
        </Space>
      </header>

      <div class="configuration-strip">
        <div class="configuration-summary">
          <span
            class="status-dot"
            :class="{ ready: configuration.readyForPayment }"
          ></span>
          <div>
            <b>{{
              configuration.readyForPayment
                ? '支付环境已就绪'
                : '支付环境未就绪'
            }}</b>
            <span>这里只显示是否配置，不会向浏览器返回商户密钥或证书内容</span>
          </div>
        </div>
        <div class="configuration-items">
          <span
            v-for="item in configurationItems"
            :key="item.key"
            :class="{ ready: item.ready }"
          >
            <IconifyIcon
              :icon="item.ready ? 'lucide:circle-check' : 'lucide:circle-x'"
            />
            {{ item.label }}
          </span>
        </div>
        <Button
          size="small"
          :loading="configurationLoading"
          @click="loadConfiguration"
        >
          <template #icon><IconifyIcon icon="lucide:refresh-cw" /></template>
          重新检查
        </Button>
      </div>

      <Alert
        v-if="!configuration.readyForPayment"
        class="configuration-alert"
        message="当前只能查看本地订单记录。配置完成前，创建订单、同步微信状态和退款会被后端拒绝。"
        show-icon
        type="warning"
      />

      <div class="query-bar">
        <Input
          v-model:value="query.keyword"
          allow-clear
          placeholder="商户订单号或微信支付订单号"
          @press-enter="handleQuery"
        />
        <DatePicker.RangePicker
          v-model:value="query.createTimeRange"
          :allow-clear="true"
          format="YYYY-MM-DD"
        />
        <Button type="primary" @click="handleQuery">
          <template #icon><IconifyIcon icon="lucide:search" /></template>
          查询
        </Button>
        <Button @click="resetQuery">
          <template #icon><IconifyIcon icon="lucide:rotate-ccw" /></template>
          重置
        </Button>
      </div>

      <Table
        row-key="id"
        size="small"
        :columns="columns"
        :data-source="records"
        :loading="loading"
        :pagination="{
          ...ADMIN_PAGINATION_PROPS,
          current: query.page,
          pageSize: query.pageSize,
          total,
          showTotal: (count: number) => `共 ${count} 条`,
        }"
        :scroll="{ x: 1200, y: 'calc(100vh - 360px)' }"
        @change="handleTableChange"
      >
        <template #emptyText>
          <Empty description="暂无微信支付订单" />
        </template>
        <template #bodyCell="{ column, index, record: rawRecord }">
          <template v-if="column.key === 'index'">
            {{ (query.page - 1) * query.pageSize + index + 1 }}
          </template>
          <template v-else-if="column.key === 'total'">
            <b>{{ money(rawRecord.total) }}</b>
          </template>
          <template v-else-if="column.key === 'tradeState'">
            <Tag :color="paymentState(rawRecord.tradeState).color">
              {{ paymentState(rawRecord.tradeState).text }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'business'">
            <div class="business-cell">
              <span>{{ rawRecord.tags || '未关联业务' }}</span>
              <small v-if="rawRecord.businessId"
                >ID {{ rawRecord.businessId }}</small
              >
            </div>
          </template>
          <template v-else-if="column.key === 'actions'">
            <Space :size="4">
              <Tooltip title="本地订单详情">
                <Button
                  size="small"
                  type="text"
                  @click="openDetail(asPayRecord(rawRecord))"
                >
                  <IconifyIcon icon="lucide:circle-info" />
                </Button>
              </Tooltip>
              <Tooltip
                v-if="rawRecord.qrcodeContent && !rawRecord.tradeState"
                title="付款二维码"
              >
                <Button
                  size="small"
                  type="text"
                  @click="openQrcode(rawRecord.qrcodeContent)"
                >
                  <IconifyIcon icon="lucide:qr-code" />
                </Button>
              </Tooltip>
              <Tooltip
                v-if="can('sysWechatPay:payInfoFromWechat')"
                title="从微信同步状态"
              >
                <Button
                  size="small"
                  type="text"
                  :disabled="!configuration.readyForPayment"
                  @click="syncOrder(asPayRecord(rawRecord))"
                >
                  <IconifyIcon icon="lucide:refresh-cw" />
                </Button>
              </Tooltip>
              <Button
                v-if="
                  can('sysWechatPay:listRefund') &&
                  ['REFUND', 'SUCCESS'].includes(rawRecord.tradeState || '')
                "
                size="small"
                type="link"
                @click="openRefunds(asPayRecord(rawRecord))"
              >
                退款记录
              </Button>
              <Button
                v-if="
                  can('sysWechatPay:refundDomestic') &&
                  rawRecord.tradeState === 'SUCCESS'
                "
                danger
                size="small"
                type="link"
                :disabled="!configuration.readyForRefund"
                @click="openRefund(asPayRecord(rawRecord))"
              >
                全额退款
              </Button>
            </Space>
          </template>
        </template>
      </Table>
    </section>

    <Modal
      v-model:open="createModalOpen"
      :confirm-loading="creating"
      :ok-button-props="{
        disabled: !createConfirmed || !configuration.readyForPayment,
      }"
      ok-text="创建真实测试单"
      title="创建扫码支付测试单"
      width="620px"
      @ok="submitCreate"
    >
      <Alert
        class="modal-alert"
        message="该操作会调用真实微信支付 Native 下单接口并生成待支付二维码，但只有扫码确认后才会发生扣款。"
        show-icon
        type="warning"
      />
      <Form
        ref="createFormRef"
        layout="vertical"
        :model="createForm"
        :rules="createRules"
      >
        <div class="form-grid">
          <Form.Item label="商品描述" name="description">
            <Input
              v-model:value="createForm.description"
              :maxlength="127"
              placeholder="例如：支付功能联调测试"
            />
          </Form.Item>
          <Form.Item label="订单金额（元）" name="amountYuan">
            <InputNumber
              v-model:value="createForm.amountYuan"
              class="full-width"
              :min="0.01"
              :precision="2"
              :step="0.01"
              placeholder="最低 0.01 元"
            />
          </Form.Item>
          <Form.Item label="业务类型">
            <Input
              v-model:value="createForm.tags"
              :maxlength="64"
              placeholder="可选，例如 OrderTest"
            />
          </Form.Item>
          <Form.Item label="业务 ID">
            <InputNumber
              v-model:value="createForm.businessId"
              class="full-width"
              :min="0"
              placeholder="可选"
            />
          </Form.Item>
        </div>
        <Form.Item label="附加信息">
          <Input.TextArea
            v-model:value="createForm.attachment"
            :auto-size="{ minRows: 2, maxRows: 3 }"
            :maxlength="127"
            placeholder="仅填写业务需要的非敏感信息"
          />
        </Form.Item>
        <Checkbox v-model:checked="createConfirmed">
          我已确认这是微信支付真实下单接口，并了解扫码支付后会产生实际资金交易
        </Checkbox>
      </Form>
    </Modal>

    <Modal
      v-model:open="refundModalOpen"
      :footer="null"
      title="确认全额退款"
      width="520px"
    >
      <Alert
        class="modal-alert"
        :message="`将向微信支付申请退回 ${money(currentRecord?.total)}，提交后不能在本系统撤销。`"
        show-icon
        type="error"
      />
      <Descriptions bordered size="small" :column="1">
        <Descriptions.Item label="商户订单号">
          {{ currentRecord?.outTradeNumber }}
        </Descriptions.Item>
        <Descriptions.Item label="商品描述">
          {{ valueText(currentRecord?.description) }}
        </Descriptions.Item>
        <Descriptions.Item label="退款金额">
          <b class="danger-text">{{ money(currentRecord?.total) }}</b>
        </Descriptions.Item>
      </Descriptions>
      <Form class="refund-form" layout="vertical">
        <Form.Item label="退款原因" required>
          <Input.TextArea
            v-model:value="refundForm.reason"
            :maxlength="80"
            show-count
          />
        </Form.Item>
        <Form.Item label="输入“退款”确认资金操作" required>
          <Input v-model:value="refundForm.confirmation" autocomplete="off" />
        </Form.Item>
      </Form>
      <div class="modal-footer">
        <Button @click="refundModalOpen = false">取消</Button>
        <Button
          danger
          type="primary"
          :loading="refunding"
          @click="submitRefund"
        >
          提交全额退款
        </Button>
      </div>
    </Modal>

    <Modal
      v-model:open="refundListOpen"
      :footer="null"
      title="退款记录"
      width="980px"
    >
      <Table
        row-key="id"
        size="small"
        :columns="refundColumns"
        :data-source="refunds"
        :pagination="false"
        :scroll="{ x: 1100, y: 480 }"
      >
        <template #bodyCell="{ column, record: rawRecord }">
          <template v-if="column.key === 'refund'">
            {{ money(rawRecord.refund) }}
          </template>
          <template v-else-if="column.key === 'tradeState'">
            <Tag :color="refundState(rawRecord.tradeState).color">
              {{ refundState(rawRecord.tradeState).text }}
            </Tag>
          </template>
        </template>
      </Table>
    </Modal>

    <Modal
      v-model:open="detailOpen"
      :footer="null"
      title="支付订单详情"
      width="760px"
    >
      <Descriptions v-if="currentDetail" bordered size="small" :column="2">
        <Descriptions.Item label="商户订单号" :span="2">
          {{ currentDetail.outTradeNumber }}
        </Descriptions.Item>
        <Descriptions.Item label="微信支付订单号" :span="2">
          {{ valueText(currentDetail.transactionId) }}
        </Descriptions.Item>
        <Descriptions.Item label="订单金额">
          {{ money(currentDetail.total) }}
        </Descriptions.Item>
        <Descriptions.Item label="用户实付">
          {{ money(currentDetail.payerTotal) }}
        </Descriptions.Item>
        <Descriptions.Item label="支付状态">
          {{ paymentState(currentDetail.tradeState).text }}
        </Descriptions.Item>
        <Descriptions.Item label="交易类型">
          {{ valueText(currentDetail.tradeType) }}
        </Descriptions.Item>
        <Descriptions.Item label="商品描述" :span="2">
          {{ valueText(currentDetail.description) }}
        </Descriptions.Item>
        <Descriptions.Item label="附加信息" :span="2">
          {{ valueText(currentDetail.attachment) }}
        </Descriptions.Item>
        <Descriptions.Item label="业务类型">
          {{ valueText(currentDetail.tags) }}
        </Descriptions.Item>
        <Descriptions.Item label="业务 ID">
          {{ valueText(currentDetail.businessId) }}
        </Descriptions.Item>
        <Descriptions.Item label="创建时间">
          {{ valueText(currentDetail.createTime) }}
        </Descriptions.Item>
        <Descriptions.Item label="完成时间">
          {{ valueText(currentDetail.successTime) }}
        </Descriptions.Item>
        <Descriptions.Item label="状态说明" :span="2">
          {{ valueText(currentDetail.tradeStateDescription) }}
        </Descriptions.Item>
      </Descriptions>
    </Modal>

    <Modal
      v-model:open="guideOpen"
      :footer="null"
      title="微信支付使用说明"
      width="760px"
    >
      <div class="guide-content">
        <section>
          <h3>这个页面能做什么</h3>
          <p>
            它是微信支付订单管理与联调页面，不是商户密钥配置页。可查询本地订单、生成
            Native
            扫码支付测试单、从微信同步订单状态、查看退款记录并发起全额退款。
          </p>
        </section>
        <section>
          <h3>首次使用前</h3>
          <ol>
            <li>
              在后端 `Wechat.json` 配置 AppId、商户号、APIv3 密钥和证书序列号。
            </li>
            <li>
              把商户私钥证书放到后端配置指向的目录，私钥内容不会进入浏览器。
            </li>
            <li>
              把支付和退款回调配置为外网可访问的 HTTPS
              地址，微信服务器必须能主动访问。
            </li>
            <li>点击页面“重新检查”，所有必需项通过后再创建测试单。</li>
          </ol>
        </section>
        <section>
          <h3>按钮含义</h3>
          <p>
            <b>创建扫码支付测试单：</b>调用真实微信 Native
            下单接口，二维码被付款后会产生实际交易。
          </p>
          <p>
            <b>从微信同步状态：</b
            >只查询微信订单并更新本地状态，不扣款、不退款。
          </p>
          <p>
            <b>全额退款：</b
            >把订单总金额提交给微信退款接口，属于真实资金操作，必须填写原因并再次输入“退款”。
          </p>
        </section>
        <Alert
          message="支付密钥、商户私钥和证书不能通过聊天、截图或前端页面传递；修改配置后需要重启后端。"
          show-icon
          type="info"
        />
      </div>
    </Modal>

    <Modal
      v-model:open="qrcodeModalOpen"
      :footer="null"
      title="微信扫码支付"
      width="380px"
    >
      <div class="qrcode-panel">
        <img v-if="qrcodeText" :src="qrcodeDataUrl" alt="微信支付二维码" />
        <b>请使用微信扫码</b>
        <span>付款前请核对订单金额；二维码通常在创建后 10 分钟内有效</span>
      </div>
    </Modal>
  </div>
</template>

<style scoped>
.pay-page {
  min-height: 100%;
  padding: 12px;
  background: hsl(var(--background-deep));
}

.page-panel {
  min-height: calc(100vh - 120px);
  padding: 14px 16px;
  background: hsl(var(--background));
  border: 1px solid hsl(var(--border));
  border-radius: 8px;
}

.panel-heading {
  display: flex;
  gap: 14px;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 12px;
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

.configuration-strip {
  display: grid;
  grid-template-columns: minmax(260px, 0.8fr) minmax(440px, 1.6fr) auto;
  gap: 14px;
  align-items: center;
  padding: 10px 12px;
  background: hsl(var(--muted) / 20%);
  border: 1px solid hsl(var(--border));
  border-radius: 7px;
}

.configuration-summary {
  display: flex;
  gap: 9px;
  align-items: center;
}

.configuration-summary div {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.configuration-summary b {
  font-size: 13px;
}

.configuration-summary span {
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 11px;
  color: hsl(var(--muted-foreground));
  white-space: nowrap;
}

.status-dot {
  flex: 0 0 auto;
  width: 9px;
  height: 9px;
  background: hsl(var(--destructive));
  border-radius: 50%;
  box-shadow: 0 0 0 4px hsl(var(--destructive) / 10%);
}

.status-dot.ready {
  background: hsl(var(--success));
  box-shadow: 0 0 0 4px hsl(var(--success) / 10%);
}

.configuration-items {
  display: flex;
  flex-wrap: wrap;
  gap: 5px 12px;
}

.configuration-items span {
  display: inline-flex;
  gap: 4px;
  align-items: center;
  font-size: 11px;
  color: hsl(var(--destructive));
}

.configuration-items span.ready {
  color: hsl(var(--success));
}

.configuration-alert {
  margin-top: 10px;
}

.query-bar {
  display: grid;
  grid-template-columns: minmax(260px, 360px) 270px auto auto;
  gap: 8px;
  max-width: 920px;
  margin: 12px 0 10px;
}

.business-cell {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.business-cell span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.business-cell small {
  color: hsl(var(--muted-foreground));
}

.modal-alert {
  margin-bottom: 14px;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 14px;
}

.full-width {
  width: 100%;
}

.refund-form {
  margin-top: 14px;
}

.danger-text {
  color: hsl(var(--destructive));
}

.modal-footer {
  display: flex;
  gap: 8px;
  justify-content: flex-end;
  padding-top: 12px;
  border-top: 1px solid hsl(var(--border));
}

.guide-content {
  font-size: 13px;
  line-height: 1.7;
  color: hsl(var(--foreground));
}

.guide-content section {
  margin-bottom: 16px;
}

.guide-content h3 {
  margin: 0 0 5px;
  font-size: 14px;
}

.guide-content p {
  margin: 4px 0;
  color: hsl(var(--muted-foreground));
}

.guide-content ol {
  padding-left: 20px;
  margin: 4px 0;
  color: hsl(var(--muted-foreground));
}

.qrcode-panel {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 8px 0 10px;
  text-align: center;
}

.qrcode-panel img {
  width: 280px;
  height: 280px;
  image-rendering: pixelated;
}

.qrcode-panel b {
  margin-top: 6px;
  font-size: 15px;
}

.qrcode-panel span {
  margin-top: 4px;
  font-size: 11px;
  color: hsl(var(--muted-foreground));
}

:deep(.ant-table-cell-fix-right) {
  background: hsl(var(--background));
}

@media (max-width: 1100px) {
  .configuration-strip {
    grid-template-columns: 1fr auto;
  }

  .configuration-items {
    grid-row: 2;
    grid-column: 1 / -1;
  }
}

@media (max-width: 760px) {
  .pay-page {
    padding: 8px;
  }

  .page-panel {
    padding: 12px;
  }

  .panel-heading {
    flex-direction: column;
  }

  .configuration-strip {
    grid-template-columns: 1fr;
  }

  .configuration-items {
    grid-row: auto;
    grid-column: auto;
  }

  .query-bar,
  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>

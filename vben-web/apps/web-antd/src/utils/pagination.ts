import type { TablePaginationConfig } from 'ant-design-vue';

export const ADMIN_PAGINATION_PROPS = {
  pageSizeOptions: ['10', '20', '50', '100'],
  responsive: true,
  showQuickJumper: true,
  showLessItems: true,
  showSizeChanger: true,
} satisfies Pick<
  TablePaginationConfig,
  | 'pageSizeOptions'
  | 'responsive'
  | 'showLessItems'
  | 'showQuickJumper'
  | 'showSizeChanger'
>;

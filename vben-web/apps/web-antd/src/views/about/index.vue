<script setup lang="ts">
import { IconifyIcon } from '@vben/icons';

import { Button, Descriptions, Tag } from 'ant-design-vue';

import appPackage from '../../../package.json';

defineOptions({ name: 'AdminNetAbout' });

const technicalItems = [
  ['后端框架', 'Admin.NET / .NET 8'],
  ['前端框架', `Vben Admin ${appPackage.version}`],
  ['界面组件', 'Ant Design Vue'],
  ['构建工具', 'Vite + TypeScript'],
  ['数据存储', 'MySQL'],
  ['权限模式', '服务端动态菜单与按钮权限'],
];

const migrationPrinciples = [
  '旧版 Element Plus Web 永久保留为只读对照，不在迁移过程中修改。',
  '业务页面连接 Admin.NET 真实接口，不用静态假数据伪装功能完成。',
  '只迁移数据库已有菜单，不因为源码目录里存在演示页就新增菜单。',
  '新增、删除、同步、授权等操作同时检查权限、租户边界和数据副作用。',
];
</script>

<template>
  <div class="about-page">
    <header class="about-header">
      <div class="brand-mark">
        <IconifyIcon icon="lucide:panels-top-left" />
      </div>
      <div>
        <h1>Admin.NET</h1>
        <p>基于 Admin.NET 后端与 Vben 5 前端重建的通用后台管理系统</p>
      </div>
      <Tag color="blue">迁移进行中</Tag>
    </header>

    <section class="about-section">
      <h2>项目说明</h2>
      <p>
        系统保留 Admin.NET
        的多租户、组织权限、动态菜单、日志、任务调度、文件、打印和开发工具等后端能力，
        前端逐步替换为 Vben 5 与 Ant Design
        Vue。迁移目标不是只换颜色，而是在功能一致的前提下改善信息密度、交互一致性和数据安全。
      </p>
    </section>

    <section class="about-section">
      <h2>当前技术组成</h2>
      <Descriptions :column="2" bordered size="small">
        <Descriptions.Item
          v-for="item in technicalItems"
          :key="item[0]"
          :label="item[0]"
        >
          {{ item[1] }}
        </Descriptions.Item>
      </Descriptions>
    </section>

    <section class="about-section">
      <h2>迁移约束</h2>
      <ul class="principle-list">
        <li v-for="principle in migrationPrinciples" :key="principle">
          <IconifyIcon icon="lucide:circle-check" />
          <span>{{ principle }}</span>
        </li>
      </ul>
    </section>

    <section class="about-section link-section">
      <div>
        <h2>源码与许可</h2>
        <p>
          Admin.NET 源码包含 MIT 与 Apache 2.0 许可文件；Vben Admin 使用 MIT
          许可。
        </p>
      </div>
      <div class="link-actions">
        <Button href="https://gitee.com/zuohuaijun/Admin.NET" target="_blank">
          <template #icon><IconifyIcon icon="lucide:git-branch" /></template>
          Admin.NET Gitee
        </Button>
        <Button href="https://github.com/zuohuaijun/Admin.NET" target="_blank">
          <template #icon><IconifyIcon icon="lucide:github" /></template>
          Admin.NET GitHub
        </Button>
        <Button href="https://doc.vben.pro" target="_blank">
          <template #icon><IconifyIcon icon="lucide:book-open" /></template>
          Vben 文档
        </Button>
      </div>
    </section>
  </div>
</template>

<style scoped>
.about-page {
  min-height: 100%;
  padding: 14px;
  color: hsl(var(--foreground));
  background: hsl(var(--muted) / 35%);
}

.about-header {
  display: grid;
  grid-template-columns: 48px minmax(0, 1fr) auto;
  gap: 14px;
  align-items: center;
  padding: 20px 18px;
  background: hsl(var(--background));
  border-bottom: 1px solid hsl(var(--border));
}

.brand-mark {
  display: grid;
  place-items: center;
  width: 48px;
  height: 48px;
  font-size: 25px;
  color: white;
  background: #1677ff;
  border-radius: 8px;
}

.about-header h1 {
  margin: 0;
  font-size: 24px;
  font-weight: 700;
}

.about-header p,
.about-section p {
  margin: 5px 0 0;
  line-height: 1.7;
  color: hsl(var(--muted-foreground));
}

.about-section {
  padding: 18px;
  background: hsl(var(--background));
  border-bottom: 1px solid hsl(var(--border));
}

.about-section:last-child {
  border-bottom: 0;
}

.about-section h2 {
  margin: 0 0 12px;
  font-size: 15px;
  font-weight: 650;
}

.principle-list {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px 20px;
  padding: 0;
  margin: 0;
  list-style: none;
}

.principle-list li {
  display: flex;
  gap: 8px;
  align-items: flex-start;
  line-height: 1.6;
}

.principle-list svg {
  flex: 0 0 auto;
  margin-top: 4px;
  color: #16a34a;
}

.link-section {
  display: flex;
  gap: 18px;
  align-items: center;
  justify-content: space-between;
}

.link-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  justify-content: flex-end;
}

@media (max-width: 760px) {
  .about-page {
    padding: 8px;
  }

  .about-header {
    grid-template-columns: 42px minmax(0, 1fr);
    padding: 16px 14px;
  }

  .about-header > .ant-tag {
    grid-column: 2;
    width: fit-content;
    margin: 0;
  }

  .brand-mark {
    width: 42px;
    height: 42px;
  }

  .principle-list {
    grid-template-columns: 1fr;
  }

  .link-section {
    flex-direction: column;
    align-items: flex-start;
  }

  .link-actions {
    justify-content: flex-start;
  }

  :deep(.ant-descriptions-row) {
    display: grid;
    grid-template-columns: 112px minmax(0, 1fr);
  }
}
</style>

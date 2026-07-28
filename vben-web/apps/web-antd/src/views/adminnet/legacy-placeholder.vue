<script lang="ts" setup>
import { computed } from 'vue';
import { useRoute } from 'vue-router';

import { Button, Space, Tag } from 'ant-design-vue';

const route = useRoute();

const title = computed(() => route.meta?.title || route.name || 'Admin.NET');
const component = computed(() => String(route.name || route.path));
</script>

<template>
  <main class="min-h-full bg-slate-50 px-4 py-4 dark:bg-zinc-950">
    <section class="mx-auto max-w-6xl">
      <div
        class="border-border bg-background rounded-md border px-5 py-4 shadow-sm"
      >
        <div class="flex flex-wrap items-start justify-between gap-4">
          <div>
            <div class="mb-2">
              <Tag color="processing">Admin.NET migration</Tag>
              <Tag color="default">Ant Design Vue</Tag>
            </div>
            <h1 class="text-2xl font-semibold tracking-normal">
              {{ title }}
            </h1>
            <p class="text-muted-foreground mt-2 max-w-3xl text-sm">
              This menu is already provided by the Admin.NET backend and loaded
              through Vben backend access mode. The original Element Plus page
              still needs to be rebuilt with the Ant Design Vue component layer.
            </p>
          </div>
          <Space>
            <Button href="/#/dashboard/home">Dashboard</Button>
            <Button
              href="https://gitee.com/zuohuaijun/Admin.NET"
              target="_blank"
            >
              Admin.NET
            </Button>
          </Space>
        </div>
      </div>

      <div class="mt-4 grid gap-4 md:grid-cols-3">
        <div class="border-border bg-background rounded-md border p-4">
          <div class="text-muted-foreground text-xs uppercase">Route</div>
          <div class="mt-2 break-all font-mono text-sm">{{ route.path }}</div>
        </div>
        <div class="border-border bg-background rounded-md border p-4">
          <div class="text-muted-foreground text-xs uppercase">Component</div>
          <div class="mt-2 break-all font-mono text-sm">{{ component }}</div>
        </div>
        <div class="border-border bg-background rounded-md border p-4">
          <div class="text-muted-foreground text-xs uppercase">Status</div>
          <div class="mt-2 text-sm">Routed, protected, ready to port</div>
        </div>
      </div>
    </section>
  </main>
</template>

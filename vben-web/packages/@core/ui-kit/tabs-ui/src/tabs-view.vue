<script setup lang="ts">
import type { TabsEmits, TabsProps } from './types';

import { useForwardPropsEmits } from '@vben-core/composables';
import { ChevronsLeft, ChevronsRight } from '@vben-core/icons';
import { VbenScrollbar } from '@vben-core/shadcn-ui';

import { Tabs, TabsChrome } from './components';
import { useTabsDrag } from './use-tabs-drag';
import { useTabsViewScroll } from './use-tabs-view-scroll';

interface Props extends TabsProps {}

defineOptions({
  name: 'TabsView',
});

const props = withDefaults(defineProps<Props>(), {
  contentClass: 'vben-tabs-content',
  draggable: true,
  styleType: 'chrome',
  wheelable: true,
});

const emit = defineEmits<TabsEmits>();

const forward = useForwardPropsEmits(props, emit);

const {
  handleScrollAt,
  handleWheel,
  // @ts-expect-error unused
  scrollbarRef,
  scrollDirection,
  scrollIsAtLeft,
  scrollIsAtRight,
  showScrollButton,
} = useTabsViewScroll(props);

function onWheel(e: WheelEvent) {
  if (props.wheelable) {
    handleWheel(e);
    e.stopPropagation();
    e.preventDefault();
  }
}

useTabsDrag(props, emit);
</script>

<template>
  <div class="flex h-full flex-1 overflow-hidden">
    <!-- 左侧滚动按钮 -->
    <button
      v-show="showScrollButton"
      aria-label="向左滚动页签"
      :class="{
        'cursor-pointer text-muted-foreground hover:bg-muted': !scrollIsAtLeft,
        'pointer-events-none opacity-30': scrollIsAtLeft,
      }"
      class="flex w-9 flex-none items-center justify-center border-r"
      type="button"
      @click="scrollDirection('left')"
    >
      <ChevronsLeft class="size-4 h-full" />
    </button>

    <div
      :class="{
        'pt-0.75': styleType === 'chrome',
      }"
      class="size-full flex-1 overflow-hidden"
    >
      <VbenScrollbar
        ref="scrollbarRef"
        :shadow-bottom="false"
        :shadow-top="false"
        class="h-full"
        horizontal
        scroll-bar-class="z-10 hidden "
        shadow
        shadow-left
        shadow-right
        @scroll-at="handleScrollAt"
        @wheel="onWheel"
      >
        <div class="h-full w-full">
          <TabsChrome
            v-if="styleType === 'chrome'"
            v-bind="{ ...forward, ...$attrs, ...$props }"
          />

          <Tabs v-else v-bind="{ ...forward, ...$attrs, ...$props }" />
        </div>
      </VbenScrollbar>
    </div>

    <!-- 右侧滚动按钮 -->
    <button
      v-show="showScrollButton"
      aria-label="向右滚动页签"
      :class="{
        'cursor-pointer text-muted-foreground hover:bg-muted': !scrollIsAtRight,
        'pointer-events-none opacity-30': scrollIsAtRight,
      }"
      class="flex w-9 flex-none items-center justify-center border-l text-muted-foreground"
      type="button"
      @click="scrollDirection('right')"
    >
      <ChevronsRight class="size-4 h-full" />
    </button>
  </div>
</template>

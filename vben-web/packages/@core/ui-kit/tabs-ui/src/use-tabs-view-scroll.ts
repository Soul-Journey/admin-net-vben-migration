import type { TabsProps } from './types';

import { nextTick, onMounted, onUnmounted, ref, watch } from 'vue';

import { VbenScrollbar } from '@vben-core/shadcn-ui';

import { useDebounceFn } from '@vueuse/core';

type DomElement = Element | null | undefined;

export function useTabsViewScroll(props: TabsProps) {
  let resizeObserver: null | ResizeObserver = null;
  let mutationObserver: MutationObserver | null = null;
  let scrollListenerEl: Element | null = null;
  let tabItemCount = 0;
  const scrollbarRef = ref<InstanceType<typeof VbenScrollbar> | null>(null);
  const scrollViewportEl = ref<DomElement>(null);
  const showScrollButton = ref(false);
  const scrollIsAtLeft = ref(true);
  const scrollIsAtRight = ref(false);

  function updateScrollState() {
    const viewportEl = scrollViewportEl.value;
    if (!viewportEl) return;

    const { clientWidth, scrollLeft, scrollWidth } = viewportEl;
    scrollIsAtLeft.value = scrollLeft <= 1;
    scrollIsAtRight.value = scrollLeft + clientWidth >= scrollWidth - 1;
    showScrollButton.value = scrollWidth > clientWidth + 1;
  }

  function getScrollClientWidth() {
    const scrollbarEl = scrollbarRef.value?.$el;
    if (!scrollbarEl || !scrollViewportEl.value) return {};

    const scrollbarWidth = scrollbarEl.clientWidth;
    const scrollViewWidth = scrollViewportEl.value.clientWidth;

    return {
      scrollbarWidth,
      scrollViewWidth,
    };
  }

  function scrollDirection(direction: 'left' | 'right', distance?: number) {
    const { scrollViewWidth } = getScrollClientWidth();

    if (!scrollViewWidth || !scrollViewportEl.value) return;

    const scrollDistance =
      distance ?? Math.max(240, Math.floor(scrollViewWidth * 0.72));

    scrollViewportEl.value?.scrollBy({
      behavior: 'smooth',
      left: direction === 'left' ? -scrollDistance : scrollDistance,
    });
    window.setTimeout(updateScrollState, 350);
  }

  async function initScrollbar() {
    await nextTick();

    const scrollbarEl = scrollbarRef.value?.$el;
    if (!scrollbarEl) {
      return;
    }

    const viewportEl = scrollbarEl?.querySelector(
      'div[data-reka-scroll-area-viewport]',
    );
    if (!viewportEl) {
      return;
    }

    scrollViewportEl.value = viewportEl;
    calcShowScrollbarButton();
    updateScrollState();

    scrollListenerEl?.removeEventListener('scroll', updateScrollState);
    scrollListenerEl = viewportEl;
    viewportEl.addEventListener('scroll', updateScrollState, {
      passive: true,
    });

    await nextTick();
    scrollToActiveIntoView();

    // 监听大小变化
    resizeObserver?.disconnect();
    resizeObserver = new ResizeObserver(
      useDebounceFn((_entries: ResizeObserverEntry[]) => {
        calcShowScrollbarButton();
        updateScrollState();
        scrollToActiveIntoView();
      }, 100),
    );
    resizeObserver.observe(viewportEl);

    tabItemCount = props.tabs?.length || 0;
    mutationObserver?.disconnect();
    // 使用 MutationObserver 仅监听子节点数量变化
    mutationObserver = new MutationObserver(() => {
      const count = viewportEl.querySelectorAll(
        `div[data-tab-item="true"]`,
      ).length;

      if (count > tabItemCount) {
        scrollToActiveIntoView();
      }

      if (count !== tabItemCount) {
        calcShowScrollbarButton();
        tabItemCount = count;
      }
    });

    // 配置为仅监听子节点的添加和移除
    mutationObserver.observe(viewportEl, {
      attributes: false,
      childList: true,
      subtree: true,
    });
  }

  async function scrollToActiveIntoView() {
    if (!scrollViewportEl.value) {
      return;
    }
    await nextTick();
    const viewportEl = scrollViewportEl.value;
    const { scrollViewWidth } = getScrollClientWidth();
    const { scrollWidth } = viewportEl;

    if (!scrollViewWidth || scrollViewWidth >= scrollWidth) {
      return;
    }

    requestAnimationFrame(() => {
      const activeItem = viewportEl?.querySelector('.is-active');
      activeItem?.scrollIntoView({ behavior: 'smooth', inline: 'start' });
      window.setTimeout(updateScrollState, 350);
    });
  }

  /**
   * 计算tabs 宽度，用于判断是否显示左右滚动按钮
   */
  async function calcShowScrollbarButton() {
    if (!scrollViewportEl.value) {
      return;
    }

    const { scrollViewWidth = 0 } = getScrollClientWidth();

    showScrollButton.value =
      Boolean(scrollViewWidth) &&
      scrollViewportEl.value.scrollWidth > scrollViewWidth + 1;
  }

  const handleScrollAt = useDebounceFn(({ left, right }) => {
    scrollIsAtLeft.value = left;
    scrollIsAtRight.value = right;
  }, 100);

  function handleWheel({ deltaY }: WheelEvent) {
    scrollViewportEl.value?.scrollBy({
      // behavior: 'smooth',
      left: deltaY * 3,
    });
  }

  watch(
    () => props.active,
    async () => {
      // 200为了等待 tab 切换动画完成
      // setTimeout(() => {
      scrollToActiveIntoView();
      // }, 300);
    },
    {
      flush: 'post',
    },
  );

  // watch(
  //   () => props.tabs?.length,
  //   async () => {
  //     await nextTick();
  //     calcShowScrollbarButton();
  //   },
  //   {
  //     flush: 'post',
  //   },
  // );

  watch(
    () => props.styleType,
    () => {
      initScrollbar();
    },
  );

  onMounted(initScrollbar);

  onUnmounted(() => {
    scrollListenerEl?.removeEventListener('scroll', updateScrollState);
    scrollListenerEl = null;
    resizeObserver?.disconnect();
    mutationObserver?.disconnect();
    resizeObserver = null;
    mutationObserver = null;
  });

  return {
    handleScrollAt,
    handleWheel,
    initScrollbar,
    scrollbarRef,
    scrollDirection,
    scrollIsAtLeft,
    scrollIsAtRight,
    showScrollButton,
  };
}

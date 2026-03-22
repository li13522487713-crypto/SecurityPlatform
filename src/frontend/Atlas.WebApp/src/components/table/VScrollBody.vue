<script setup lang="ts">
import { computed, useSlots, onMounted } from 'vue';
// @ts-ignore
import { RecycleScroller as _RecycleScroller } from 'vue-virtual-scroller';
import 'vue-virtual-scroller/dist/vue-virtual-scroller.css';

const RecycleScroller = _RecycleScroller as any;
import 'vue-virtual-scroller/dist/vue-virtual-scroller.css';

const slots = useSlots();

const props = defineProps<{
  class?: string | Record<string, any> | any[];
  style?: Record<string, any> | string;
  itemSize?: number;
}>();

const vnodesWithKey = computed(() => {
  const children = slots.default?.() || [];
  const flatNodes: any[] = [];
  
  const flatten = (nodes: any[]) => {
    nodes.forEach((vnode, i) => {
      if (Array.isArray(vnode)) {
         flatten(vnode);
      } else if (vnode.type === Symbol.for('v-fgt')) { 
         if (Array.isArray(vnode.children)) flatten(vnode.children);
      } else if (vnode.type !== Comment) {
         // 我们需要确保每个虚拟项有唯一的 id (RecycleScroller 的 key-field)
         flatNodes.push({
           id: vnode.key != null ? vnode.key : `virtual-row-${i}`,
           vnode
         });
      }
    });
  };
  
  flatten(children);
  return flatNodes;
});

</script>

<template>
  <RecycleScroller
    class="virtual-table-tbody"
    :items="vnodesWithKey"
    :item-size="itemSize || 54"
    key-field="id"
    v-bind="$attrs"
  >
    <template #default="{ item }">
      <component :is="item.vnode" />
    </template>
  </RecycleScroller>
</template>

<style>
/* 为了打破 <table> 的强限制并让 Scroller 生效，将包裹元素变为 block */
.virtual-table-tbody {
  display: block;
  width: 100%;
  height: 100%;
  min-height: 200px;
}
.virtual-table-tbody .vue-recycle-scroller__item-wrapper {
  display: block;
  width: 100%;
}
.virtual-table-tbody .vue-recycle-scroller__item-view {
  display: flex;
  width: 100%;
}
/* a-table 的 tr 需要变为 display: flex */
.virtual-table-tbody .ant-table-row {
  display: flex;
  width: 100%;
}
.virtual-table-tbody .ant-table-cell {
  /* Let cell flex auto layout manually in a generic way if column width missing */
  flex: 1;
}
</style>

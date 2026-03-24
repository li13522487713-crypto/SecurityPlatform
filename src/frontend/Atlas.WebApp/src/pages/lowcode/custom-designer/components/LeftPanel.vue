<template>
  <div class="left-panel">
    <a-tabs v-model:active-key="activeTab" :tab-bar-style="{ padding: '0 16px', margin: 0 }" centered>
      <a-tab-pane key="components" tab="组件库">
        <div class="panel-content">
          <div class="search-wrap">
            <a-input-search v-model:value="searchKey" placeholder="搜索组件..." size="small" />
          </div>
          <div class="components-scroll">
            <div v-for="category in categories" :key="category.key" class="component-group">
              <div class="group-title">{{ category.label }}</div>
              <div class="component-grid">
                <div 
                  v-for="comp in getComponentsByCategory(category.key)" 
                  :key="comp.type" 
                  class="component-item"
                  draggable="true"
                  @dragstart="onDragStart($event, comp)"
                >
                  <component :is="getIcon(comp.icon)" class="comp-icon" />
                  <span class="comp-name">{{ comp.name }}</span>
                </div>
              </div>
            </div>
            <div v-if="filteredComponents.length === 0" class="empty-hint">未找到组件</div>
          </div>
        </div>
      </a-tab-pane>
      <a-tab-pane key="tree" tab="页面结构">
        <div class="panel-content tree-scroll">
          <a-tree
            :tree-data="treeData"
            :default-expand-all="true"
            :selected-keys="selectedKeys"
            @select="onTreeSelect"
          >
            <template #title="{ title, key }">
              <span :class="{'tree-node-selected': store.selectedId === key}">{{ title }}</span>
            </template>
          </a-tree>
        </div>
      </a-tab-pane>
    </a-tabs>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { useDesignerStore } from '../core/store';
import { COMPONENT_REGISTRY } from '../core/registry';
import * as Icons from '@ant-design/icons-vue';
import type { ComponentMeta, ComponentSchema } from '../core/types';

const store = useDesignerStore();
const activeTab = ref('components');
const searchKey = ref('');

const categories = [
  { key: 'basic', label: '基础组件' },
  { key: 'container', label: '容器组件' },
  { key: 'data', label: '数据组件' },
  { key: 'feedback', label: '反馈组件' }
];

const allComponents = Object.values(COMPONENT_REGISTRY);

const filteredComponents = computed(() => {
  if (!searchKey.value) return allComponents;
  return allComponents.filter(c => c.name.includes(searchKey.value) || c.type.includes(searchKey.value));
});

const getComponentsByCategory = (cat: string) => {
  return filteredComponents.value.filter(c => c.category === cat);
};

const getIcon = (iconName: string) => {
  return (Icons as any)[iconName] || Icons.BlockOutlined;
};

const onDragStart = (e: DragEvent, comp: ComponentMeta) => {
  if (e.dataTransfer) {
    e.dataTransfer.setData('component-type', comp.type);
    e.dataTransfer.effectAllowed = 'copy';
  }
};

// Tree generation
const generateTree = (node: ComponentSchema): any => {
  return {
    title: node.name || node.type,
    key: node.id,
    children: node.children ? node.children.map(generateTree) : undefined
  };
};

const treeData = computed(() => {
  return [generateTree(store.schema)];
});

const selectedKeys = computed(() => store.selectedId ? [store.selectedId] : []);

const onTreeSelect = (keys: string[]) => {
  if (keys.length > 0) {
    store.selectedId = keys[0];
  } else {
    store.selectedId = null;
  }
};
</script>

<style scoped>
.left-panel {
  width: 260px;
  background-color: #ffffff;
  border-right: 1px solid #f0f0f0;
  display: flex;
  flex-direction: column;
  height: 100%;
}
.panel-content {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 54px - 44px); /* 54 topbar, 44 tabs */
}
.search-wrap {
  padding: 12px 16px;
  border-bottom: 1px solid #f0f0f0;
}
.components-scroll {
  flex: 1;
  overflow-y: auto;
  padding: 12px;
}
.tree-scroll {
  flex: 1;
  overflow-y: auto;
  padding: 16px 8px;
}
.group-title {
  font-size: 13px;
  font-weight: 500;
  color: #666;
  margin-bottom: 12px;
  margin-top: 8px;
  padding-left: 4px;
}
.component-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 8px;
  margin-bottom: 16px;
}
.component-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 72px;
  background-color: #f8f9fa;
  border: 1px solid transparent;
  border-radius: 6px;
  cursor: grab;
  transition: all 0.2s;
}
.component-item:hover {
  border-color: #1890ff;
  color: #1890ff;
  background-color: #e6f7ff;
}
.comp-icon {
  font-size: 20px;
  margin-bottom: 8px;
  color: #8c8c8c;
}
.component-item:hover .comp-icon {
  color: #1890ff;
}
.comp-name {
  font-size: 12px;
  color: #333;
}
.empty-hint {
  text-align: center;
  color: #999;
  padding: 20px 0;
  font-size: 13px;
}
</style>

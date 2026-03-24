<template>
  <div 
    class="custom-renderer"
    :class="{ 
      'is-selected': isSelected,
      'is-hovered': isHovered,
      'preview-mode': store.isPreviewMode,
      'is-container': isContainer
    }"
    :style="mergedStyles"
    @click.stop="selectComponent"
    @mouseover.stop="hoverComponent"
    @mouseleave.stop="leaveComponent"
    @dragover.stop.prevent="onDragOver"
    @dragleave.stop.prevent="onDragLeave"
    @drop.stop.prevent="onDrop"
  >
    <!-- Container -->
    <template v-if="node.type === 'container'">
      <CustomRenderer 
         v-for="child in node.children" 
         :key="child.id" 
         :node="child" 
         :parent="node" 
      />
      <div v-if="!node.children?.length && !store.isPreviewMode" class="empty-container">
        拖拽组件到此容器
      </div>
    </template>

    <!-- Basic Text -->
    <template v-else-if="node.type === 'text'">
      {{ node.props.text }}
    </template>

    <!-- Button -->
    <a-button
v-else-if="node.type === 'button'"
              :type="node.props.type"
              :danger="node.props.danger"
              :disabled="node.props.disabled">
      {{ node.props.text }}
    </a-button>

    <!-- Input -->
    <a-input
v-else-if="node.type === 'input'"
             :placeholder="node.props.placeholder"
             :disabled="node.props.disabled"
             :allow-clear="node.props.allowClear" />
             
    <a-select
v-else-if="node.type === 'select'"
             :placeholder="node.props.placeholder"
             :disabled="node.props.disabled"
             :options="node.props.options"
             style="width: 100%;" />

    <!-- Card -->
    <a-card
v-else-if="node.type === 'card'"
            :title="node.props.title"
            :bordered="node.props.bordered">
       <CustomRenderer 
         v-for="child in node.children" 
         :key="child.id" 
         :node="child" 
         :parent="node" 
       />
       <div v-if="!node.children?.length && !store.isPreviewMode" class="empty-container">
          拖拽组件到此卡片
       </div>
    </a-card>

    <!-- Table -->
    <a-table
v-else-if="node.type === 'table'"
             :columns="node.props.columns"
             :data-source="node.props.dataSource"
             :pagination="node.props.pagination"
             :size="node.props.size" />

    <!-- Fallback -->
    <div v-else class="unknown-component">
      【{{ node.name }}】
    </div>

    <!-- Edit Actions -->
    <div v-if="isSelected && !store.isPreviewMode" class="renderer-toolbar">
      <span class="toolbar-title">{{ node.name }}</span>
      <div class="toolbar-actions">
        <a-button type="primary" size="small" style="padding: 0 4px; height: 20px;" @click.stop="duplicateComponent">
          <CopyOutlined style="font-size: 10px;" />
        </a-button>
        <a-button type="primary" danger size="small" style="padding: 0 4px; height: 20px;" @click.stop="deleteComponent">
          <DeleteOutlined style="font-size: 10px;" />
        </a-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/* eslint-disable vue/no-mutating-props */
import { computed, ref } from 'vue';
import { useDesignerStore } from '../core/store';
import { COMPONENT_REGISTRY } from '../core/registry';
import type { ComponentSchema } from '../core/types';
import { DeleteOutlined, CopyOutlined } from '@ant-design/icons-vue';

const store = useDesignerStore();
const props = defineProps<{
  node: ComponentSchema;
  parent?: ComponentSchema | null;
}>();

const isSelected = computed(() => store.selectedId === props.node.id);
const isHovered = computed(() => store.hoverId === props.node.id && store.selectedId !== props.node.id);
const isContainer = computed(() => ['container', 'card', 'page'].includes(props.node.type));
const isDragOver = ref(false);

const mergedStyles = computed(() => {
  const base = { ...props.node.styles };
  if (isDragOver.value && !store.isPreviewMode) {
    base.border = '2px solid #1890ff';
    base.backgroundColor = 'rgba(24, 144, 255, 0.05)';
  }
  return base;
});

const selectComponent = () => {
  if (store.isPreviewMode) return;
  store.selectedId = props.node.id;
};

const hoverComponent = () => {
  if (store.isPreviewMode) return;
  store.hoverId = props.node.id;
};

const leaveComponent = () => {
  if (store.isPreviewMode) return;
  store.hoverId = null;
};

// --- Drag & Drop ---
const onDragOver = (e: DragEvent) => {
  if (!isContainer.value) return;
  e.dataTransfer!.dropEffect = 'copy';
  isDragOver.value = true;
};

const onDragLeave = () => {
  isDragOver.value = false;
};

const generateId = (type: string) => {
  return `${type}_${Math.random().toString(36).substring(2, 8)}`;
};

const deepClone = (obj: any): any => JSON.parse(JSON.stringify(obj));

const onDrop = (e: DragEvent) => {
  if (!isContainer.value) return;
  isDragOver.value = false;
  
  const compType = e.dataTransfer?.getData('component-type');
  if (compType) {
    const meta = COMPONENT_REGISTRY[compType];
    if (meta) {
      store.commit(); // save history
      
      const newComponent: ComponentSchema = deepClone(meta.defaultSchema);
      newComponent.id = generateId(compType);
      
      if (!props.node.children) {
        props.node.children = [];
      }
      props.node.children.push(newComponent);
      store.selectedId = newComponent.id;
    }
  }
};

const deleteComponent = () => {
  if (!props.parent || !props.parent.children) return;
  store.commit();
  const index = props.parent.children.findIndex(c => c.id === props.node.id);
  if (index !== -1) {
    props.parent.children.splice(index, 1);
    store.selectedId = null;
  }
};

const duplicateComponent = () => {
  if (!props.parent || !props.parent.children) return;
  store.commit();
  const index = props.parent.children.findIndex(c => c.id === props.node.id);
  if (index !== -1) {
    const cloned = deepClone(props.node);
    cloned.id = generateId(cloned.type);
    // basic recursive ID update for children if any
    const updateIds = (node: ComponentSchema) => {
      node.id = generateId(node.type);
      if (node.children) node.children.forEach(updateIds);
    };
    if (cloned.children) cloned.children.forEach(updateIds);
    
    props.parent.children.splice(index + 1, 0, cloned);
    store.selectedId = cloned.id;
  }
};
</script>

<style scoped>
.custom-renderer {
  position: relative;
  box-sizing: border-box;
  transition: all 0.2s;
}

.custom-renderer:not(.preview-mode) {
  cursor: pointer;
  border: 1px dashed transparent;
}

.custom-renderer.is-hovered:not(.preview-mode) {
  border: 1px dashed #1890ff;
}

.custom-renderer.is-selected:not(.preview-mode) {
  border: 2px solid #1890ff;
}

.empty-container {
  min-height: 60px;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: rgba(0,0,0,0.02);
  border: 1px dashed #d9d9d9;
  color: #999;
  font-size: 12px;
  border-radius: 4px;
}

.renderer-toolbar {
  position: absolute;
  top: -24px;
  right: -2px;
  background-color: #1890ff;
  color: #fff;
  font-size: 12px;
  display: flex;
  align-items: center;
  padding: 0 8px;
  height: 24px;
  border-radius: 4px 4px 0 0;
  z-index: 100;
  white-space: nowrap;
}

.toolbar-title {
  margin-right: 8px;
}
.toolbar-actions {
  display: flex;
  gap: 4px;
}

.unknown-component {
  padding: 8px;
  background: #f5222d11;
  border: 1px dashed #f5222d;
  color: #f5222d;
}
</style>

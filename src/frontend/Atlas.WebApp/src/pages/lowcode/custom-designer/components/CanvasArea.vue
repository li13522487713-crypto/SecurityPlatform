<template>
  <div class="canvas-wrapper">
    <!-- 顶端的设备切换模拟区域 -->
    <div 
      class="canvas-area" 
      :class="[store.deviceType, { 'preview-mode': store.isPreviewMode }]"
      @click.self="clearSelection"
    >
      <div 
        class="canvas-page"
        :class="{ 'is-selected': store.selectedId === store.schema.id }"
        :style="store.schema.styles"
        @dragover.prevent="onDragOver"
        @drop="onDrop"
        @click.self="selectRoot"
      >
        <CustomRenderer 
          v-for="child in store.schema.children" 
          :key="child.id" 
          :node="child" 
          :parent="store.schema"
        />
        
        <div v-if="!store.schema.children?.length && !store.isPreviewMode" class="empty-page-hint" @click.self="selectRoot">
          <div class="hint-content">
            <LayoutOutlined class="hint-icon" />
            <p>从左侧拖拽组件到此区域开始搭建页面</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useDesignerStore } from '../core/store';
import CustomRenderer from '../renderers/CustomRenderer.vue';
import { COMPONENT_REGISTRY } from '../core/registry';
import { LayoutOutlined } from '@ant-design/icons-vue';
import type { ComponentSchema } from '../core/types';

const store = useDesignerStore();

const clearSelection = () => {
  if (store.isPreviewMode) return;
  store.selectedId = null;
};

const selectRoot = () => {
  if (store.isPreviewMode) return;
  store.selectedId = store.schema.id;
};

const generateId = (type: string) => `${type}_${Math.random().toString(36).substring(2, 8)}`;
const deepClone = (obj: any): any => JSON.parse(JSON.stringify(obj));

const onDragOver = (e: DragEvent) => {
  e.dataTransfer!.dropEffect = 'copy';
};

const onDrop = (e: DragEvent) => {
  const compType = e.dataTransfer?.getData('component-type');
  if (compType) {
    const meta = COMPONENT_REGISTRY[compType];
    if (meta) {
      store.commit();
      const newComponent: ComponentSchema = deepClone(meta.defaultSchema);
      newComponent.id = generateId(compType);
      
      if (!store.schema.children) {
        store.schema.children = [];
      }
      store.schema.children.push(newComponent);
      store.selectedId = newComponent.id;
    }
  }
};
</script>

<style scoped>
.canvas-wrapper {
  width: 100%;
  height: 100%;
  display: flex;
  justify-content: center;
  align-items: flex-start;
  padding-bottom: 40px;
}

.canvas-area {
  transition: width 0.3s;
  height: 100%;
  box-shadow: 0 2px 12px 0 rgba(0,0,0,0.05);
  background-color: transparent;
  display: flex;
  flex-direction: column;
}

/* Device simulation */
.canvas-area.desktop {
  width: 100%;
  max-width: 1200px;
}
.canvas-area.tablet {
  width: 768px;
}
.canvas-area.mobile {
  width: 375px;
}

.canvas-page {
  flex: 1;
  min-height: 100%;
  position: relative;
  background-color: #ffffff;
  overflow-y: auto;
  border-radius: 2px;
}

.canvas-page:not(.preview-mode) {
  background-image: linear-gradient(#e5e5e5 1px, transparent 0), linear-gradient(90deg, #e5e5e5 1px, transparent 0);
  background-size: 20px 20px;
}

.canvas-page.is-selected {
  outline: 2px solid #1890ff;
  outline-offset: -2px;
}

.empty-page-hint {
  position: absolute;
  top: 0; left: 0; right: 0; bottom: 0;
  display: flex;
  justify-content: center;
  align-items: center;
  pointer-events: none;
}
.hint-content {
  text-align: center;
  color: #bfbfbf;
}
.hint-icon {
  font-size: 48px;
  margin-bottom: 16px;
}
.hint-content p {
  font-size: 16px;
  letter-spacing: 1px;
}
</style>

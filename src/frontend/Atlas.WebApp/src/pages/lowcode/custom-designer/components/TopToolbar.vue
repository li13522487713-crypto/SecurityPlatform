<template>
  <div class="top-toolbar">
    <div class="toolbar-left">
      <div class="logo">
        <AppstoreOutlined />
        <span>LowCode Pro</span>
      </div>
      <div class="divider"></div>
      <a-input
        v-model:value="store.schema.name"
        class="page-name-input"
        :bordered="false"
        placeholder="页面名称"
      />
      <a-tag color="success" class="status-tag">已保存</a-tag>
    </div>
    
    <div class="toolbar-center">
      <a-radio-group v-model:value="store.deviceType" option-type="button" button-style="solid" size="small">
        <a-radio-button value="desktop"><DesktopOutlined /></a-radio-button>
        <a-radio-button value="tablet"><TabletOutlined /></a-radio-button>
        <a-radio-button value="mobile"><MobileOutlined /></a-radio-button>
      </a-radio-group>
      
      <div class="divider"></div>
      
      <a-space size="small">
        <a-tooltip title="撤销 (Ctrl+Z)">
          <a-button type="text" :disabled="!store.canUndo" @click="store.undo()">
            <UndoOutlined />
          </a-button>
        </a-tooltip>
        <a-tooltip title="重做 (Ctrl+Y)">
          <a-button type="text" :disabled="!store.canRedo" @click="store.redo()">
            <RedoOutlined />
          </a-button>
        </a-tooltip>
      </a-space>
    </div>
    
    <div class="toolbar-right">
      <a-button type="text" @click="$emit('open-schema')">
        <CodeOutlined /> Schema
      </a-button>
      <a-button type="text" @click="store.isPreviewMode = !store.isPreviewMode">
        <template v-if="store.isPreviewMode"><EditOutlined /> 编辑模式</template>
        <template v-else><PlaySquareOutlined /> 预览模式</template>
      </a-button>
      <a-button type="primary" @click="$emit('publish')">
        <SendOutlined /> 发布
      </a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useDesignerStore } from '../core/store';
import { 
  AppstoreOutlined, 
  DesktopOutlined, 
  TabletOutlined, 
  MobileOutlined,
  UndoOutlined,
  RedoOutlined,
  CodeOutlined,
  PlaySquareOutlined,
  EditOutlined,
  SendOutlined
} from '@ant-design/icons-vue';

const store = useDesignerStore();
defineEmits(['open-schema', 'publish']);
</script>

<style scoped>
.top-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  height: 54px;
  background-color: #ffffff;
  border-bottom: 1px solid #f0f0f0;
  padding: 0 16px;
  box-shadow: 0 1px 4px rgba(0,21,41,0.08);
  z-index: 10;
}
.toolbar-left, .toolbar-center, .toolbar-right {
  display: flex;
  align-items: center;
  height: 100%;
}
.logo {
  font-size: 16px;
  font-weight: 600;
  color: #1890ff;
  display: flex;
  align-items: center;
  gap: 8px;
}
.divider {
  width: 1px;
  height: 20px;
  background-color: #f0f0f0;
  margin: 0 12px;
}
.page-name-input {
  width: 200px;
  font-weight: 500;
  font-size: 14px;
}
.page-name-input:hover, .page-name-input:focus {
  background-color: #f5f5f5;
  border-radius: 4px;
}
.status-tag {
  margin-left: 8px;
  transform: scale(0.9);
}
</style>

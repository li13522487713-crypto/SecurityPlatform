<template>
  <div class="designer-layout">
    <TopToolbar @publish="handlePublish" @open-schema="schemaModalVisible = true" />
    <div class="designer-body">
      <LeftPanel />
      <div class="designer-canvas-container">
        <CanvasArea />
      </div>
      <RightPanel />
    </div>

    <!-- Schema Viewer Modal -->
    <a-modal v-model:open="schemaModalVisible" title="Schema 视图" width="800px" @ok="saveSchemaJson">
      <div style="margin-bottom: 8px">您可以直接编辑 JSON 并应用，这会同步更新画布内容。</div>
      <a-textarea v-model:value="schemaJson" :rows="20" style="font-family: monospace; font-size: 13px;" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import { message } from 'ant-design-vue';
import { useDesignerStore } from './core/store';
import TopToolbar from './components/TopToolbar.vue';
import LeftPanel from './components/LeftPanel.vue';
import RightPanel from './components/RightPanel.vue';
import CanvasArea from './components/CanvasArea.vue';

const store = useDesignerStore();
const schemaModalVisible = ref(false);
const schemaJson = ref('');

// Sync JSON to text when opening modal
watch(schemaModalVisible, (val) => {
  if (val) {
    schemaJson.value = JSON.stringify(store.schema, null, 2);
  }
});

const saveSchemaJson = () => {
  try {
    const parsed = JSON.parse(schemaJson.value);
    store.setSchema(parsed);
    schemaModalVisible.value = false;
    message.success('Schema 更新成功');
  } catch (err) {
    message.error('JSON 格式有误，请检查');
  }
};

const handlePublish = () => {
  message.success('已触发发布操作（MVP演示）');
};
</script>

<style scoped>
.designer-layout {
  display: flex;
  flex-direction: column;
  height: 100vh;
  width: 100vw;
  background-color: #f5f5f5;
  overflow: hidden;
}
.designer-body {
  display: flex;
  flex: 1;
  overflow: hidden;
}
.designer-canvas-container {
  flex: 1;
  overflow: auto;
  position: relative;
  display: flex;
  justify-content: center;
  padding: 24px;
}
</style>

<template>
  <a-card class="page-card" :bordered="false">
    <template #title>流程可视化设计器（骨架）</template>
    <template #extra>
      <a-space>
        <a-button @click="handleSave">保存草稿</a-button>
        <a-button @click="handleValidate">校验</a-button>
        <a-button type="primary" @click="handlePublish">发布</a-button>
      </a-space>
    </template>

    <div class="designer-shell">
      <div class="panel panel-left">
        <div class="panel-title">节点库</div>
        <a-list size="small" bordered :data-source="nodeLibrary">
          <template #renderItem="{ item }">
            <a-list-item class="node-item">
              <a-badge :color="item.color" /> <span class="node-label">{{ item.label }}</span>
            </a-list-item>
          </template>
        </a-list>
      </div>

      <div class="panel panel-center">
        <div class="panel-title">画布（占位）</div>
        <div class="canvas-placeholder">拖拽节点到此处，稍后接入 @antv/x6</div>
      </div>

      <div class="panel panel-right">
        <div class="panel-title">属性配置</div>
        <a-form layout="vertical">
          <a-form-item label="流程名称">
            <a-input v-model:value="processName" placeholder="请输入流程名称" />
          </a-form-item>
          <a-form-item label="版本">
            <a-input-number v-model:value="version" :min="1" :max="999" style="width: 100%" />
          </a-form-item>
          <a-form-item label="备注">
            <a-textarea v-model:value="note" :rows="3" />
          </a-form-item>
        </a-form>
      </div>
    </div>
  </a-card>
</template>

<script setup lang="ts">
import { reactive, ref } from "vue";
import { message } from "ant-design-vue";
import {
  validateVisualizationProcess,
  publishVisualizationProcess
} from "@/services/api";

const processName = ref("示例流程");
const version = ref(1);
const note = ref("");
const canvasDefinition = reactive({ nodes: [], edges: [] });

const nodeLibrary = [
  { label: "开始", color: "#1890ff" },
  { label: "审批", color: "#52c41a" },
  { label: "条件", color: "#fa8c16" },
  { label: "抄送", color: "#722ed1" },
  { label: "结束", color: "#595959" }
];

const handleValidate = async () => {
  const definitionJson = JSON.stringify(canvasDefinition);
  const result = await validateVisualizationProcess({ definitionJson });
  if (result.passed) {
    message.success("校验通过");
  } else {
    message.error(`校验失败：${result.errors.join("；")}`);
  }
};

const handlePublish = async () => {
  const definitionJson = JSON.stringify(canvasDefinition);
  const validateResult = await validateVisualizationProcess({ definitionJson });
  if (!validateResult.passed) {
    message.error("请先修复校验错误再发布");
    return;
  }

  const result = await publishVisualizationProcess({
    processId: processName.value,
    version: version.value,
    note: note.value
  });
  message.success(`已发布：${result.processId} v${result.version}`);
};

const handleSave = () => {
  message.success("已保存草稿（示例占位）");
};
</script>

<style scoped>
.designer-shell {
  display: grid;
  grid-template-columns: 240px 1fr 300px;
  gap: 12px;
  min-height: 520px;
}

.panel {
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  padding: 12px;
  display: flex;
  flex-direction: column;
}

.panel-title {
  font-weight: 600;
  margin-bottom: 8px;
}

.canvas-placeholder {
  flex: 1;
  border: 1px dashed #d9d9d9;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #8c8c8c;
  background: #fafafa;
}

.node-item {
  display: flex;
  gap: 8px;
  align-items: center;
}

.node-label {
  font-size: 13px;
}
</style>

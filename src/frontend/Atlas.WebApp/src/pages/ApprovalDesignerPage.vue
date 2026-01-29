<template>
  <a-card class="page-card">
    <template #title>
      <a-input
        v-model:value="flowName"
        placeholder="流程名称"
        style="width: 300px"
        :maxlength="100"
      />
    </template>
    <template #extra>
      <a-space>
        <a-button @click="togglePreview">
          {{ showPreview ? '隐藏预览' : '显示预览' }}
        </a-button>
        <a-button @click="undo" :disabled="!canUndo">撤销</a-button>
        <a-button @click="redo" :disabled="!canRedo">重做</a-button>
        <a-button @click="handleSave">保存</a-button>
        <a-button type="primary" @click="handlePublish">发布</a-button>
      </a-space>
    </template>
    
    <div class="designer-container">
      <div class="editor-panel" :class="{ 'has-preview': showPreview }">
        <ApprovalTreeEditor
            :flow-tree="flowTree"
            :selected-node="selectedNode"
            @addNode="addNode"
            @deleteNode="deleteNode"
            @update:selectedNode="selectNode"
            @addConditionBranch="addConditionBranch"
            @deleteConditionBranch="deleteConditionBranch"
        />
      </div>
      
      <div v-if="showPreview" class="preview-panel">
        <X6PreviewCanvas :flow-tree="flowTree" />
      </div>
      
      <NodePropertiesDrawer
        v-model:open="drawerVisible"
        :node="selectedNode"
        @update="handleNodeUpdate"
      />
    </div>
  </a-card>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { message } from 'ant-design-vue';
import ApprovalTreeEditor from '@/components/approval/ApprovalTreeEditor.vue';
import NodePropertiesDrawer from '@/components/approval/NodePropertiesDrawer.vue';
import X6PreviewCanvas from '@/components/approval/X6PreviewCanvas.vue';
import { useApprovalTree } from '@/composables/useApprovalTree';
import { ApprovalTreeConverter } from '@/utils/approval-tree-converter';
import {
  getApprovalFlowById,
  createApprovalFlow,
  updateApprovalFlow,
  publishApprovalFlow
} from '@/services/api';

const route = useRoute();
const router = useRouter();

const { 
    flowTree, 
    selectedNode, 
    addNode, 
    deleteNode, 
    updateNode, 
    addConditionBranch, 
    deleteConditionBranch, 
    selectNode, 
    validateFlow,
    undo,
    redo,
    canUndo,
    canRedo,
    pushState
} = useApprovalTree();

const flowName = ref('');
const flowId = ref<string | null>(null);
const drawerVisible = ref(false);
const showPreview = ref(false);

watch(selectedNode, (node) => {
  drawerVisible.value = !!node;
});

const handleNodeUpdate = (updatedNode: any) => {
    updateNode(updatedNode);
};

const togglePreview = () => {
    showPreview.value = !showPreview.value;
};

const loadFlow = async () => {
  const id = route.params.id as string;
  if (!id || id === 'undefined') {
      pushState(flowTree.value);
      return;
  }
  
  try {
    const flow = await getApprovalFlowById(id);
    flowName.value = flow.name;
    flowId.value = flow.id;
    
    if (flow.definitionJson) {
      flowTree.value = ApprovalTreeConverter.definitionJsonToTree(flow.definitionJson);
    }
    
    pushState(flowTree.value);
  } catch (err) {
    message.error(err instanceof Error ? err.message : '加载失败');
  }
};

const handleSave = async () => {
  if (!flowName.value.trim()) {
    message.warning('请输入流程名称');
    return;
  }
  
  const validation = validateFlow();
  if (!validation.valid) {
    message.warning(`流程配置不完整:\n${validation.errors.join('\n')}`);
    return;
  }
  
  const definitionJson = ApprovalTreeConverter.treeToDefinitionJson(flowTree.value);
  
  try {
    if (flowId.value) {
      await updateApprovalFlow(flowId.value, { name: flowName.value, definitionJson });
      message.success('保存成功');
    } else {
      const result = await createApprovalFlow({ name: flowName.value, definitionJson });
      flowId.value = result.id;
      router.replace(`/approval/designer/${result.id}`);
      message.success('创建成功');
    }
  } catch (err) {
    message.error(err instanceof Error ? err.message : '保存失败');
  }
};

const handlePublish = async () => {
  if (!flowId.value) {
    message.warning('请先保存流程');
    return;
  }
  
  try {
    await publishApprovalFlow(flowId.value);
    message.success('发布成功');
    router.push('/approval/flows');
  } catch (err) {
    message.error(err instanceof Error ? err.message : '发布失败');
  }
};

onMounted(() => {
  loadFlow();
});
</script>

<style scoped>
.designer-container {
  height: calc(100vh - 200px);
  border: 1px solid #d9d9d9;
  position: relative;
  overflow: hidden;
  display: flex;
}

.editor-panel {
    flex: 1;
    height: 100%;
    overflow: hidden;
    transition: all 0.3s;
}

.editor-panel.has-preview {
    width: 50%;
    flex: none;
    border-right: 1px solid #d9d9d9;
}

.preview-panel {
    flex: 1;
    height: 100%;
    background: #fafafa;
}
</style>

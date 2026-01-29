<template>
  <div class="approval-tree-editor">
    <div class="zoom-box">
      <TreeNodeRenderer v-if="flowTree && flowTree.rootNode" :node="flowTree.rootNode" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { provide } from 'vue';
import type { ApprovalFlowTree, TreeNode, ConditionBranch } from '@/types/approval-tree';
import TreeNodeRenderer from './TreeNodeRenderer.vue';
import { 
    AddNodeKey, 
    DeleteNodeKey, 
    SelectNodeKey, 
    AddConditionBranchKey, 
    DeleteConditionBranchKey 
} from '@/types/injection-keys';

// Props & Emits
const props = defineProps<{
  flowTree: ApprovalFlowTree;
  selectedNode: TreeNode | ConditionBranch | null;
}>();

const emit = defineEmits<{
  'update:flowTree': [tree: ApprovalFlowTree];
  'update:selectedNode': [node: TreeNode | ConditionBranch | null];
  'addNode': [parentId: string, nodeType: string];
  'deleteNode': [nodeId: string];
  'addConditionBranch': [nodeId: string];
  'deleteConditionBranch': [branchId: string];
}>();

const addNode = (parentId: string, nodeType: string) => {
    emit('addNode', parentId, nodeType);
};

const deleteNode = (nodeId: string) => {
    emit('deleteNode', nodeId);
};

const selectNode = (node: TreeNode | ConditionBranch | null) => {
    emit('update:selectedNode', node);
};

const addConditionBranch = (nodeId: string) => {
    emit('addConditionBranch', nodeId);
};

const deleteConditionBranch = (branchId: string) => {
    emit('deleteConditionBranch', branchId);
};

provide(AddNodeKey, addNode);
provide(DeleteNodeKey, deleteNode);
provide(SelectNodeKey, selectNode);
provide(AddConditionBranchKey, addConditionBranch);
provide(DeleteConditionBranchKey, deleteConditionBranch);

</script>

<style scoped>
.approval-tree-editor {
  width: 100%;
  height: 100%;
  overflow: auto;
  background: #f5f5f7;
  padding: 50px;
  display: flex;
  justify-content: center;
}

.zoom-box {
    transform-origin: 50% 0;
}
</style>

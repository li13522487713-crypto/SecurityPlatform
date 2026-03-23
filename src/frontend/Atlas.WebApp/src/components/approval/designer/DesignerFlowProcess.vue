<template>
  <div class="dd-body dd-body--designer">
    <ApprovalNodePalette 
      :visible="paletteVisible" 
      @update:visible="$emit('update:paletteVisible', $event)" 
      @add-node="$emit('addPaletteNode', $event)" 
    />
    
    <div class="dd-canvas">
      <X6ApprovalDesigner
        ref="designerRef"
        :flow-tree="flowTree"
        :selected-node-id="selectedNode?.id ?? null"
        @select-node="handleSelectNode"
        @add-node="handleAddNode"
        @delete-node="handleDeleteNode"
        @add-condition-branch="handleAddConditionBranch"
        @delete-condition-branch="handleDeleteConditionBranch"
        @move-branch="handleMoveBranch"
        @update-route-target="handleUpdateRouteTarget"
        @undo="$emit('undo')"
        @redo="$emit('redo')"
      />
    </div>

    <ApprovalPropertiesPanel
      :open="panelOpen"
      :node="selectedNode"
      :form-fields="effectiveFormFields"
      @update:open="$emit('update:panelOpen', $event)"
      @update="handleUpdateNode"
    />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import X6ApprovalDesigner from '@/components/approval/x6/X6ApprovalDesigner.vue';
import ApprovalPropertiesPanel from '@/components/approval/ApprovalPropertiesPanel.vue';
import ApprovalNodePalette from '@/components/approval/ApprovalNodePalette.vue';
import type { LfFormField } from '@/types/approval-definition';
import type { ApprovalFlowTree, ConditionBranch, TreeNode } from '@/types/approval-tree';

defineProps<{
  paletteVisible: boolean;
  flowTree: ApprovalFlowTree;
  selectedNode: TreeNode | ConditionBranch | null;
  panelOpen: boolean;
  effectiveFormFields: LfFormField[];
}>();

const emit = defineEmits<{
  'update:paletteVisible': [value: boolean];
  'update:panelOpen': [value: boolean];
  'addPaletteNode': [nodeType: string];
  'selectNode': [node: TreeNode | ConditionBranch | null];
  'addNode': [parentId: string, nodeType: string];
  'deleteNode': [nodeId: string];
  'addConditionBranch': [nodeId: string];
  'deleteConditionBranch': [branchId: string];
  'moveBranch': [conditionNodeId: string, branchId: string, direction: 'left' | 'right'];
  'updateRouteTarget': [routeNodeId: string, targetNodeId: string];
  'updateNode': [node: TreeNode | ConditionBranch];
  'undo': [];
  'redo': [];
}>();

const designerRef = ref<InstanceType<typeof X6ApprovalDesigner> | null>(null);
const handleSelectNode = (node: TreeNode | ConditionBranch | null) => emit('selectNode', node);
const handleAddNode = (parentId: string, nodeType: string) => emit('addNode', parentId, nodeType);
const handleDeleteNode = (nodeId: string) => emit('deleteNode', nodeId);
const handleAddConditionBranch = (nodeId: string) => emit('addConditionBranch', nodeId);
const handleDeleteConditionBranch = (branchId: string) => emit('deleteConditionBranch', branchId);
const handleMoveBranch = (conditionNodeId: string, branchId: string, direction: 'left' | 'right') =>
  emit('moveBranch', conditionNodeId, branchId, direction);
const handleUpdateRouteTarget = (routeNodeId: string, targetNodeId: string) =>
  emit('updateRouteTarget', routeNodeId, targetNodeId);
const handleUpdateNode = (node: TreeNode | ConditionBranch) => emit('updateNode', node);

const zoomIn = () => designerRef.value?.zoomIn();
const zoomOut = () => designerRef.value?.zoomOut();
const zoomFit = () => designerRef.value?.zoomFit();

defineExpose({
  zoomIn,
  zoomOut,
  zoomFit
});
</script>

<style scoped>
.dd-body--designer {
  position: relative;
  display: flex;
  flex: 1;
  height: 100%;
  overflow: hidden;
}
.dd-canvas {
  flex: 1;
  height: 100%;
  position: relative;
}
</style>

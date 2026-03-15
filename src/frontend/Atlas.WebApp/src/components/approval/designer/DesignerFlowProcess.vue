<template>
  <div class="dd-body dd-body--designer">
    <ApprovalNodePalette 
      :visible="paletteVisible" 
      @update:visible="$emit('update:paletteVisible', $event)" 
      @addNode="$emit('addPaletteNode', $event)" 
    />
    
    <div class="dd-canvas">
      <X6ApprovalDesigner
        ref="designerRef"
        :flow-tree="flowTree"
        :selected-node-id="selectedNode?.id ?? null"
        @selectNode="(...args) => $emit('selectNode', ...args)"
        @addNode="(...args) => $emit('addNode', ...args)"
        @deleteNode="(...args) => $emit('deleteNode', ...args)"
        @addConditionBranch="(...args) => $emit('addConditionBranch', ...args)"
        @deleteConditionBranch="(...args) => $emit('deleteConditionBranch', ...args)"
        @moveBranch="(...args) => $emit('moveBranch', ...args)"
        @updateRouteTarget="(...args) => $emit('updateRouteTarget', ...args)"
      />
    </div>

    <ApprovalPropertiesPanel
      :open="panelOpen"
      :node="selectedNode"
      :form-fields="effectiveFormFields"
      @update:open="$emit('update:panelOpen', $event)"
      @update="$emit('updateNode', $event)"
    />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import X6ApprovalDesigner from '@/components/approval/x6/X6ApprovalDesigner.vue';
import ApprovalPropertiesPanel from '@/components/approval/ApprovalPropertiesPanel.vue';
import ApprovalNodePalette from '@/components/approval/ApprovalNodePalette.vue';

// Using unknown to avoid massive type imports in this wrapper, let the parent handle strict types
defineProps<{
  paletteVisible: boolean;
  flowTree: any;
  selectedNode: any;
  panelOpen: boolean;
  effectiveFormFields: any[];
}>();

defineEmits<{
  'update:paletteVisible': [value: boolean];
  'update:panelOpen': [value: boolean];
  'addPaletteNode': [nodeType: string];
  'selectNode': [...args: any[]];
  'addNode': [...args: any[]];
  'deleteNode': [...args: any[]];
  'addConditionBranch': [...args: any[]];
  'deleteConditionBranch': [...args: any[]];
  'moveBranch': [...args: any[]];
  'updateRouteTarget': [...args: any[]];
  'updateNode': [node: any];
}>();

const designerRef = ref<InstanceType<typeof X6ApprovalDesigner> | null>(null);

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

<template>
  <div class="tree-node-container">
    <!-- 1. 渲染当前节点 -->
    <div class="node-content-wrapper">
      <component
        :is="nodeComponent"
        :node="node"
        @click="handleNodeClick"
        @delete="handleNodeDelete"
        @add-branch="handleAddBranch"
      />
    </div>

    <!-- 2. 加号按钮（非条件节点，且非结束节点） -->
    <div v-if="canAddChild(node) && !isConditionLike" class="add-node-wrapper">
      <AddNodeButton @select="handleAddNode" />
    </div>

    <!-- 3. 条件节点的分支处理 -->
    <div v-if="isConditionLike" class="condition-branches-wrapper">
       <div class="condition-branches">
        <div
            v-for="(branch, index) in (node as ConditionNode).conditionNodes"
            :key="branch.id"
            class="condition-branch"
        >
            <!-- 分支连线辅助线 -->
            <div class="branch-lines">
               <div v-if="index === 0 || index === (node as ConditionNode).conditionNodes.length - 1" class="line-vertical-top"></div>
               <div v-if="index > 0 && index < (node as ConditionNode).conditionNodes.length - 1" class="line-horizontal-top"></div>
               <!-- 左右边界的横线 -->
               <div v-if="index === 0 && (node as ConditionNode).conditionNodes.length > 1" class="line-horizontal-left"></div>
               <div v-if="index === (node as ConditionNode).conditionNodes.length - 1 && (node as ConditionNode).conditionNodes.length > 1" class="line-horizontal-right"></div>
            </div>

            <ConditionBranchWidget 
                :branch="branch" 
                @click="handleBranchClick"
                @delete="handleBranchDelete"
            />
            
            <div class="add-node-wrapper">
               <AddNodeButton @select="(type) => handleAddBranchNode(branch, type)" />
            </div>

            <TreeNodeRenderer
                v-if="branch.childNode"
                :node="branch.childNode"
            />
        </div>
       </div>
       
       <!-- 条件节点汇聚后的加号按钮 -->
       <div class="add-node-wrapper">
          <AddNodeButton @select="handleAddNode" />
       </div>
    </div>

    <!-- 3.1 并行节点分支处理 -->
    <div v-if="isParallel" class="condition-branches-wrapper">
      <div class="condition-branches">
        <div
          v-for="(branch, index) in parallelNode?.parallelNodes ?? []"
          :key="branch.id"
          class="condition-branch"
        >
          <div class="branch-lines">
            <div v-if="index === 0 || index === (parallelNode?.parallelNodes.length ?? 0) - 1" class="line-vertical-top"></div>
            <div v-if="index > 0 && index < (parallelNode?.parallelNodes.length ?? 0) - 1" class="line-horizontal-top"></div>
            <div v-if="index === 0 && (parallelNode?.parallelNodes.length ?? 0) > 1" class="line-horizontal-left"></div>
            <div v-if="index === (parallelNode?.parallelNodes.length ?? 0) - 1 && (parallelNode?.parallelNodes.length ?? 0) > 1" class="line-horizontal-right"></div>
          </div>
          <TreeNodeRenderer :node="branch" />
        </div>
      </div>
      <div class="add-node-wrapper">
        <AddNodeButton @select="handleAddNode" />
      </div>
    </div>

    <!-- 4. 递归渲染子节点 (非条件节点) -->
    <TreeNodeRenderer
      v-if="node.nodeType !== 'end' && 'childNode' in node && node.childNode"
      :node="node.childNode"
    />
  </div>
</template>

<script setup lang="ts">
import { inject, computed } from 'vue';
import type { Component } from 'vue';
import type { TreeNode, ConditionNode, ConditionBranch, ParallelNode } from '@/types/approval-tree';
import StartNodeWidget from './nodes/StartNodeWidget.vue';
import ApproveNodeWidget from './nodes/ApproveNodeWidget.vue';
import CopyNodeWidget from './nodes/CopyNodeWidget.vue';
import ConditionNodeWidget from './nodes/ConditionNodeWidget.vue';
import EndNodeWidget from './nodes/EndNodeWidget.vue';
import ConditionBranchWidget from './nodes/ConditionBranchWidget.vue';
import AddNodeButton from './AddNodeButton.vue';
import { 
    AddNodeKey, 
    DeleteNodeKey, 
    SelectNodeKey, 
    AddConditionBranchKey, 
    DeleteConditionBranchKey 
} from '@/types/injection-keys';

defineOptions({
  name: 'TreeNodeRenderer'
});

const props = defineProps<{
  node: TreeNode;
}>();

// 注入操作方法
const addNode = inject(AddNodeKey)!;
const deleteNode = inject(DeleteNodeKey)!;
const selectNode = inject(SelectNodeKey)!;
const addConditionBranch = inject(AddConditionBranchKey)!;
const deleteConditionBranch = inject(DeleteConditionBranchKey)!;

const nodeComponent = computed<Component | undefined>(() => {
  const map: Record<string, Component> = {
    start: StartNodeWidget,
    approve: ApproveNodeWidget,
    copy: CopyNodeWidget,
    condition: ConditionNodeWidget,
    dynamicCondition: ConditionNodeWidget,
    parallelCondition: ConditionNodeWidget,
    parallel: ApproveNodeWidget,
    end: EndNodeWidget,
  };
  return map[props.node.nodeType];
});

const isConditionLike = computed(() =>
  props.node.nodeType === 'condition' ||
  props.node.nodeType === 'dynamicCondition' ||
  props.node.nodeType === 'parallelCondition'
);

const isParallel = computed(() => props.node.nodeType === 'parallel');
const parallelNode = computed(() => (props.node.nodeType === 'parallel' ? (props.node as ParallelNode) : undefined));

const canAddChild = (node: TreeNode) => {
  return node.nodeType !== 'end';
};

// 事件处理
const handleNodeClick = (n: TreeNode) => {
  selectNode(n);
};

const handleNodeDelete = (nodeId: string) => {
  deleteNode(nodeId);
};

const handleAddNode = (nodeType: string) => {
  addNode(props.node.id, nodeType);
};

const handleAddBranch = (nodeId: string) => {
    addConditionBranch(nodeId);
};

const handleBranchClick = (branch: ConditionBranch) => {
    selectNode(branch);
};

const handleBranchDelete = (branchId: string) => {
    deleteConditionBranch(branchId);
};

const handleAddBranchNode = (branch: ConditionBranch, nodeType: string) => {
    addNode(branch.id, nodeType);
};

</script>

<style scoped>
.tree-node-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  position: relative;
}

.tree-node-container::before {
  content: '';
  position: absolute;
  top: 0;
  left: 50%;
  width: 2px;
  height: 20px;
  background: #cacaca;
  transform: translateX(-1px);
  z-index: 0;
}

.node-content-wrapper {
    position: relative;
    z-index: 1;
}

.add-node-wrapper {
    position: relative;
    z-index: 1;
}

.condition-branches-wrapper {
    display: flex;
    flex-direction: column;
    align-items: center;
}

.condition-branches {
  display: flex;
  flex-direction: row;
  gap: 40px;
  margin-top: 20px;
  position: relative;
  border-top: 2px solid #cacaca; 
  padding-top: 20px;
}

.condition-branch {
    display: flex;
    flex-direction: column;
    align-items: center;
    position: relative;
}

.condition-branch::before {
  content: '';
  position: absolute;
  top: 0;
  left: 50%;
  width: 2px;
  height: 100%;
  background: #cacaca;
  transform: translateX(-1px);
  z-index: -1;
}

/* 覆盖横向连线：首尾分支的左右多余线条需要遮盖 */
.branch-lines {
    position: absolute;
    top: -22px; /* 调整位置覆盖 border-top */
    left: 0;
    right: 0;
    height: 22px;
    pointer-events: none;
}

.line-vertical-top {
    position: absolute;
    top: 0;
    left: 50%;
    width: 2px;
    height: 100%;
    background: #cacaca;
    transform: translateX(-1px);
}

.line-horizontal-left {
    position: absolute;
    top: 0;
    left: 0;
    width: 50%;
    height: 2px;
    background: #f5f5f7; /* 遮盖背景色 */
}

.line-horizontal-right {
    position: absolute;
    top: 0;
    right: 0;
    width: 50%;
    height: 2px;
    background: #f5f5f7; /* 遮盖背景色 */
}
</style>

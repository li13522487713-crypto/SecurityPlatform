<template>
  <div class="dd-body dd-body--designer">
    <div class="dd-canvas">
      <div class="dd-toolbar-inline">
        <a-space>
          <a-button
            size="small"
            :type="paletteVisible ? 'primary' : 'default'"
            @click="$emit('update:paletteVisible', !paletteVisible)"
          >
            {{ t('approvalDesigner.nodePaletteTitle') }}
          </a-button>
          <a-button size="small" @click="emit('undo')">{{ t('approvalDesigner.toolbarUndo') }}</a-button>
          <a-button size="small" @click="emit('redo')">{{ t('approvalDesigner.toolbarRedo') }}</a-button>
        </a-space>
      </div>

      <a-alert
        type="info"
        show-icon
        class="dd-alert"
        :message="t('approvalDesigner.previewHint')"
      />

      <div v-if="paletteVisible" class="dd-palette">
        <a-space wrap>
          <a-button
            v-for="item in nodePalette"
            :key="item.type"
            size="small"
            @click="emit('addPaletteNode', item.type)"
          >
            {{ item.label }}
          </a-button>
        </a-space>
      </div>

      <a-list class="dd-tree-list" bordered size="small" :data-source="flatItems">
        <template #renderItem="{ item }">
          <a-list-item
            :class="[
              'dd-tree-item',
              selectedId === item.id ? 'dd-tree-item--active' : '',
            ]"
            @click="handleSelect(item)"
          >
            <div class="dd-tree-item__content" :style="{ paddingLeft: `${item.level * 16}px` }">
              <a-tag :color="item.kind === 'branch' ? 'gold' : 'blue'">
                {{ item.kind === 'branch' ? t('approvalDesigner.branchLabel') : item.nodeType }}
              </a-tag>
              <span class="dd-tree-item__title">{{ item.title }}</span>
            </div>
          </a-list-item>
        </template>
      </a-list>
    </div>

    <div v-if="panelOpen && selectedKind !== null" class="dd-panel">
      <div class="dd-panel__header">
        <span>{{ t('approvalDesigner.propertiesTitle') }}</span>
        <a-button type="text" size="small" @click="$emit('update:panelOpen', false)">×</a-button>
      </div>

      <a-form layout="vertical" class="dd-form">
        <template v-if="selectedKind === 'branch' && branchDraft">
          <a-form-item :label="t('approvalDesigner.branchNameLabel')">
            <a-input v-model:value="branchDraft.branchName" />
          </a-form-item>
          <a-form-item>
            <a-space>
              <a-button size="small" @click="moveBranch('left')">{{ t('approvalDesigner.moveLeft') }}</a-button>
              <a-button size="small" @click="moveBranch('right')">{{ t('approvalDesigner.moveRight') }}</a-button>
              <a-button danger size="small" @click="deleteBranch">{{ t('common.delete') }}</a-button>
              <a-button type="primary" size="small" @click="saveBranch">{{ t('common.save') }}</a-button>
            </a-space>
          </a-form-item>
        </template>

        <template v-if="selectedKind === 'node' && nodeDraft">
          <a-form-item :label="t('approvalDesigner.nodeNameLabel')">
            <a-input v-model:value="nodeDraft.nodeName" />
          </a-form-item>

          <a-form-item v-if="nodeDraft.nodeType === 'route'" :label="t('approvalDesigner.routeTargetLabel')">
            <a-input
              v-model:value="routeTargetNodeId"
              :placeholder="t('approvalDesigner.routeTargetPlaceholder')"
            />
            <a-button size="small" style="margin-top: 8px" @click="updateRouteTarget">
              {{ t('approvalDesigner.routeTargetApply') }}
            </a-button>
          </a-form-item>

          <a-form-item v-if="isConditionType(nodeDraft.nodeType)">
            <a-button size="small" @click="addConditionBranch">
              {{ t('approvalDesigner.addConditionBranch') }}
            </a-button>
          </a-form-item>

          <a-form-item>
            <a-space>
              <a-button type="primary" size="small" @click="saveNode">{{ t('common.save') }}</a-button>
              <a-button
                size="small"
                danger
                :disabled="nodeDraft.nodeType === 'start'"
                @click="deleteNode"
              >
                {{ t('common.delete') }}
              </a-button>
            </a-space>
          </a-form-item>
        </template>
      </a-form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import type { LfFormField } from '@/types/approval-definition';
import type { ApprovalFlowTree, ConditionBranch, NodeType, TreeNode } from '@/types/approval-tree';

type FlatTreeItem = {
  id: string;
  title: string;
  level: number;
  kind: 'node' | 'branch';
  nodeType: string;
};

type NodeDraft = {
  id: string;
  nodeType: NodeType;
  nodeName: string;
};

type BranchDraft = {
  id: string;
  branchName: string;
};

const props = defineProps<{
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

const { t } = useI18n();

const selectedKind = ref<'node' | 'branch' | null>(null);
const selectedId = ref<string | null>(null);
const nodeDraft = ref<NodeDraft | null>(null);
const branchDraft = ref<BranchDraft | null>(null);
const routeTargetNodeId = ref('');

const nodePalette: Array<{ type: NodeType; label: string }> = [
  { type: 'approve', label: t('approvalDesigner.nodeApprove') },
  { type: 'copy', label: t('approvalDesigner.nodeCopy') },
  { type: 'condition', label: t('approvalDesigner.nodeCondition') },
  { type: 'parallel', label: t('approvalDesigner.nodeParallel') },
  { type: 'route', label: t('approvalDesigner.nodeRoute') },
  { type: 'callProcess', label: t('approvalDesigner.nodeCallProcess') },
  { type: 'timer', label: t('approvalDesigner.nodeTimer') },
  { type: 'trigger', label: t('approvalDesigner.nodeTrigger') },
  { type: 'end', label: t('approvalDesigner.nodeEnd') },
];

const isConditionType = (nodeType: NodeType) =>
  nodeType === 'condition' ||
  nodeType === 'dynamicCondition' ||
  nodeType === 'parallelCondition' ||
  nodeType === 'inclusive';

const isBranch = (value: TreeNode | ConditionBranch): value is ConditionBranch =>
  !Object.prototype.hasOwnProperty.call(value, 'nodeType');

const findNodeById = (node: TreeNode, nodeId: string): TreeNode | null => {
  if (node.id === nodeId) {
    return node;
  }

  if (
    (node.nodeType === 'condition' ||
      node.nodeType === 'dynamicCondition' ||
      node.nodeType === 'parallelCondition' ||
      node.nodeType === 'inclusive') &&
    node.conditionNodes.length
  ) {
    for (const branch of node.conditionNodes) {
      if (branch.childNode) {
        const found = findNodeById(branch.childNode, nodeId);
        if (found) {
          return found;
        }
      }
    }
  }

  if (node.nodeType === 'parallel' && node.parallelNodes.length) {
    for (const parallelNode of node.parallelNodes) {
      const found = findNodeById(parallelNode, nodeId);
      if (found) {
        return found;
      }
    }
  }

  if (
    (node.nodeType === 'start' ||
      node.nodeType === 'approve' ||
      node.nodeType === 'copy' ||
      node.nodeType === 'condition' ||
      node.nodeType === 'dynamicCondition' ||
      node.nodeType === 'parallelCondition' ||
      node.nodeType === 'parallel' ||
      node.nodeType === 'inclusive' ||
      node.nodeType === 'callProcess' ||
      node.nodeType === 'timer' ||
      node.nodeType === 'trigger') &&
    node.childNode
  ) {
    return findNodeById(node.childNode, nodeId);
  }

  return null;
};

const findBranchById = (node: TreeNode, branchId: string): ConditionBranch | null => {
  if (
    node.nodeType === 'condition' ||
    node.nodeType === 'dynamicCondition' ||
    node.nodeType === 'parallelCondition' ||
    node.nodeType === 'inclusive'
  ) {
    const branch = node.conditionNodes.find((item) => item.id === branchId);
    if (branch) {
      return branch;
    }
    for (const item of node.conditionNodes) {
      if (item.childNode) {
        const found = findBranchById(item.childNode, branchId);
        if (found) {
          return found;
        }
      }
    }
  }

  if (node.nodeType === 'parallel') {
    for (const parallelNode of node.parallelNodes) {
      const found = findBranchById(parallelNode, branchId);
      if (found) {
        return found;
      }
    }
  }

  if (
    (node.nodeType === 'start' ||
      node.nodeType === 'approve' ||
      node.nodeType === 'copy' ||
      node.nodeType === 'condition' ||
      node.nodeType === 'dynamicCondition' ||
      node.nodeType === 'parallelCondition' ||
      node.nodeType === 'parallel' ||
      node.nodeType === 'inclusive' ||
      node.nodeType === 'callProcess' ||
      node.nodeType === 'timer' ||
      node.nodeType === 'trigger') &&
    node.childNode
  ) {
    return findBranchById(node.childNode, branchId);
  }

  return null;
};

const findBranchOwnerId = (node: TreeNode, branchId: string): string | null => {
  if (
    node.nodeType === 'condition' ||
    node.nodeType === 'dynamicCondition' ||
    node.nodeType === 'parallelCondition' ||
    node.nodeType === 'inclusive'
  ) {
    if (node.conditionNodes.some((branch) => branch.id === branchId)) {
      return node.id;
    }
    for (const branch of node.conditionNodes) {
      if (branch.childNode) {
        const ownerId = findBranchOwnerId(branch.childNode, branchId);
        if (ownerId) {
          return ownerId;
        }
      }
    }
  }

  if (node.nodeType === 'parallel') {
    for (const parallelNode of node.parallelNodes) {
      const ownerId = findBranchOwnerId(parallelNode, branchId);
      if (ownerId) {
        return ownerId;
      }
    }
  }

  if (
    (node.nodeType === 'start' ||
      node.nodeType === 'approve' ||
      node.nodeType === 'copy' ||
      node.nodeType === 'condition' ||
      node.nodeType === 'dynamicCondition' ||
      node.nodeType === 'parallelCondition' ||
      node.nodeType === 'parallel' ||
      node.nodeType === 'inclusive' ||
      node.nodeType === 'callProcess' ||
      node.nodeType === 'timer' ||
      node.nodeType === 'trigger') &&
    node.childNode
  ) {
    return findBranchOwnerId(node.childNode, branchId);
  }

  return null;
};

const flattenTree = (node: TreeNode, level: number, items: FlatTreeItem[]) => {
  items.push({
    id: node.id,
    title: node.nodeName || node.nodeType,
    level,
    kind: 'node',
    nodeType: node.nodeType,
  });

  if (
    node.nodeType === 'condition' ||
    node.nodeType === 'dynamicCondition' ||
    node.nodeType === 'parallelCondition' ||
    node.nodeType === 'inclusive'
  ) {
    node.conditionNodes.forEach((branch) => {
      items.push({
        id: branch.id,
        title: branch.branchName,
        level: level + 1,
        kind: 'branch',
        nodeType: 'branch',
      });
      if (branch.childNode) {
        flattenTree(branch.childNode, level + 2, items);
      }
    });
  }

  if (node.nodeType === 'parallel') {
    node.parallelNodes.forEach((parallelNode) => {
      flattenTree(parallelNode, level + 1, items);
    });
  }

  if (
    (node.nodeType === 'start' ||
      node.nodeType === 'approve' ||
      node.nodeType === 'copy' ||
      node.nodeType === 'condition' ||
      node.nodeType === 'dynamicCondition' ||
      node.nodeType === 'parallelCondition' ||
      node.nodeType === 'parallel' ||
      node.nodeType === 'inclusive' ||
      node.nodeType === 'callProcess' ||
      node.nodeType === 'timer' ||
      node.nodeType === 'trigger') &&
    node.childNode
  ) {
    flattenTree(node.childNode, level + 1, items);
  }
};

const flatItems = computed(() => {
  const items: FlatTreeItem[] = [];
  flattenTree(props.flowTree.rootNode, 0, items);
  return items;
});

watch(
  () => props.selectedNode,
  (value) => {
    if (!value) {
      selectedKind.value = null;
      selectedId.value = null;
      nodeDraft.value = null;
      branchDraft.value = null;
      routeTargetNodeId.value = '';
      emit('update:panelOpen', false);
      return;
    }

    selectedId.value = value.id;
    emit('update:panelOpen', true);

    if (isBranch(value)) {
      selectedKind.value = 'branch';
      branchDraft.value = {
        id: value.id,
        branchName: value.branchName,
      };
      nodeDraft.value = null;
      routeTargetNodeId.value = '';
      return;
    }

    selectedKind.value = 'node';
    nodeDraft.value = {
      id: value.id,
      nodeType: value.nodeType,
      nodeName: value.nodeName,
    };
    branchDraft.value = null;
    routeTargetNodeId.value = value.nodeType === 'route' ? value.routeTargetNodeId ?? '' : '';
  },
  { immediate: true },
);

const handleSelect = (item: FlatTreeItem) => {
  if (item.kind === 'node') {
    const node = findNodeById(props.flowTree.rootNode, item.id);
    if (node) {
      emit('selectNode', node);
      return;
    }
    return;
  }

  const branch = findBranchById(props.flowTree.rootNode, item.id);
  if (branch) {
    emit('selectNode', branch);
  }
};

const saveNode = () => {
  if (!nodeDraft.value) {
    return;
  }
  const sourceNode = findNodeById(props.flowTree.rootNode, nodeDraft.value.id);
  if (!sourceNode) {
    return;
  }
  const nextNode: TreeNode = {
    ...sourceNode,
    nodeName: nodeDraft.value.nodeName,
  };
  if (nextNode.nodeType === 'route') {
    nextNode.routeTargetNodeId = routeTargetNodeId.value.trim() || undefined;
  }
  emit('updateNode', nextNode);
};

const saveBranch = () => {
  if (!branchDraft.value) {
    return;
  }
  const sourceBranch = findBranchById(props.flowTree.rootNode, branchDraft.value.id);
  if (!sourceBranch) {
    return;
  }
  const nextBranch: ConditionBranch = {
    ...sourceBranch,
    branchName: branchDraft.value.branchName,
  };
  emit('updateNode', nextBranch);
};

const deleteNode = () => {
  if (!nodeDraft.value) {
    return;
  }
  emit('deleteNode', nodeDraft.value.id);
};

const addConditionBranch = () => {
  if (!nodeDraft.value || !isConditionType(nodeDraft.value.nodeType)) {
    return;
  }
  emit('addConditionBranch', nodeDraft.value.id);
};

const deleteBranch = () => {
  if (!branchDraft.value) {
    return;
  }
  emit('deleteConditionBranch', branchDraft.value.id);
};

const moveBranch = (direction: 'left' | 'right') => {
  if (!branchDraft.value) {
    return;
  }
  const ownerId = findBranchOwnerId(props.flowTree.rootNode, branchDraft.value.id);
  if (!ownerId) {
    return;
  }
  emit('moveBranch', ownerId, branchDraft.value.id, direction);
};

const updateRouteTarget = () => {
  if (!nodeDraft.value || nodeDraft.value.nodeType !== 'route') {
    return;
  }
  emit('updateRouteTarget', nodeDraft.value.id, routeTargetNodeId.value.trim());
};

const zoomIn = () => {};
const zoomOut = () => {};
const zoomFit = () => {};

defineExpose({
  zoomIn,
  zoomOut,
  zoomFit,
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
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 8px;
  padding: 12px;
  overflow: hidden;
}

.dd-toolbar-inline {
  display: flex;
  justify-content: space-between;
}

.dd-alert {
  margin-bottom: 4px;
}

.dd-palette {
  padding: 8px;
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
}

.dd-tree-list {
  flex: 1;
  overflow: auto;
}

.dd-tree-item {
  cursor: pointer;
  transition: background 0.2s;
}

.dd-tree-item:hover {
  background: #fafafa;
}

.dd-tree-item--active {
  background: #e6f4ff;
}

.dd-tree-item__content {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
}

.dd-tree-item__title {
  color: #1f1f1f;
}

.dd-panel {
  width: 360px;
  border-left: 1px solid #f0f0f0;
  background: #fff;
  display: flex;
  flex-direction: column;
}

.dd-panel__header {
  height: 42px;
  border-bottom: 1px solid #f0f0f0;
  padding: 0 12px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-weight: 500;
}

.dd-form {
  padding: 12px;
}
</style>

<template>
  <a-modal
    :open="visible"
    title="选择跳转节点"
    @ok="handleOk"
    @cancel="handleCancel"
    width="600px"
  >
    <div class="node-list">
      <a-radio-group v-model:value="selectedNodeId" style="width: 100%">
        <a-list item-layout="horizontal" :data-source="nodes">
          <template #renderItem="{ item }">
            <a-list-item class="node-item" :class="{ active: selectedNodeId === item.id }" @click="selectedNodeId = item.id">
              <a-radio :value="item.id" class="node-radio">
                <span class="node-name">{{ item.name }}</span>
                <span class="node-type-tag">{{ getTypeName(item.type) }}</span>
              </a-radio>
            </a-list-item>
          </template>
        </a-list>
      </a-radio-group>
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';

const props = defineProps<{
  visible: boolean;
  flowDefinition: unknown;
}>();

const emit = defineEmits<{
  'update:visible': [value: boolean];
  select: [nodeId: string];
}>();

type SelectableNode = { id: string; name: string; type: string };

const selectedNodeId = ref('');
const nodes = ref<SelectableNode[]>([]);

watch(() => props.flowDefinition, (def) => {
  if (def) {
    // 兼容传入 rootNode 或 nodes.rootNode 两种结构
    const rootNode = typeof def === 'object' && def !== null
      ? ((def as { rootNode?: unknown }).rootNode ?? (def as { nodes?: { rootNode?: unknown } }).nodes?.rootNode)
      : undefined;
    nodes.value = flattenNodes(rootNode);
  }
}, { immediate: true });

const allowedNodeTypes = new Set(['approve', 'copy', 'condition', 'dynamicCondition', 'parallelCondition', 'inclusive', 'route', 'callProcess', 'timer', 'trigger']);

const flattenNodes = (node: unknown): SelectableNode[] => {
  const list: SelectableNode[] = [];
  if (typeof node === 'object' && node !== null) {
    const currentNode = node as {
      id?: string;
      nodeId?: string;
      nodeName?: string;
      nodeType?: string;
      childNode?: unknown;
      conditionNodes?: Array<{ childNode?: unknown }>;
      parallelNodes?: unknown[];
    };
    const id = currentNode.id ?? currentNode.nodeId;
    const type = currentNode.nodeType ?? '';
    if (id && allowedNodeTypes.has(type)) {
      list.push({ id, name: currentNode.nodeName ?? id, type });
    }
    if (currentNode.childNode) {
      list.push(...flattenNodes(currentNode.childNode));
    }
    if (currentNode.conditionNodes) {
      currentNode.conditionNodes.forEach((branch) => {
        if (branch.childNode) list.push(...flattenNodes(branch.childNode));
      });
    }
    if (currentNode.parallelNodes) {
      currentNode.parallelNodes.forEach((child) => {
        list.push(...flattenNodes(child));
      });
    }
  }
  // 防止重复节点展示
  return list.filter((item, idx, arr) => arr.findIndex((n) => n.id === item.id) === idx);
};

const getTypeName = (type: string) => {
  const map: Record<string, string> = {
    start: '开始',
    approve: '审批',
    copy: '抄送',
    condition: '条件',
    parallel: '并行',
    end: '结束'
  };
  return map[type] || type;
};

const handleOk = () => {
  if (selectedNodeId.value) {
    emit('select', selectedNodeId.value);
    emit('update:visible', false);
  }
};

const handleCancel = () => {
  emit('update:visible', false);
};
</script>

<style scoped>
.node-list {
  max-height: 400px;
  overflow-y: auto;
}
.node-item {
  cursor: pointer;
  padding: 8px;
  border-radius: 4px;
}
.node-item:hover {
  background: #f5f5f5;
}
.node-item.active {
  background: #e6f7ff;
}
.node-radio {
  display: flex;
  align-items: center;
  width: 100%;
}
.node-name {
  flex: 1;
  margin-left: 8px;
}
.node-type-tag {
  font-size: 12px;
  color: #999;
  background: #f0f0f0;
  padding: 2px 6px;
  border-radius: 4px;
}
</style>

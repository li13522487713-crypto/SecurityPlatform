<template>
  <a-modal
    :visible="visible"
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
import type { FlowDefinition } from '@/types/workflow'; // 假设有 FlowDefinition 类型

const props = defineProps<{
  visible: boolean;
  flowDefinition: any; // 应该是解析后的树或图结构
}>();

const emit = defineEmits<{
  'update:visible': [value: boolean];
  select: [nodeId: string];
}>();

const selectedNodeId = ref('');
const nodes = ref<any[]>([]);

watch(() => props.flowDefinition, (def) => {
  if (def) {
    // 解析流程定义，提取所有可跳转节点
    // 这里假设 def 是树结构，需要扁平化
    nodes.value = flattenNodes(def.rootNode);
  }
}, { immediate: true });

const flattenNodes = (node: any): any[] => {
  const list = [];
  if (node) {
    list.push({ id: node.id, name: node.nodeName, type: node.nodeType });
    if (node.childNode) {
      list.push(...flattenNodes(node.childNode));
    }
    if (node.conditionNodes) {
      node.conditionNodes.forEach((branch: any) => {
        if (branch.childNode) list.push(...flattenNodes(branch.childNode));
      });
    }
    if (node.parallelNodes) {
      node.parallelNodes.forEach((child: any) => {
        list.push(...flattenNodes(child));
      });
    }
  }
  return list;
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

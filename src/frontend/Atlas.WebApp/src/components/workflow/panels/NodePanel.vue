<template>
  <div class="node-panel">
    <div class="panel-header">
      <a-input
        v-model:value="searchText"
        placeholder="搜索节点"
        size="small"
        allow-clear
      >
        <template #prefix><SearchOutlined /></template>
      </a-input>
    </div>

    <div class="panel-body">
      <div v-for="category in filteredCategories" :key="category.name" class="node-category">
        <div class="category-title">{{ category.name }}</div>
        <div class="category-nodes">
          <div
            v-for="node in category.nodes"
            :key="node.type"
            class="node-item"
            draggable="true"
            @dragstart="handleDragStart(node.type)"
          >
            <span class="node-item-icon" :style="{ background: getNodeColor(node.type) }">
              {{ getNodeIcon(node.type) }}
            </span>
            <div class="node-item-info">
              <div class="node-item-name">{{ node.name }}</div>
              <div class="node-item-desc">{{ node.description }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { SearchOutlined } from '@ant-design/icons-vue'

const emit = defineEmits<{
  (e: 'drag-start', nodeType: string): void
}>()

const searchText = ref('')

interface NodeItem {
  type: string
  name: string
  description: string
  category: string
}

const allNodes: NodeItem[] = [
  { type: 'Entry', name: '开始', description: '工作流入口', category: '基础' },
  { type: 'Exit', name: '结束', description: '工作流出口', category: '基础' },
  { type: 'Llm', name: '大模型', description: '调用 LLM 生成文本', category: 'AI' },
  { type: 'Selector', name: '条件判断', description: '根据条件选择分支', category: '流程控制' },
  { type: 'Loop', name: '循环', description: '遍历数组元素', category: '流程控制' },
  { type: 'SubWorkflow', name: '子流程', description: '调用已发布子工作流', category: '流程控制' },
  { type: 'HttpRequester', name: 'HTTP 请求', description: '发送 HTTP 请求', category: '工具' },
  { type: 'CodeRunner', name: '代码执行', description: '执行代码片段', category: '工具' },
  { type: 'DatabaseQuery', name: '数据库查询', description: '执行 SQL 查询', category: '数据' },
  { type: 'AssignVariable', name: '变量赋值', description: '给变量赋值', category: '变量' },
  { type: 'VariableAggregator', name: '变量聚合', description: '合并多个变量', category: '变量' },
  { type: 'JsonSerialization', name: 'JSON 序列化', description: '对象转 JSON 字符串', category: 'JSON' },
  { type: 'JsonDeserialization', name: 'JSON 反序列化', description: 'JSON 字符串转对象', category: 'JSON' },
  { type: 'TextProcessor', name: '文本处理', description: '文本拼接/截取/格式化', category: '文本' },
]

const NODE_COLORS: Record<string, string> = {
  Entry: '#52c41a', Exit: '#ff4d4f', Llm: '#6366f1', Selector: '#f59e0b',
  Loop: '#f59e0b', SubWorkflow: '#8b5cf6', CodeRunner: '#10b981',
  HttpRequester: '#10b981', DatabaseQuery: '#3b82f6',
  AssignVariable: '#d946ef', VariableAggregator: '#d946ef', JsonSerialization: '#84cc16',
  JsonDeserialization: '#84cc16', TextProcessor: '#6b7280',
}

const NODE_ICONS: Record<string, string> = {
  Entry: '▶', Exit: '⏹', Llm: '🤖', Selector: '⟟', Loop: '↻',
  SubWorkflow: '⊞', CodeRunner: '⌨', HttpRequester: '🌐', DatabaseQuery: '🔍',
  AssignVariable: '=', VariableAggregator: '∪', JsonSerialization: '{}',
  JsonDeserialization: '{}', TextProcessor: 'T',
}

function getNodeColor(type: string) { return NODE_COLORS[type] ?? '#6b7280' }
function getNodeIcon(type: string) { return NODE_ICONS[type] ?? '□' }

const filteredCategories = computed(() => {
  const filtered = searchText.value
    ? allNodes.filter(n =>
        n.name.includes(searchText.value) ||
        n.description.includes(searchText.value) ||
        n.type.toLowerCase().includes(searchText.value.toLowerCase())
      )
    : allNodes

  const map = new Map<string, NodeItem[]>()
  for (const node of filtered) {
    if (!map.has(node.category)) map.set(node.category, [])
    map.get(node.category)!.push(node)
  }

  return Array.from(map.entries()).map(([name, nodes]) => ({ name, nodes }))
})

function handleDragStart(nodeType: string) {
  emit('drag-start', nodeType)
}
</script>

<style scoped>
.node-panel {
  width: 220px;
  background: #161b22;
  border-right: 1px solid #30363d;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  flex-shrink: 0;
}

.panel-header {
  padding: 12px;
  border-bottom: 1px solid #30363d;
}

.panel-body {
  flex: 1;
  overflow-y: auto;
  padding: 8px 0;
}

.node-category {
  margin-bottom: 8px;
}

.category-title {
  padding: 4px 12px;
  font-size: 11px;
  font-weight: 700;
  color: #7d8590;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.category-nodes {
  padding: 0 8px;
}

.node-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px;
  border-radius: 6px;
  cursor: grab;
  transition: background 0.15s;
  margin-bottom: 2px;
}

.node-item:hover {
  background: #1f2937;
}

.node-item:active {
  cursor: grabbing;
}

.node-item-icon {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  flex-shrink: 0;
}

.node-item-info {
  flex: 1;
  min-width: 0;
}

.node-item-name {
  font-size: 13px;
  font-weight: 600;
  color: #e6edf3;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.node-item-desc {
  font-size: 11px;
  color: #7d8590;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
</style>

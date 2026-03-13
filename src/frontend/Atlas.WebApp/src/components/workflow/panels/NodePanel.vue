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
  { type: 'OutputEmitter', name: '流式输出', description: '流式输出文本片段', category: '基础' },
  { type: 'LLM', name: '大模型', description: '调用 LLM 生成文本', category: 'AI' },
  { type: 'IntentDetector', name: '意图识别', description: '基于 LLM 的意图分类', category: 'AI' },
  { type: 'KnowledgeRetriever', name: '知识库检索', description: '从知识库检索文档', category: 'AI/RAG' },
  { type: 'KnowledgeIndexer', name: '知识库写入', description: '向知识库添加文档', category: 'AI/RAG' },
  { type: 'KnowledgeDeleter', name: '知识库删除', description: '从知识库删除文档', category: 'AI/RAG' },
  { type: 'If', name: '条件判断', description: '基于条件分支', category: '流程控制' },
  { type: 'Loop', name: '循环', description: '遍历数组元素', category: '流程控制' },
  { type: 'Break', name: 'Break', description: '退出循环', category: '流程控制' },
  { type: 'Continue', name: 'Continue', description: '跳过本次循环', category: '流程控制' },
  { type: 'Batch', name: '批处理', description: '并行处理数组元素', category: '流程控制' },
  { type: 'SubWorkflow', name: '子流程', description: '调用已发布子工作流', category: '流程控制' },
  { type: 'HttpRequester', name: 'HTTP 请求', description: '发送 HTTP 请求', category: '工具' },
  { type: 'CodeRunner', name: '代码执行', description: '执行代码片段', category: '工具' },
  { type: 'PluginApi', name: '插件调用', description: '调用插件 API', category: '工具' },
  { type: 'DatabaseQuery', name: '数据库查询', description: '执行 SQL 查询', category: '数据' },
  { type: 'DatabaseInsert', name: '数据库插入', description: '执行 SQL 插入', category: '数据' },
  { type: 'DatabaseUpdate', name: '数据库更新', description: '执行 SQL 更新', category: '数据' },
  { type: 'DatabaseDelete', name: '数据库删除', description: '执行 SQL 删除', category: '数据' },
  { type: 'AssignVariable', name: '变量赋值', description: '给变量赋值', category: '变量' },
  { type: 'VariableAggregator', name: '变量聚合', description: '合并多个变量', category: '变量' },
  { type: 'JsonSerialization', name: 'JSON 序列化', description: '对象转 JSON 字符串', category: 'JSON' },
  { type: 'JsonDeserialization', name: 'JSON 反序列化', description: 'JSON 字符串转对象', category: 'JSON' },
  { type: 'TextProcessor', name: '文本处理', description: '文本拼接/截取/格式化', category: '文本' },
  { type: 'MessageList', name: '消息列表', description: '读取会话消息', category: '消息' },
  { type: 'CreateMessage', name: '创建消息', description: '创建新消息', category: '消息' },
  { type: 'ConversationList', name: '会话列表', description: '列出会话', category: '消息' },
  { type: 'QuestionAnswer', name: '提问等待', description: '向用户提问并等待回答', category: '交互' },
]

const NODE_COLORS: Record<string, string> = {
  Entry: '#52c41a', Exit: '#ff4d4f', LLM: '#6366f1', If: '#f59e0b',
  Loop: '#f59e0b', Break: '#f59e0b', Continue: '#f59e0b', Batch: '#f59e0b',
  SubWorkflow: '#8b5cf6', IntentDetector: '#6366f1', KnowledgeRetriever: '#06b6d4',
  KnowledgeIndexer: '#06b6d4', KnowledgeDeleter: '#06b6d4', CodeRunner: '#10b981',
  HttpRequester: '#10b981', PluginApi: '#10b981', DatabaseQuery: '#3b82f6',
  DatabaseInsert: '#3b82f6', DatabaseUpdate: '#3b82f6', DatabaseDelete: '#3b82f6',
  AssignVariable: '#d946ef', VariableAggregator: '#d946ef', JsonSerialization: '#84cc16',
  JsonDeserialization: '#84cc16', TextProcessor: '#6b7280', MessageList: '#f97316',
  CreateMessage: '#f97316', ConversationList: '#f97316', QuestionAnswer: '#ec4899',
  OutputEmitter: '#52c41a',
}

const NODE_ICONS: Record<string, string> = {
  Entry: '▶', Exit: '⏹', LLM: '🤖', If: '⟟', Loop: '↻', Break: '⏏',
  Continue: '⏩', Batch: '≡', SubWorkflow: '⊞', IntentDetector: '🎯',
  KnowledgeRetriever: '📖', KnowledgeIndexer: '📝', KnowledgeDeleter: '🗑',
  CodeRunner: '⌨', HttpRequester: '🌐', PluginApi: '🔌', DatabaseQuery: '🔍',
  DatabaseInsert: '➕', DatabaseUpdate: '✏', DatabaseDelete: '🗑',
  AssignVariable: '=', VariableAggregator: '∪', JsonSerialization: '{}',
  JsonDeserialization: '{}', TextProcessor: 'T', MessageList: '💬',
  CreateMessage: '✉', ConversationList: '📋', QuestionAnswer: '❓', OutputEmitter: '📤',
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

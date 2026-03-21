<template>
  <div class="node-panel">
    <div class="panel-header">
      <a-input
        v-model:value="searchText"
        :placeholder="t('wfUi.nodePanel.phSearch')"
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
import { useI18n } from 'vue-i18n'
import { SearchOutlined } from '@ant-design/icons-vue'

const { t } = useI18n()

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

const NODE_DEFS: ReadonlyArray<{ type: string; desc: string; cat: string }> = [
  { type: 'Entry', desc: 'descEntry', cat: 'catBasic' },
  { type: 'Exit', desc: 'descExit', cat: 'catBasic' },
  { type: 'Llm', desc: 'descLlm', cat: 'catAi' },
  { type: 'Selector', desc: 'descSelector', cat: 'catFlow' },
  { type: 'Loop', desc: 'descLoop', cat: 'catFlow' },
  { type: 'SubWorkflow', desc: 'descSub', cat: 'catFlow' },
  { type: 'HttpRequester', desc: 'descHttp', cat: 'catTool' },
  { type: 'CodeRunner', desc: 'descCode', cat: 'catTool' },
  { type: 'DatabaseQuery', desc: 'descDb', cat: 'catData' },
  { type: 'AssignVariable', desc: 'descAssign', cat: 'catVar' },
  { type: 'VariableAggregator', desc: 'descAgg', cat: 'catVar' },
  { type: 'JsonSerialization', desc: 'descSer', cat: 'catJson' },
  { type: 'JsonDeserialization', desc: 'descDe', cat: 'catJson' },
  { type: 'TextProcessor', desc: 'descText', cat: 'catText' },
]

const allNodes = computed<NodeItem[]>(() =>
  NODE_DEFS.map((d) => ({
    type: d.type,
    name: t(`wfUi.nodeTypes.${d.type}`),
    description: t(`wfUi.nodePalette.${d.desc}`),
    category: t(`wfUi.nodePanel.${d.cat}`)
  }))
)

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
    ? allNodes.value.filter(n =>
        n.name.includes(searchText.value) ||
        n.description.includes(searchText.value) ||
        n.type.toLowerCase().includes(searchText.value.toLowerCase())
      )
    : allNodes.value

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

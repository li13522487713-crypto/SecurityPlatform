<template>
  <CrudPageLayout
    v-model:keyword="keyword"
    :title="t('logicFlow.nodePanel.title')"
    :search-placeholder="t('logicFlow.nodePanel.searchPlaceholder')"
    @search="handleSearch"
    @reset="handleReset"
  >
    <template #toolbar>
      <a-radio-group v-model:value="viewMode" size="small" style="margin-right: 12px">
        <a-radio-button value="tree">
          <ApartmentOutlined />
        </a-radio-button>
        <a-radio-button value="list">
          <UnorderedListOutlined />
        </a-radio-button>
      </a-radio-group>
      <a-select
        v-model:value="selectedCategory"
        :placeholder="t('logicFlow.nodePanel.allCategories')"
        allow-clear
        style="width: 160px"
        @change="fetchData"
      >
        <a-select-option v-for="cat in categories" :key="cat.category" :value="cat.category">
          {{ cat.displayName }}
        </a-select-option>
      </a-select>
    </template>

    <!-- Tree View -->
    <template v-if="viewMode === 'tree'">
      <a-collapse v-model:activeKey="activeCategoryKeys" :bordered="false">
        <a-collapse-panel
          v-for="group in groupedNodes"
          :key="group.category"
          :header="group.displayName"
        >
          <template #extra>
            <a-badge :count="group.items.length" :number-style="{ backgroundColor: '#e6f7ff', color: '#1890ff' }" />
          </template>
          <div class="node-grid">
            <div
              v-for="node in group.items"
              :key="node.typeKey"
              class="node-card"
              :style="{ borderLeftColor: node.uiMetadata?.color || '#d9d9d9' }"
              @click="handleNodeClick(node)"
            >
              <div class="node-card-icon" :style="{ color: node.uiMetadata?.color || '#1890ff' }">
                <component :is="getIconComponent(node.uiMetadata?.icon)" />
              </div>
              <div class="node-card-content">
                <div class="node-card-title">{{ node.displayName }}</div>
                <div class="node-card-desc">{{ node.description || '-' }}</div>
              </div>
              <div class="node-card-ports">
                <a-tag v-for="port in node.ports.slice(0, 3)" :key="port.portKey" size="small">
                  {{ port.displayName }}
                </a-tag>
                <a-tag v-if="node.ports.length > 3" size="small">+{{ node.ports.length - 3 }}</a-tag>
              </div>
            </div>
          </div>
        </a-collapse-panel>
      </a-collapse>
    </template>

    <!-- List View -->
    <template v-else>
      <a-table
        :columns="columns"
        :data-source="filteredNodes"
        :pagination="false"
        row-key="typeKey"
        size="small"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'displayName'">
            <span
              class="node-dot"
              :style="{ backgroundColor: record.uiMetadata?.color || '#d9d9d9' }"
            />
            {{ record.displayName }}
          </template>
          <template v-if="column.dataIndex === 'category'">
            <a-tag>{{ getCategoryName(record.category) }}</a-tag>
          </template>
          <template v-if="column.dataIndex === 'ports'">
            {{ record.ports.length }}
          </template>
          <template v-if="column.dataIndex === 'capabilities'">
            <a-space :size="2" wrap>
              <a-tag v-if="record.capabilities?.supportsRetry" color="blue">{{ t('logicFlow.nodePanel.capRetry') }}</a-tag>
              <a-tag v-if="record.capabilities?.supportsTimeout" color="orange">{{ t('logicFlow.nodePanel.capTimeout') }}</a-tag>
              <a-tag v-if="record.capabilities?.supportsCompensation" color="red">{{ t('logicFlow.nodePanel.capCompensation') }}</a-tag>
              <a-tag v-if="record.capabilities?.supportsParallelExecution" color="green">{{ t('logicFlow.nodePanel.capParallel') }}</a-tag>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <!-- Node Detail Drawer -->
    <a-drawer
      v-model:open="drawerVisible"
      :title="selectedNode?.displayName"
      width="480"
      placement="right"
    >
      <template v-if="selectedNode">
        <a-descriptions :column="1" bordered size="small">
          <a-descriptions-item :label="t('logicFlow.nodePanel.typeKey')">
            <a-typography-text code>{{ selectedNode.typeKey }}</a-typography-text>
          </a-descriptions-item>
          <a-descriptions-item :label="t('logicFlow.nodePanel.categoryLabel')">
            <a-tag :color="selectedNode.uiMetadata?.color">{{ getCategoryName(selectedNode.category) }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item :label="t('logicFlow.nodePanel.description')">
            {{ selectedNode.description || '-' }}
          </a-descriptions-item>
        </a-descriptions>

        <a-divider>{{ t('logicFlow.nodePanel.ports') }}</a-divider>
        <a-table
          :columns="portColumns"
          :data-source="selectedNode.ports"
          :pagination="false"
          row-key="portKey"
          size="small"
        >
          <template #bodyCell="{ column, record: port }">
            <template v-if="column.dataIndex === 'direction'">
              <a-tag :color="port.direction === 0 ? 'blue' : 'green'">
                {{ port.direction === 0 ? t('logicFlow.nodePanel.portInput') : t('logicFlow.nodePanel.portOutput') }}
              </a-tag>
            </template>
            <template v-if="column.dataIndex === 'portType'">
              {{ portTypeLabels[port.portType] || '-' }}
            </template>
          </template>
        </a-table>

        <a-divider>{{ t('logicFlow.nodePanel.capTitle') }}</a-divider>
        <a-space direction="vertical" style="width: 100%">
          <a-row v-for="cap in capabilityList" :key="cap.key" :gutter="8">
            <a-col :span="16">{{ cap.label }}</a-col>
            <a-col :span="8">
              <a-tag :color="cap.value ? 'success' : 'default'">{{ cap.value ? '✓' : '—' }}</a-tag>
            </a-col>
          </a-row>
        </a-space>
      </template>
    </a-drawer>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { ApartmentOutlined, UnorderedListOutlined } from '@ant-design/icons-vue'
import {
  getNodeTypeRegistry,
  getNodeTypeCategories,
  type NodeRegistryItem,
  type NodeCategoryInfo,
  NodeCategory,
} from '@/services/api-logic-flow'
import CrudPageLayout from '@/components/crud/CrudPageLayout.vue'

const { t } = useI18n()

const keyword = ref('')
const viewMode = ref<'tree' | 'list'>('tree')
const selectedCategory = ref<NodeCategory | undefined>(undefined)
const allNodes = ref<NodeRegistryItem[]>([])
const categories = ref<NodeCategoryInfo[]>([])
const drawerVisible = ref(false)
const selectedNode = ref<NodeRegistryItem | null>(null)
const activeCategoryKeys = ref<number[]>([])

const categoryNameMap: Record<number, string> = {
  [NodeCategory.Trigger]: '触发',
  [NodeCategory.DataRead]: '数据读取',
  [NodeCategory.DataTransform]: '数据变换',
  [NodeCategory.ControlFlow]: '控制流',
  [NodeCategory.Transaction]: '事务与可靠性',
  [NodeCategory.SystemIntegration]: '系统联动',
}

const portTypeLabels: Record<number, string> = {
  0: 'Control',
  1: 'Data',
  2: 'Error',
  3: 'Compensation',
}

const getCategoryName = (cat: NodeCategory) => categoryNameMap[cat] || cat.toString()

const filteredNodes = computed(() => {
  let list = allNodes.value
  if (selectedCategory.value != null) {
    list = list.filter(n => n.category === selectedCategory.value)
  }
  if (keyword.value) {
    const kw = keyword.value.toLowerCase()
    list = list.filter(n =>
      n.displayName.toLowerCase().includes(kw) ||
      n.typeKey.toLowerCase().includes(kw) ||
      (n.description && n.description.toLowerCase().includes(kw)),
    )
  }
  return list
})

const groupedNodes = computed(() => {
  const groups: { category: NodeCategory; displayName: string; items: NodeRegistryItem[] }[] = []
  const catOrder = [
    NodeCategory.Trigger, NodeCategory.DataRead, NodeCategory.DataTransform,
    NodeCategory.ControlFlow, NodeCategory.Transaction, NodeCategory.SystemIntegration,
  ]
  for (const cat of catOrder) {
    const items = filteredNodes.value.filter(n => n.category === cat)
    if (items.length > 0) {
      groups.push({ category: cat, displayName: getCategoryName(cat), items })
    }
  }
  return groups
})

const capabilityList = computed(() => {
  const caps = selectedNode.value?.capabilities
  if (!caps) return []
  return [
    { key: 'retry', label: t('logicFlow.nodePanel.capRetry'), value: caps.supportsRetry },
    { key: 'timeout', label: t('logicFlow.nodePanel.capTimeout'), value: caps.supportsTimeout },
    { key: 'compensation', label: t('logicFlow.nodePanel.capCompensation'), value: caps.supportsCompensation },
    { key: 'parallel', label: t('logicFlow.nodePanel.capParallel'), value: caps.supportsParallelExecution },
    { key: 'batching', label: t('logicFlow.nodePanel.capBatching'), value: caps.supportsBatching },
    { key: 'branch', label: t('logicFlow.nodePanel.capBranch'), value: caps.supportsConditionalBranching },
    { key: 'subflow', label: t('logicFlow.nodePanel.capSubFlow'), value: caps.supportsSubFlow },
    { key: 'breakpoint', label: t('logicFlow.nodePanel.capBreakpoint'), value: caps.supportsBreakpoint },
  ]
})

const columns = [
  { title: t('logicFlow.nodePanel.nameCol'), dataIndex: 'displayName', key: 'displayName' },
  { title: t('logicFlow.nodePanel.categoryLabel'), dataIndex: 'category', key: 'category', width: 120 },
  { title: t('logicFlow.nodePanel.typeKey'), dataIndex: 'typeKey', key: 'typeKey', width: 180 },
  { title: t('logicFlow.nodePanel.ports'), dataIndex: 'ports', key: 'ports', width: 80 },
  { title: t('logicFlow.nodePanel.capTitle'), dataIndex: 'capabilities', key: 'capabilities', width: 260 },
]

const portColumns = [
  { title: 'Key', dataIndex: 'portKey', key: 'portKey', width: 80 },
  { title: t('logicFlow.nodePanel.nameCol'), dataIndex: 'displayName', key: 'displayName' },
  { title: t('logicFlow.nodePanel.portDirection'), dataIndex: 'direction', key: 'direction', width: 80 },
  { title: t('logicFlow.nodePanel.portType'), dataIndex: 'portType', key: 'portType', width: 100 },
]

const getIconComponent = (iconName?: string) => {
  if (!iconName) return 'span'
  return iconName
}

const fetchData = async () => {
  const [regRes, catRes] = await Promise.all([
    getNodeTypeRegistry(selectedCategory.value),
    getNodeTypeCategories(),
  ])
  if (regRes.success && regRes.data) {
    allNodes.value = regRes.data
  }
  if (catRes.success && catRes.data) {
    categories.value = catRes.data
  }
  activeCategoryKeys.value = groupedNodes.value.map(g => g.category)
}

const handleSearch = () => {
  // filtering is reactive via computed
}

const handleReset = () => {
  keyword.value = ''
  selectedCategory.value = undefined
  fetchData()
}

const handleNodeClick = (node: NodeRegistryItem) => {
  selectedNode.value = node
  drawerVisible.value = true
}

onMounted(fetchData)
</script>

<style scoped>
.node-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 8px;
}

.node-card {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 10px 12px;
  border: 1px solid #f0f0f0;
  border-left-width: 3px;
  border-radius: 4px;
  cursor: pointer;
  transition: box-shadow 0.2s;
}

.node-card:hover {
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.09);
}

.node-card-icon {
  font-size: 20px;
  flex-shrink: 0;
  margin-top: 2px;
}

.node-card-content {
  flex: 1;
  min-width: 0;
}

.node-card-title {
  font-weight: 500;
  font-size: 13px;
  line-height: 20px;
}

.node-card-desc {
  font-size: 12px;
  color: #8c8c8c;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.node-card-ports {
  display: flex;
  flex-wrap: wrap;
  gap: 2px;
  flex-shrink: 0;
}

.node-dot {
  display: inline-block;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  margin-right: 6px;
}
</style>

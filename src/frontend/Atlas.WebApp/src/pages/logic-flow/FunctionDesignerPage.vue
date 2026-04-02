<template>
  <div class="function-designer-page">
    <a-page-header :title="t('logicFlow.functionDesigner.title')" :sub-title="t('logicFlow.functionDesigner.subtitle')">
      <template #extra>
        <a-button type="primary" @click="handleCreate">
          {{ t('common.create') }}
        </a-button>
      </template>
    </a-page-header>

    <a-card :bordered="false">
      <div style="display: flex; gap: 12px; margin-bottom: 16px;">
        <a-input-search v-model:value="searchKeyword" :placeholder="t('common.search')"
          style="width: 280px;" allow-clear @search="loadData" />
        <a-select v-model:value="filterCategory" :placeholder="t('logicFlow.functionDesigner.allCategories')"
          style="width: 160px;" allow-clear @change="loadData">
          <a-select-option :value="1">{{ t('logicFlow.functionDesigner.catString') }}</a-select-option>
          <a-select-option :value="2">{{ t('logicFlow.functionDesigner.catNumeric') }}</a-select-option>
          <a-select-option :value="3">{{ t('logicFlow.functionDesigner.catDate') }}</a-select-option>
          <a-select-option :value="4">{{ t('logicFlow.functionDesigner.catConversion') }}</a-select-option>
          <a-select-option :value="5">{{ t('logicFlow.functionDesigner.catCollection') }}</a-select-option>
          <a-select-option :value="6">{{ t('logicFlow.functionDesigner.catAggregate') }}</a-select-option>
          <a-select-option :value="7">{{ t('logicFlow.functionDesigner.catWindow') }}</a-select-option>
          <a-select-option :value="99">{{ t('logicFlow.functionDesigner.catCustom') }}</a-select-option>
        </a-select>
      </div>

      <a-table :columns="columns" :data-source="dataList" :loading="loading" :pagination="pagination"
        row-key="id" size="middle" @change="handleTableChange">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'category'">
            <a-tag :color="categoryColor(record.category)">{{ categoryLabel(record.category) }}</a-tag>
          </template>
          <template v-if="column.key === 'isEnabled'">
            <a-tag :color="record.isEnabled ? 'green' : 'default'">
              {{ record.isEnabled ? t('common.statusEnabled') : t('common.statusDisabled') }}
            </a-tag>
          </template>
          <template v-if="column.key === 'isBuiltin'">
            <a-tag v-if="record.isBuiltin" color="blue">{{ t('logicFlow.functionDesigner.builtin') }}</a-tag>
            <a-tag v-else color="orange">{{ t('logicFlow.functionDesigner.custom') }}</a-tag>
          </template>
          <template v-if="column.key === 'action'">
            <a-space>
              <a @click="handleEdit(record)">{{ t('common.edit') }}</a>
              <a-popconfirm :title="t('common.delete') + '?'" @confirm="handleDelete(record.id)"
                :disabled="record.isBuiltin">
                <a :class="{ disabled: record.isBuiltin }">{{ t('common.delete') }}</a>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal v-model:open="modalVisible" :title="editingId ? t('common.edit') : t('common.create')"
      width="720px" @ok="handleSave" :confirm-loading="saving">
      <a-form :label-col="{ span: 5 }" :wrapper-col="{ span: 18 }">
        <a-form-item :label="t('logicFlow.functionDesigner.name')" required>
          <a-input v-model:value="form.name" :disabled="!!editingId && form.isBuiltin" />
        </a-form-item>
        <a-form-item :label="t('logicFlow.functionDesigner.displayName')">
          <a-input v-model:value="form.displayName" />
        </a-form-item>
        <a-form-item :label="t('logicFlow.functionDesigner.description')">
          <a-textarea v-model:value="form.description" :rows="2" />
        </a-form-item>
        <a-form-item :label="t('logicFlow.functionDesigner.category')" required>
          <a-select v-model:value="form.category">
            <a-select-option :value="1">{{ t('logicFlow.functionDesigner.catString') }}</a-select-option>
            <a-select-option :value="2">{{ t('logicFlow.functionDesigner.catNumeric') }}</a-select-option>
            <a-select-option :value="3">{{ t('logicFlow.functionDesigner.catDate') }}</a-select-option>
            <a-select-option :value="4">{{ t('logicFlow.functionDesigner.catConversion') }}</a-select-option>
            <a-select-option :value="5">{{ t('logicFlow.functionDesigner.catCollection') }}</a-select-option>
            <a-select-option :value="6">{{ t('logicFlow.functionDesigner.catAggregate') }}</a-select-option>
            <a-select-option :value="7">{{ t('logicFlow.functionDesigner.catWindow') }}</a-select-option>
            <a-select-option :value="99">{{ t('logicFlow.functionDesigner.catCustom') }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item :label="t('logicFlow.functionDesigner.bodyExpression')">
          <a-textarea v-model:value="form.bodyExpression" :rows="5"
            class="code-textarea"
            :placeholder="t('logicFlow.functionDesigner.bodyPlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('logicFlow.functionDesigner.parameters')">
          <a-textarea v-model:value="form.parametersJson" :rows="3"
            class="code-textarea" placeholder="JSON" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { message } from 'ant-design-vue'
import type { TablePaginationConfig } from 'ant-design-vue'
import {
  getFunctionDefinitionsPaged,
  getFunctionDefinitionById,
  createFunctionDefinition,
  updateFunctionDefinition,
  deleteFunctionDefinition,
  type FunctionDefinitionListItem,
} from '@/services/api-logic-flow'

const { t } = useI18n()

const searchKeyword = ref('')
const filterCategory = ref<number | undefined>()
const loading = ref(false)
const saving = ref(false)
const modalVisible = ref(false)
const editingId = ref<number | null>(null)
const dataList = ref<FunctionDefinitionListItem[]>([])
const pagination = reactive({ current: 1, pageSize: 20, total: 0 })

const form = reactive({
  name: '',
  displayName: '',
  description: '',
  category: 99,
  parametersJson: '[]',
  returnType: 98,
  bodyExpression: '',
  isBuiltin: false,
  isEnabled: true,
  sortOrder: 0,
})

const columns = computed(() => [
  { title: t('logicFlow.functionDesigner.name'), dataIndex: 'name', key: 'name', width: 180 },
  { title: t('logicFlow.functionDesigner.displayName'), dataIndex: 'displayName', key: 'displayName', ellipsis: true },
  { title: t('logicFlow.functionDesigner.category'), key: 'category', width: 120 },
  { title: t('logicFlow.functionDesigner.type'), key: 'isBuiltin', width: 100 },
  { title: t('logicFlow.functionDesigner.status'), key: 'isEnabled', width: 100 },
  { title: t('logicFlow.functionDesigner.action'), key: 'action', width: 140, fixed: 'right' as const },
])

function categoryLabel(cat: number): string {
  const map: Record<number, string> = {
    1: t('logicFlow.functionDesigner.catString'),
    2: t('logicFlow.functionDesigner.catNumeric'),
    3: t('logicFlow.functionDesigner.catDate'),
    4: t('logicFlow.functionDesigner.catConversion'),
    5: t('logicFlow.functionDesigner.catCollection'),
    6: t('logicFlow.functionDesigner.catAggregate'),
    7: t('logicFlow.functionDesigner.catWindow'),
    99: t('logicFlow.functionDesigner.catCustom'),
  }
  return map[cat] ?? String(cat)
}

function categoryColor(cat: number): string {
  const map: Record<number, string> = { 1: 'blue', 2: 'green', 3: 'orange', 4: 'purple', 5: 'cyan', 6: 'red', 7: 'magenta', 99: 'gold' }
  return map[cat] ?? 'default'
}

async function loadData() {
  loading.value = true
  try {
    const response = await getFunctionDefinitionsPaged({
      pageIndex: pagination.current,
      pageSize: pagination.pageSize,
      keyword: searchKeyword.value || undefined,
      category: filterCategory.value,
    })
    if (response?.data) {
      dataList.value = response.data.items
      pagination.total = response.data.total
    }
  } finally {
    loading.value = false
  }
}

function handleTableChange(pag: TablePaginationConfig) {
  pagination.current = pag.current ?? 1
  pagination.pageSize = pag.pageSize ?? 20
  loadData()
}

function handleCreate() {
  editingId.value = null
  Object.assign(form, { name: '', displayName: '', description: '', category: 99, parametersJson: '[]', returnType: 98, bodyExpression: '', isBuiltin: false, isEnabled: true, sortOrder: 0 })
  modalVisible.value = true
}

async function handleEdit(record: FunctionDefinitionListItem) {
  const response = await getFunctionDefinitionById(record.id)
  const detail = response?.data
  if (!detail) return
  editingId.value = record.id
  Object.assign(form, {
    name: detail.name,
    displayName: detail.displayName ?? '',
    description: detail.description ?? '',
    category: detail.category,
    parametersJson: detail.parametersJson,
    returnType: detail.returnType,
    bodyExpression: detail.bodyExpression ?? '',
    isBuiltin: detail.isBuiltin,
    isEnabled: detail.isEnabled,
    sortOrder: detail.sortOrder,
  })
  modalVisible.value = true
}

async function handleSave() {
  saving.value = true
  try {
    if (editingId.value) {
      await updateFunctionDefinition(editingId.value, { ...form, id: editingId.value })
      message.success(t('common.save') + ' ✓')
    } else {
      await createFunctionDefinition(form)
      message.success(t('common.create') + ' ✓')
    }
    modalVisible.value = false
    loadData()
  } finally {
    saving.value = false
  }
}

async function handleDelete(id: number) {
  await deleteFunctionDefinition(id)
  message.success(t('common.delete') + ' ✓')
  loadData()
}

onMounted(loadData)
</script>

<style scoped>
.function-designer-page {
  padding: 16px;
}
.code-textarea {
  font-family: 'Cascadia Code', 'Fira Code', 'Consolas', 'Monaco', monospace;
  font-size: 13px;
}
.disabled {
  color: #ccc;
  pointer-events: none;
}
</style>

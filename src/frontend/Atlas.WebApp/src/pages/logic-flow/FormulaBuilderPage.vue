<template>
  <div class="formula-builder-page">
    <a-page-header :title="t('logicFlow.formulaBuilder.title')" :sub-title="t('logicFlow.formulaBuilder.subtitle')">
      <template #extra>
        <a-radio-group v-model:value="mode" button-style="solid" size="small">
          <a-radio-button value="text">{{ t('logicFlow.formulaBuilder.textMode') }}</a-radio-button>
          <a-radio-button value="visual">{{ t('logicFlow.formulaBuilder.visualMode') }}</a-radio-button>
        </a-radio-group>
      </template>
    </a-page-header>

    <a-row :gutter="16">
      <!-- Function Catalog -->
      <a-col :span="6">
        <a-card :title="t('logicFlow.formulaBuilder.functionCatalog')" :bordered="false" size="small">
          <a-input-search v-model:value="fnSearch" :placeholder="t('common.search')"
            allow-clear size="small" style="margin-bottom: 8px;" />
          <a-collapse v-model:active-key="expandedCategories" :bordered="false" size="small">
            <a-collapse-panel v-for="cat in filteredCategories" :key="cat.key" :header="cat.label">
              <div v-for="fn in cat.functions" :key="fn.name"
                class="fn-item" @click="insertFunction(fn.name)">
                <span class="fn-name">{{ fn.name }}</span>
                <span class="fn-desc">{{ fn.displayName ?? fn.name }}</span>
              </div>
            </a-collapse-panel>
          </a-collapse>
        </a-card>
      </a-col>

      <!-- Editor Area -->
      <a-col :span="12">
        <a-card :bordered="false" size="small">
          <template v-if="mode === 'text'">
            <div class="editor-label">{{ t('logicFlow.formulaBuilder.expression') }}</div>
            <a-textarea ref="editorRef" v-model:value="expression" :rows="8"
              class="code-textarea"
              :placeholder="t('logicFlow.formulaBuilder.expressionPlaceholder')" />
          </template>

          <template v-else>
            <div class="editor-label">{{ t('logicFlow.formulaBuilder.visualEditor') }}</div>
            <div class="visual-editor">
              <a-select v-model:value="visualFn" style="width: 200px;" show-search
                :placeholder="t('logicFlow.formulaBuilder.selectFunction')"
                :filter-option="filterFnOption">
                <a-select-option v-for="fn in allFunctions" :key="fn.name" :value="fn.name">
                  {{ fn.name }}
                </a-select-option>
              </a-select>
              <div v-if="visualFn" class="visual-params">
                <div v-for="(param, idx) in visualParams" :key="idx" class="param-row">
                  <span class="param-label">{{ param.name }}:</span>
                  <a-input v-model:value="param.value" size="small" style="width: 240px;" />
                </div>
              </div>
              <a-button type="primary" size="small" style="margin-top: 12px;" @click="buildFromVisual">
                {{ t('logicFlow.formulaBuilder.generate') }}
              </a-button>
            </div>
          </template>

          <a-divider />

          <a-space>
            <a-button type="primary" :loading="validating" @click="handleValidate">
              {{ t('logicFlow.formulaBuilder.validate') }}
            </a-button>
            <a-button :loading="evaluating" @click="handleEvaluate">
              {{ t('logicFlow.formulaBuilder.evaluate') }}
            </a-button>
          </a-space>

          <div v-if="validationResult" style="margin-top: 12px;">
            <a-alert v-if="validationResult.isValid" type="success"
              :message="t('logicFlow.formulaBuilder.validOk')" show-icon />
            <a-alert v-else type="error" :message="validationResult.errors.join('; ')" show-icon />
          </div>
        </a-card>
      </a-col>

      <!-- Context & Result -->
      <a-col :span="6">
        <a-card :title="t('logicFlow.formulaBuilder.context')" :bordered="false" size="small">
          <div class="editor-label">{{ t('logicFlow.formulaBuilder.variables') }}</div>
          <a-textarea v-model:value="contextJson" :rows="6" class="code-textarea"
            placeholder='{"name": "Alice", "age": 30}' />
        </a-card>

        <a-card v-if="evalResult" :title="t('logicFlow.formulaBuilder.result')" :bordered="false"
          size="small" style="margin-top: 12px;">
          <a-descriptions :column="1" size="small" :bordered="true">
            <a-descriptions-item :label="t('logicFlow.formulaBuilder.success')">
              <a-tag :color="evalResult.success ? 'green' : 'red'">{{ evalResult.success }}</a-tag>
            </a-descriptions-item>
            <a-descriptions-item :label="t('logicFlow.formulaBuilder.value')">
              {{ evalResult.resultValue ?? '-' }}
            </a-descriptions-item>
            <a-descriptions-item v-if="evalResult.error" :label="t('logicFlow.formulaBuilder.error')">
              <span style="color: #ff4d4f;">{{ evalResult.error }}</span>
            </a-descriptions-item>
          </a-descriptions>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { validateExpression, evaluateExpression, type ExpressionValidateResponse, type ExpressionEvaluateResponse } from '@/services/api-expression'
import { getFunctionDefinitionsAll, type FunctionDefinitionListItem } from '@/services/api-logic-flow'

const { t } = useI18n()

const mode = ref<'text' | 'visual'>('text')
const expression = ref('')
const contextJson = ref('{}')
const fnSearch = ref('')
const visualFn = ref<string | undefined>()
const validating = ref(false)
const evaluating = ref(false)
const validationResult = ref<ExpressionValidateResponse | null>(null)
const evalResult = ref<ExpressionEvaluateResponse | null>(null)
const allFunctions = ref<FunctionDefinitionListItem[]>([])
const expandedCategories = ref<string[]>([])

const categoryMap: Record<number, string> = {
  1: 'String', 2: 'Numeric', 3: 'Date', 4: 'Conversion',
  5: 'Collection', 6: 'Aggregate', 7: 'Window', 99: 'Custom',
}

interface VisualParam { name: string; value: string }
const visualParams = ref<VisualParam[]>([])

const filteredCategories = computed(() => {
  const groups = new Map<number, { key: string; label: string; functions: FunctionDefinitionListItem[] }>()
  for (const fn of allFunctions.value) {
    if (fnSearch.value && !fn.name.toLowerCase().includes(fnSearch.value.toLowerCase())) continue
    if (!groups.has(fn.category)) {
      groups.set(fn.category, {
        key: String(fn.category),
        label: categoryMap[fn.category] ?? `Category ${fn.category}`,
        functions: [],
      })
    }
    groups.get(fn.category)!.functions.push(fn)
  }
  return Array.from(groups.values()).sort((a, b) => Number(a.key) - Number(b.key))
})

function filterFnOption(input: string, option: { value: string }) {
  return option.value.toLowerCase().includes(input.toLowerCase())
}

function insertFunction(name: string) {
  expression.value += `${name}()`
}

function buildFromVisual() {
  if (!visualFn.value) return
  const args = visualParams.value.map(p => p.value || '""').join(', ')
  expression.value = `${visualFn.value}(${args})`
  mode.value = 'text'
}

async function handleValidate() {
  if (!expression.value.trim()) return
  validating.value = true
  evalResult.value = null
  try {
    const response = await validateExpression({ expression: expression.value })
    validationResult.value = response ?? null
  } finally {
    validating.value = false
  }
}

async function handleEvaluate() {
  if (!expression.value.trim()) return
  evaluating.value = true
  validationResult.value = null
  try {
    let record: Record<string, unknown> = {}
    try { record = JSON.parse(contextJson.value) } catch { /* ignore */ }
    const response = await evaluateExpression({ expression: expression.value, record })
    evalResult.value = response ?? null
  } finally {
    evaluating.value = false
  }
}

onMounted(async () => {
  const response = await getFunctionDefinitionsAll()
  if (response?.data) allFunctions.value = response.data
})
</script>

<style scoped>
.formula-builder-page {
  padding: 16px;
}
.code-textarea {
  font-family: 'Cascadia Code', 'Fira Code', 'Consolas', 'Monaco', monospace;
  font-size: 13px;
}
.editor-label {
  font-weight: 500;
  margin-bottom: 8px;
  color: #333;
}
.fn-item {
  padding: 4px 8px;
  cursor: pointer;
  border-radius: 4px;
  transition: background-color 0.15s;
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.fn-item:hover {
  background-color: #f0f5ff;
}
.fn-name {
  font-family: 'Cascadia Code', 'Fira Code', 'Consolas', monospace;
  font-size: 12px;
  font-weight: 600;
  color: #1890ff;
}
.fn-desc {
  font-size: 11px;
  color: #999;
}
.visual-editor {
  padding: 16px;
  border: 1px dashed #d9d9d9;
  border-radius: 8px;
  min-height: 160px;
}
.visual-params {
  margin-top: 12px;
}
.param-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}
.param-label {
  min-width: 80px;
  text-align: right;
  font-size: 13px;
  color: #555;
}
</style>

<template>
  <div class="properties-panel">
    <div class="panel-header">
      <span class="panel-title">节点属性</span>
      <a-button type="text" size="small" @click="$emit('close')">
        <CloseOutlined />
      </a-button>
    </div>

    <div class="panel-body">
      <!-- 基础信息 -->
      <div class="prop-section">
        <div class="section-title">基础信息</div>
        <a-form layout="vertical" size="small">
          <a-form-item label="节点标题">
            <a-input v-model:value="localTitle" @change="emitUpdate" />
          </a-form-item>
          <a-form-item label="节点 Key">
            <a-input :value="node.key" disabled />
          </a-form-item>
          <a-form-item label="节点类型">
            <a-tag>{{ node.type }}</a-tag>
          </a-form-item>
        </a-form>
      </div>

      <!-- 节点专属配置 -->
      <div class="prop-section">
        <div class="section-title">节点配置</div>

        <!-- LLM 节点 -->
        <template v-if="node.type === 'LLM'">
          <a-form layout="vertical" size="small">
            <a-form-item label="模型">
              <a-select v-model:value="localConfigs.model" @change="emitUpdate">
                <a-select-option value="gpt-4o">GPT-4o</a-select-option>
                <a-select-option value="gpt-4o-mini">GPT-4o Mini</a-select-option>
                <a-select-option value="gpt-3.5-turbo">GPT-3.5 Turbo</a-select-option>
                <a-select-option value="claude-3-5-sonnet-20241022">Claude 3.5 Sonnet</a-select-option>
              </a-select>
            </a-form-item>
            <a-form-item label="系统提示词">
              <a-textarea v-model:value="localConfigs.systemPrompt" :rows="3" @change="emitUpdate" />
            </a-form-item>
            <a-form-item label="用户提示词（支持 {{变量}} 模板）">
              <a-textarea v-model:value="localConfigs.userPrompt" :rows="5" @change="emitUpdate" />
            </a-form-item>
            <a-form-item label="Temperature">
              <a-slider v-model:value="localConfigs.temperature" :min="0" :max="2" :step="0.1" @change="emitUpdate" />
            </a-form-item>
            <a-form-item label="最大 Token 数">
              <a-input-number v-model:value="localConfigs.maxTokens" :min="100" :max="8000" @change="emitUpdate" style="width:100%" />
            </a-form-item>
          </a-form>
        </template>

        <!-- If 节点 -->
        <template v-else-if="node.type === 'If'">
          <a-form layout="vertical" size="small">
            <a-form-item label="逻辑关系">
              <a-radio-group v-model:value="localConfigs.logic" @change="emitUpdate">
                <a-radio value="and">AND（全部满足）</a-radio>
                <a-radio value="or">OR（任一满足）</a-radio>
              </a-radio-group>
            </a-form-item>
            <a-form-item label="条件列表">
              <div v-for="(cond, idx) in conditions" :key="idx" class="condition-row">
                <a-input v-model:value="cond.left" placeholder="变量引用" style="width:35%" size="small" />
                <a-select v-model:value="cond.op" style="width:22%" size="small">
                  <a-select-option value="eq">==</a-select-option>
                  <a-select-option value="ne">!=</a-select-option>
                  <a-select-option value="gt">&gt;</a-select-option>
                  <a-select-option value="lt">&lt;</a-select-option>
                  <a-select-option value="contains">包含</a-select-option>
                </a-select>
                <a-input v-model:value="cond.right" placeholder="值" style="width:30%" size="small" />
                <a-button size="small" danger @click="removeCondition(idx)">-</a-button>
              </div>
              <a-button size="small" @click="addCondition">+ 添加条件</a-button>
            </a-form-item>
          </a-form>
        </template>

        <!-- HTTP 请求节点 -->
        <template v-else-if="node.type === 'HttpRequester'">
          <a-form layout="vertical" size="small">
            <a-form-item label="请求方法">
              <a-select v-model:value="localConfigs.method" @change="emitUpdate">
                <a-select-option value="GET">GET</a-select-option>
                <a-select-option value="POST">POST</a-select-option>
                <a-select-option value="PUT">PUT</a-select-option>
                <a-select-option value="DELETE">DELETE</a-select-option>
                <a-select-option value="PATCH">PATCH</a-select-option>
              </a-select>
            </a-form-item>
            <a-form-item label="URL（支持 {{变量}} 模板）">
              <a-input v-model:value="localConfigs.url" @change="emitUpdate" />
            </a-form-item>
            <a-form-item label="请求体模板（JSON）">
              <a-textarea v-model:value="localConfigs.bodyTemplate" :rows="4" @change="emitUpdate" />
            </a-form-item>
          </a-form>
        </template>

        <!-- 代码执行节点 -->
        <template v-else-if="node.type === 'CodeRunner'">
          <a-form layout="vertical" size="small">
            <a-form-item label="表达式（支持 {{变量}} 模板）">
              <a-textarea v-model:value="localConfigs.expression" :rows="6" @change="emitUpdate" />
            </a-form-item>
          </a-form>
        </template>

        <!-- Loop 节点 -->
        <template v-else-if="node.type === 'Loop'">
          <a-form layout="vertical" size="small">
            <a-form-item label="数组引用">
              <a-input v-model:value="localConfigs.arrayRef" placeholder="如 entry_1.items" @change="emitUpdate" />
            </a-form-item>
          </a-form>
        </template>

        <!-- 子流程节点 -->
        <template v-else-if="node.type === 'SubWorkflow'">
          <a-form layout="vertical" size="small">
            <a-form-item label="工作流 ID">
              <a-input-number v-model:value="localConfigs.workflowId" @change="emitUpdate" style="width:100%" />
            </a-form-item>
            <a-form-item label="版本（空 = 最新草稿）">
              <a-input v-model:value="localConfigs.version" placeholder="如 1.0.0" @change="emitUpdate" />
            </a-form-item>
          </a-form>
        </template>

        <!-- 通用 JSON 配置（其他节点） -->
        <template v-else>
          <a-form layout="vertical" size="small">
            <a-form-item label="配置（JSON 格式）">
              <a-textarea
                :value="configsJson"
                :rows="8"
                @change="handleRawConfigsChange"
                style="font-family: monospace; font-size: 12px"
              />
            </a-form-item>
          </a-form>
        </template>
      </div>

      <!-- 输入映射 -->
      <div class="prop-section">
        <div class="section-title">输入映射</div>
        <div class="mapping-hint">格式：字段名 → 上游变量（如 entry_1.userInput）</div>
        <div v-for="(ref, field) in localInputMappings" :key="field" class="mapping-row">
          <a-input :value="field" disabled style="width: 40%" size="small" />
          <span style="color: #9ca3af; padding: 0 8px">→</span>
          <a-input v-model:value="localInputMappings[field]" size="small" style="width: 50%" @change="emitUpdate" />
          <a-button size="small" @click="removeMapping(field)">-</a-button>
        </div>
        <div class="add-mapping">
          <a-input v-model:value="newMappingField" placeholder="字段名" size="small" style="width: 40%" />
          <span style="color: #9ca3af; padding: 0 8px">→</span>
          <a-input v-model:value="newMappingRef" placeholder="引用" size="small" style="width: 40%" />
          <a-button size="small" @click="addMapping">+</a-button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { CloseOutlined } from '@ant-design/icons-vue'
import type { NodeSchema, NodeTypeMetadata } from '@/types/workflow-v2'

const props = defineProps<{
  node: NodeSchema
  nodeTypesMetadata: NodeTypeMetadata[]
}>()

const emit = defineEmits<{
  (e: 'update', nodeKey: string, configs: Record<string, unknown>, inputMappings: Record<string, string>, title: string): void
  (e: 'close'): void
}>()

const localTitle = ref(props.node.title)
const localConfigs = reactive<Record<string, unknown>>({ ...props.node.configs })
const localInputMappings = reactive<Record<string, string>>({ ...props.node.inputMappings })

watch(() => props.node, (newNode) => {
  localTitle.value = newNode.title
  Object.assign(localConfigs, newNode.configs)
  Object.keys(localInputMappings).forEach(k => delete localInputMappings[k])
  Object.assign(localInputMappings, newNode.inputMappings)
}, { deep: true })

// If 节点条件列表
interface Condition { left: string; op: string; right: string }
const conditions = computed<Condition[]>(() => {
  const c = localConfigs.conditions
  if (Array.isArray(c)) return c as Condition[]
  return []
})

function addCondition() {
  const arr = [...conditions.value, { left: '', op: 'eq', right: '' }]
  localConfigs.conditions = arr
  emitUpdate()
}

function removeCondition(idx: number) {
  const arr = [...conditions.value]
  arr.splice(idx, 1)
  localConfigs.conditions = arr
  emitUpdate()
}

// 通用配置 JSON
const configsJson = computed(() => JSON.stringify(localConfigs, null, 2))

function handleRawConfigsChange(e: Event) {
  try {
    const parsed = JSON.parse((e.target as HTMLTextAreaElement).value)
    Object.keys(localConfigs).forEach(k => delete localConfigs[k])
    Object.assign(localConfigs, parsed)
    emitUpdate()
  } catch {
    // ignore invalid JSON
  }
}

// 输入映射
const newMappingField = ref('')
const newMappingRef = ref('')

function addMapping() {
  if (!newMappingField.value) return
  localInputMappings[newMappingField.value] = newMappingRef.value
  newMappingField.value = ''
  newMappingRef.value = ''
  emitUpdate()
}

function removeMapping(field: string) {
  delete localInputMappings[field]
  emitUpdate()
}

function emitUpdate() {
  emit(
    'update',
    props.node.key,
    { ...localConfigs },
    { ...localInputMappings },
    localTitle.value,
  )
}
</script>

<style scoped>
.properties-panel {
  width: 320px;
  background: #161b22;
  border-left: 1px solid #30363d;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  flex-shrink: 0;
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid #30363d;
}

.panel-title {
  font-weight: 600;
  color: #e6edf3;
}

.panel-body {
  flex: 1;
  overflow-y: auto;
  padding: 0;
}

.prop-section {
  padding: 16px;
  border-bottom: 1px solid #21262d;
}

.section-title {
  font-size: 12px;
  font-weight: 700;
  color: #7d8590;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-bottom: 12px;
}

.condition-row,
.mapping-row,
.add-mapping {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-bottom: 8px;
}

.mapping-hint {
  font-size: 11px;
  color: #7d8590;
  margin-bottom: 8px;
}

:deep(.ant-form-item) {
  margin-bottom: 12px;
}

:deep(.ant-input),
:deep(.ant-input-number),
:deep(.ant-select-selector),
:deep(.ant-textarea) {
  background: #0d1117 !important;
  border-color: #30363d !important;
  color: #e6edf3 !important;
}
</style>

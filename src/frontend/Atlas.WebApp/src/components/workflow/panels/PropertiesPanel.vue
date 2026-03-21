<template>
  <div class="properties-panel">
    <div class="panel-header">
      <span class="panel-title">{{ t('wfUi.properties.title') }}</span>
      <a-button type="text" size="small" @click="$emit('close')">
        <CloseOutlined />
      </a-button>
    </div>

    <div class="panel-body">
      <div class="prop-section">
        <div class="section-title">{{ t('wfUi.properties.basic') }}</div>
        <a-form layout="vertical" size="small">
          <a-form-item :label="t('wfUi.properties.labelTitle')">
            <a-input v-model:value="localTitle" @change="emitUpdate" />
          </a-form-item>
          <a-form-item :label="t('wfUi.properties.labelKey')">
            <a-input :value="node.key" disabled />
          </a-form-item>
          <a-form-item :label="t('wfUi.properties.labelType')">
            <a-tag>{{ node.type }}</a-tag>
          </a-form-item>
        </a-form>
      </div>

      <div class="prop-section">
        <div class="section-title">{{ t('wfUi.properties.nodeConfig') }}</div>

        <template v-if="node.type === 'Llm'">
          <a-form layout="vertical" size="small">
            <a-form-item :label="t('wfUi.properties.llmModel')">
              <a-select v-model:value="localConfigs.model" @change="emitUpdate">
                <a-select-option value="gpt-4o">GPT-4o</a-select-option>
                <a-select-option value="gpt-4o-mini">GPT-4o Mini</a-select-option>
                <a-select-option value="gpt-3.5-turbo">GPT-3.5 Turbo</a-select-option>
                <a-select-option value="claude-3-5-sonnet-20241022">Claude 3.5 Sonnet</a-select-option>
              </a-select>
            </a-form-item>
            <a-form-item :label="t('wfUi.properties.llmProvider')">
              <a-input v-model:value="localConfigs.provider" placeholder="openai / deepseek / ollama" @change="emitUpdate" />
            </a-form-item>
            <a-form-item :label="t('wfUi.properties.llmPrompt')">
              <a-textarea v-model:value="localConfigs.prompt" :rows="5" @change="emitUpdate" />
            </a-form-item>
            <a-form-item label="Temperature">
              <a-slider v-model:value="localConfigs.temperature" :min="0" :max="2" :step="0.1" @change="emitUpdate" />
            </a-form-item>
            <a-form-item :label="t('wfUi.properties.llmMaxTokens')">
              <a-input-number v-model:value="localConfigs.maxTokens" :min="100" :max="8000" @change="emitUpdate" style="width:100%" />
            </a-form-item>
            <a-form-item :label="t('wfUi.properties.llmOutVar')">
              <a-input v-model:value="localConfigs.outputKey" placeholder="llm_output" @change="emitUpdate" />
            </a-form-item>
          </a-form>
        </template>

        <template v-else-if="node.type === 'Selector'">
          <a-form layout="vertical" size="small">
            <a-form-item :label="t('wfUi.properties.selExpr')">
              <a-textarea
                v-model:value="localConfigs.condition"
                :rows="4"
                :placeholder="t('wfUi.properties.phSelectorExpr')"
                @change="emitUpdate"
              />
            </a-form-item>
          </a-form>
        </template>

        <template v-else-if="node.type === 'HttpRequester'">
          <a-form layout="vertical" size="small">
            <a-form-item :label="t('wfUi.properties.httpMethod')">
              <a-select v-model:value="localConfigs.method" @change="emitUpdate">
                <a-select-option value="GET">GET</a-select-option>
                <a-select-option value="POST">POST</a-select-option>
                <a-select-option value="PUT">PUT</a-select-option>
                <a-select-option value="DELETE">DELETE</a-select-option>
                <a-select-option value="PATCH">PATCH</a-select-option>
              </a-select>
            </a-form-item>
            <a-form-item :label="t('wfUi.properties.httpUrl')">
              <a-input v-model:value="localConfigs.url" @change="emitUpdate" />
            </a-form-item>
            <a-form-item :label="t('wfUi.properties.httpBody')">
              <a-textarea v-model:value="localConfigs.body" :rows="4" @change="emitUpdate" />
            </a-form-item>
          </a-form>
        </template>

        <template v-else-if="node.type === 'CodeRunner'">
          <a-form layout="vertical" size="small">
            <a-form-item :label="t('wfUi.properties.codeExpr')">
              <a-textarea v-model:value="localConfigs.code" :rows="6" @change="emitUpdate" />
            </a-form-item>
          </a-form>
        </template>

        <template v-else-if="node.type === 'Loop'">
          <a-form layout="vertical" size="small">
            <a-form-item :label="t('wfUi.properties.loopMode')">
              <a-select v-model:value="localConfigs.mode" @change="emitUpdate">
                <a-select-option value="count">{{ t('wfUi.properties.loopCount') }}</a-select-option>
                <a-select-option value="while">{{ t('wfUi.properties.loopWhile') }}</a-select-option>
                <a-select-option value="forEach">{{ t('wfUi.properties.loopForEach') }}</a-select-option>
              </a-select>
            </a-form-item>
            <a-form-item :label="t('wfUi.properties.maxIter')">
              <a-input-number v-model:value="localConfigs.maxIterations" :min="1" :max="1000" style="width:100%" @change="emitUpdate" />
            </a-form-item>
            <a-form-item :label="t('wfUi.properties.idxVar')">
              <a-input v-model:value="localConfigs.indexVariable" placeholder="loop_index" @change="emitUpdate" />
            </a-form-item>
            <a-form-item v-if="localConfigs.mode === 'while'" :label="t('wfUi.properties.condExpr')">
              <a-textarea v-model:value="localConfigs.condition" :rows="3" :placeholder="t('wfUi.properties.phWhileExpr')" @change="emitUpdate" />
            </a-form-item>
            <template v-if="localConfigs.mode === 'forEach'">
              <a-form-item :label="t('wfUi.properties.collPath')">
                <a-input v-model:value="localConfigs.collectionPath" :placeholder="t('wfUi.properties.phCollPathEg')" @change="emitUpdate" />
              </a-form-item>
              <a-form-item :label="t('wfUi.properties.itemVar')">
                <a-input v-model:value="localConfigs.itemVariable" placeholder="loop_item" @change="emitUpdate" />
              </a-form-item>
              <a-form-item :label="t('wfUi.properties.itemIdxVar')">
                <a-input v-model:value="localConfigs.itemIndexVariable" placeholder="loop_item_index" @change="emitUpdate" />
              </a-form-item>
            </template>
          </a-form>
        </template>

        <template v-else-if="node.type === 'SubWorkflow'">
          <a-form layout="vertical" size="small">
            <a-form-item :label="t('wfUi.properties.subWorkflowId')">
              <a-input-number v-model:value="localConfigs.workflowId" @change="emitUpdate" style="width:100%" />
            </a-form-item>
            <a-form-item :label="t('wfUi.properties.maxDepth')">
              <a-input-number v-model:value="localConfigs.maxDepth" :min="1" :max="10" @change="emitUpdate" style="width:100%" />
            </a-form-item>
            <a-form-item :label="t('wfUi.properties.inheritParent')">
              <a-switch v-model:checked="localConfigs.inheritVariables" @change="emitUpdate" />
            </a-form-item>
            <a-form-item v-if="!localConfigs.inheritVariables" :label="t('wfUi.properties.inputsPath')">
              <a-input v-model:value="localConfigs.inputsVariable" :placeholder="t('wfUi.properties.phSubInputEg')" @change="emitUpdate" />
            </a-form-item>
            <a-form-item :label="t('wfUi.properties.mergeOutputs')">
              <a-switch v-model:checked="localConfigs.mergeOutputs" @change="emitUpdate" />
            </a-form-item>
            <a-form-item :label="t('wfUi.properties.aggVar')">
              <a-input v-model:value="localConfigs.outputKey" placeholder="subworkflow_output" @change="emitUpdate" />
            </a-form-item>
          </a-form>
        </template>

        <template v-else>
          <a-form layout="vertical" size="small">
            <a-form-item :label="t('wfUi.properties.configJson')">
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

      <div class="prop-section">
        <div class="section-title">{{ t('wfUi.properties.inputMap') }}</div>
        <div class="mapping-hint">{{ t('wfUi.properties.mapHint') }}</div>
        <div v-for="(ref, field) in localInputMappings" :key="field" class="mapping-row">
          <a-input :value="field" disabled style="width: 40%" size="small" />
          <span style="color: #9ca3af; padding: 0 8px">→</span>
          <a-input v-model:value="localInputMappings[field]" size="small" style="width: 50%" @change="emitUpdate" />
          <a-button size="small" @click="removeMapping(field)">-</a-button>
        </div>
        <div class="add-mapping">
          <a-input v-model:value="newMappingField" :placeholder="t('wfUi.properties.phField')" size="small" style="width: 40%" />
          <span style="color: #9ca3af; padding: 0 8px">→</span>
          <a-input v-model:value="newMappingRef" :placeholder="t('wfUi.properties.phRef')" size="small" style="width: 40%" />
          <a-button size="small" @click="addMapping">+</a-button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { CloseOutlined } from '@ant-design/icons-vue'

const { t } = useI18n()
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
applyNodeDefaults(props.node.type, localConfigs)

watch(() => props.node, (newNode) => {
  localTitle.value = newNode.title
  Object.assign(localConfigs, newNode.configs)
  applyNodeDefaults(newNode.type, localConfigs)
  Object.keys(localInputMappings).forEach(k => delete localInputMappings[k])
  Object.assign(localInputMappings, newNode.inputMappings)
}, { deep: true })

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

function applyNodeDefaults(nodeType: string, configs: Record<string, unknown>) {
  if (nodeType === 'Selector') {
    configs.condition ??= ''
    return
  }

  if (nodeType === 'Loop') {
    configs.mode ??= 'count'
    configs.maxIterations ??= 10
    configs.indexVariable ??= 'loop_index'
    configs.condition ??= ''
    configs.collectionPath ??= ''
    configs.itemVariable ??= 'loop_item'
    configs.itemIndexVariable ??= 'loop_item_index'
    return
  }

  if (nodeType === 'SubWorkflow') {
    configs.maxDepth ??= 4
    configs.inheritVariables ??= true
    configs.inputsVariable ??= ''
    configs.mergeOutputs ??= true
    configs.outputKey ??= 'subworkflow_output'
  }
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

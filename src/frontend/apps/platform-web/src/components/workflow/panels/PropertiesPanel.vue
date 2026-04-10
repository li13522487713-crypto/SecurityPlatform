<template>
  <div class="properties-panel">
    <div class="panel-header">
      <div class="panel-header-main">
        <span class="node-icon">{{ nodeIcon }}</span>
        <div>
          <div class="panel-title">{{ t("wfUi.properties.title") }}</div>
          <div class="node-type-name">{{ node.type }}</div>
        </div>
      </div>
      <a-button type="text" size="small" @click="$emit('close')">
        <CloseOutlined />
      </a-button>
    </div>

    <div class="panel-body">
      <a-tabs v-model:active-key="activeTab" size="small" class="panel-tabs">
        <a-tab-pane key="common" :tab="t('wfUi.properties.basic')">
          <div class="prop-section">
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
            <div class="section-title">{{ t("wfUi.properties.nodeConfig") }}</div>
            <component :is="activeNodeForm" :configs="localConfigs" :node-type="String(node.type)" @change="emitUpdate" />
          </div>
        </a-tab-pane>

        <a-tab-pane key="advanced" tab="高级设置">
          <div class="prop-section">
            <a-form layout="vertical" size="small">
              <a-form-item :label="t('wfUi.properties.configJson')">
                <a-textarea
                  :value="configsJson"
                  :rows="10"
                  style="font-family: monospace; font-size: 12px"
                  @change="handleRawConfigsChange"
                />
              </a-form-item>
            </a-form>
          </div>

          <div class="prop-section">
            <div class="section-title">{{ t('wfUi.properties.inputMap') }}</div>
            <div class="mapping-hint">{{ t('wfUi.properties.mapHint') }}</div>
            <div v-for="(mappingRef, field) in localInputMappings" :key="field" class="mapping-row">
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
        </a-tab-pane>
      </a-tabs>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, watch, markRaw } from 'vue'
import { useI18n } from 'vue-i18n'
import { CloseOutlined } from '@ant-design/icons-vue'
import type { Component } from 'vue'
import type { NodeSchema, NodeTypeMetadata } from '@/types/workflow-v2'
import StartNodeForm from '@/components/workflow/forms/StartNodeForm.vue'
import EndNodeForm from '@/components/workflow/forms/EndNodeForm.vue'
import SelectorNodeForm from '@/components/workflow/forms/SelectorNodeForm.vue'
import LoopNodeForm from '@/components/workflow/forms/LoopNodeForm.vue'
import BatchNodeForm from '@/components/workflow/forms/BatchNodeForm.vue'
import BreakNodeForm from '@/components/workflow/forms/BreakNodeForm.vue'
import ContinueNodeForm from '@/components/workflow/forms/ContinueNodeForm.vue'
import LlmNodeForm from '@/components/workflow/forms/LlmNodeForm.vue'
import IntentDetectorNodeForm from '@/components/workflow/forms/IntentDetectorNodeForm.vue'
import QuestionAnswerNodeForm from '@/components/workflow/forms/QuestionAnswerNodeForm.vue'
import CodeNodeForm from '@/components/workflow/forms/CodeNodeForm.vue'
import TextProcessorNodeForm from '@/components/workflow/forms/TextProcessorNodeForm.vue'
import JsonSerializationNodeForm from '@/components/workflow/forms/JsonSerializationNodeForm.vue'
import JsonDeserializationNodeForm from '@/components/workflow/forms/JsonDeserializationNodeForm.vue'
import VariableAggregatorNodeForm from '@/components/workflow/forms/VariableAggregatorNodeForm.vue'
import AssignVariableNodeForm from '@/components/workflow/forms/AssignVariableNodeForm.vue'
import PluginNodeForm from '@/components/workflow/forms/PluginNodeForm.vue'
import HttpRequestNodeForm from '@/components/workflow/forms/HttpRequestNodeForm.vue'
import SubWorkflowNodeForm from '@/components/workflow/forms/SubWorkflowNodeForm.vue'
import KnowledgeSearchNodeForm from '@/components/workflow/forms/KnowledgeSearchNodeForm.vue'
import KnowledgeWriteNodeForm from '@/components/workflow/forms/KnowledgeWriteNodeForm.vue'
import LtmNodeForm from '@/components/workflow/forms/LtmNodeForm.vue'
import DatabaseQueryNodeForm from '@/components/workflow/forms/DatabaseQueryNodeForm.vue'
import DatabaseInsertNodeForm from '@/components/workflow/forms/DatabaseInsertNodeForm.vue'
import DatabaseUpdateNodeForm from '@/components/workflow/forms/DatabaseUpdateNodeForm.vue'
import DatabaseDeleteNodeForm from '@/components/workflow/forms/DatabaseDeleteNodeForm.vue'
import DatabaseCustomSqlNodeForm from '@/components/workflow/forms/DatabaseCustomSqlNodeForm.vue'
import ConversationNodeForm from '@/components/workflow/forms/ConversationNodeForm.vue'
import ConversationHistoryNodeForm from '@/components/workflow/forms/ConversationHistoryNodeForm.vue'
import MessageNodeForm from '@/components/workflow/forms/MessageNodeForm.vue'
import IoNodeForm from '@/components/workflow/forms/IoNodeForm.vue'
import GenericNodeForm from '@/components/workflow/forms/GenericNodeForm.vue'

const { t } = useI18n()

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
const activeTab = ref<'common' | 'advanced'>('common')

watch(() => props.node, (newNode) => {
  localTitle.value = newNode.title
  Object.assign(localConfigs, newNode.configs)
  applyNodeDefaults(newNode.type, localConfigs)
  Object.keys(localInputMappings).forEach(k => delete localInputMappings[k])
  Object.assign(localInputMappings, newNode.inputMappings)
}, { deep: true })

const configsJson = computed(() => JSON.stringify(localConfigs, null, 2))
const activeNodeForm = computed<Component>(() => {
  const type = props.node.type
  if (type === 'Entry' || type === 'Start') {
    return markRaw(StartNodeForm)
  }
  if (type === 'Exit' || type === 'End') {
    return markRaw(EndNodeForm)
  }
  if (type === 'Selector' || type === 'If') {
    return markRaw(SelectorNodeForm)
  }
  if (type === 'Loop') {
    return markRaw(LoopNodeForm)
  }
  if (type === 'Batch') {
    return markRaw(BatchNodeForm)
  }
  if (type === 'Break') {
    return markRaw(BreakNodeForm)
  }
  if (type === 'Continue') {
    return markRaw(ContinueNodeForm)
  }
  if (type === 'Llm' || type === 'LLM') {
    return markRaw(LlmNodeForm)
  }
  if (type === 'IntentDetector') {
    return markRaw(IntentDetectorNodeForm)
  }
  if (type === 'QuestionAnswer') {
    return markRaw(QuestionAnswerNodeForm)
  }
  if (type === 'CodeRunner') {
    return markRaw(CodeNodeForm)
  }
  if (type === 'TextProcessor') {
    return markRaw(TextProcessorNodeForm)
  }
  if (type === 'JsonSerialization') {
    return markRaw(JsonSerializationNodeForm)
  }
  if (type === 'JsonDeserialization') {
    return markRaw(JsonDeserializationNodeForm)
  }
  if (type === 'VariableAggregator') {
    return markRaw(VariableAggregatorNodeForm)
  }
  if (type === 'AssignVariable' || type === 'VariableAssignerWithinLoop') {
    return markRaw(AssignVariableNodeForm)
  }
  if (type === 'Plugin') {
    return markRaw(PluginNodeForm)
  }
  if (type === 'HttpRequester') {
    return markRaw(HttpRequestNodeForm)
  }
  if (type === 'SubWorkflow') {
    return markRaw(SubWorkflowNodeForm)
  }
  if (type === 'KnowledgeRetriever') {
    return markRaw(KnowledgeSearchNodeForm)
  }
  if (type === 'KnowledgeIndexer') {
    return markRaw(KnowledgeWriteNodeForm)
  }
  if (type === 'Ltm') {
    return markRaw(LtmNodeForm)
  }
  if (type === 'DatabaseQuery') {
    return markRaw(DatabaseQueryNodeForm)
  }
  if (type === 'DatabaseInsert') {
    return markRaw(DatabaseInsertNodeForm)
  }
  if (type === 'DatabaseUpdate') {
    return markRaw(DatabaseUpdateNodeForm)
  }
  if (type === 'DatabaseDelete') {
    return markRaw(DatabaseDeleteNodeForm)
  }
  if (type === 'DatabaseCustomSql') {
    return markRaw(DatabaseCustomSqlNodeForm)
  }
  if (type === 'CreateConversation' || type === 'ConversationList' || type === 'ConversationUpdate' || type === 'ConversationDelete') {
    return markRaw(ConversationNodeForm)
  }
  if (type === 'ConversationHistory' || type === 'ClearConversationHistory') {
    return markRaw(ConversationHistoryNodeForm)
  }
  if (type === 'MessageList' || type === 'CreateMessage' || type === 'EditMessage' || type === 'DeleteMessage') {
    return markRaw(MessageNodeForm)
  }
  if (type === 'OutputEmitter' || type === 'InputReceiver') {
    return markRaw(IoNodeForm)
  }
  return markRaw(GenericNodeForm)
})

const nodeIcon = computed(() => {
  const type = String(props.node.type)
  const iconMap: Record<string, string> = {
    Entry: '▶',
    Exit: '⏹',
    Start: '▶',
    End: '⏹'
  }
  return iconMap[type] ?? '□'
})

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
  if (nodeType === 'Entry' || nodeType === 'Start') {
    configs.variables ??= []
    configs.autoSaveHistory ??= true
    return
  }

  if (nodeType === 'Exit' || nodeType === 'End') {
    configs.terminationMode ??= 'returnVariables'
    configs.outputMappings ??= []
    configs.templateText ??= ''
    configs.streamOutput ??= false
    return
  }

  if (nodeType === 'Selector' || nodeType === 'If') {
    configs.matchMode ??= 'all'
    configs.conditions ??= []
    configs.fallbackExpression ??= ''
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

  if (nodeType === 'Batch') {
    configs.collectionPath ??= ''
    configs.parallelism ??= 4
    configs.itemTimeoutMs ??= 0
    configs.onError ??= 'continue'
    configs.outputKey ??= 'batch_output'
    return
  }

  if (nodeType === 'Break') {
    configs.signal ??= 'loop_break'
    configs.reason ??= ''
    configs.outputKey ??= 'loop_control_signal'
    return
  }

  if (nodeType === 'Continue') {
    configs.signal ??= 'loop_continue'
    configs.reason ??= ''
    configs.outputKey ??= 'loop_control_signal'
    return
  }

  if (nodeType === 'Llm' || nodeType === 'LLM') {
    configs.provider ??= 'openai'
    configs.model ??= 'gpt-5.4-medium'
    configs.systemPrompt ??= ''
    configs.prompt ??= '{{input.message}}'
    configs.temperature ??= 0.7
    configs.maxTokens ??= 2048
    configs.stream ??= true
    configs.outputKey ??= 'llm_output'
    return
  }

  if (nodeType === 'IntentDetector') {
    configs.intents ??= []
    configs.inputPath ??= 'input.message'
    configs.threshold ??= 0.6
    configs.outputKey ??= 'intent_result'
    return
  }

  if (nodeType === 'QuestionAnswer') {
    configs.questionTemplate ??= '请补充必要信息。'
    configs.timeoutSeconds ??= 300
    configs.answerKey ??= 'qa_answer'
    configs.allowEmpty ??= false
    return
  }

  if (nodeType === 'CodeRunner') {
    configs.language ??= 'javascript'
    configs.code ??= 'return input;'
    configs.outputKey ??= 'code_output'
    return
  }

  if (nodeType === 'TextProcessor') {
    configs.mode ??= 'template'
    configs.inputPath ??= 'input.text'
    configs.templateText ??= '{{input.text}}'
    configs.outputKey ??= 'text_output'
    return
  }

  if (nodeType === 'JsonSerialization') {
    configs.direction ??= 'serialize'
    configs.inputPath ??= 'input.payload'
    configs.pretty ??= true
    configs.outputKey ??= 'json_output'
    return
  }

  if (nodeType === 'JsonDeserialization') {
    configs.direction ??= 'deserialize'
    configs.inputPath ??= 'input.payload'
    configs.pretty ??= true
    configs.outputKey ??= 'json_output'
    return
  }

  if (nodeType === 'VariableAggregator') {
    configs.mode ??= 'object'
    configs.sourcePaths ??= []
    configs.outputKey ??= 'aggregated_output'
    return
  }

  if (nodeType === 'AssignVariable' || nodeType === 'VariableAssignerWithinLoop') {
    configs.variableName ??= ''
    configs.valueExpression ??= ''
    configs.scope ??= nodeType === 'VariableAssignerWithinLoop' ? 'loop' : 'workflow'
    configs.overwrite ??= true
    return
  }

  if (nodeType === 'Plugin') {
    configs.pluginKey ??= ''
    configs.method ??= 'execute'
    configs.inputJson ??= '{}'
    configs.timeoutMs ??= 30000
    configs.outputKey ??= 'plugin_output'
    return
  }

  if (nodeType === 'HttpRequester') {
    configs.method ??= 'GET'
    configs.url ??= ''
    configs.headersJson ??= '{}'
    configs.body ??= ''
    configs.timeoutMs ??= 15000
    configs.outputKey ??= 'http_output'
    return
  }

  if (nodeType === 'SubWorkflow') {
    configs.subWorkflowId ??= ''
    configs.inheritVariables ??= true
    configs.inputsVariable ??= 'input'
    configs.maxDepth ??= 4
    configs.outputKey ??= 'subworkflow_output'
    return
  }

  if (nodeType === 'KnowledgeRetriever') {
    configs.datasetId ??= ''
    configs.queryPath ??= 'input.query'
    configs.topK ??= 5
    configs.minScore ??= 0.5
    configs.outputKey ??= 'knowledge_hits'
    return
  }

  if (nodeType === 'KnowledgeIndexer') {
    configs.datasetId ??= ''
    configs.title ??= ''
    configs.contentPath ??= 'input.content'
    configs.chunkSize ??= 800
    configs.outputKey ??= 'knowledge_write_result'
    return
  }

  if (nodeType === 'Ltm') {
    configs.action ??= 'read'
    configs.namespace ??= 'default'
    configs.keyName ??= ''
    configs.valuePath ??= 'input.value'
    configs.outputKey ??= 'ltm_result'
    return
  }

  if (nodeType === 'DatabaseQuery') {
    configs.databaseId ??= ''
    configs.tableName ??= ''
    configs.whereJson ??= '{}'
    configs.outputKey ??= 'db_output'
    return
  }

  if (nodeType === 'DatabaseInsert') {
    configs.databaseId ??= ''
    configs.tableName ??= ''
    configs.payloadJson ??= '{}'
    configs.outputKey ??= 'db_output'
    return
  }

  if (nodeType === 'DatabaseUpdate') {
    configs.databaseId ??= ''
    configs.tableName ??= ''
    configs.whereJson ??= '{}'
    configs.payloadJson ??= '{}'
    configs.outputKey ??= 'db_output'
    return
  }

  if (nodeType === 'DatabaseDelete') {
    configs.databaseId ??= ''
    configs.tableName ??= ''
    configs.whereJson ??= '{}'
    configs.outputKey ??= 'db_output'
    return
  }

  if (nodeType === 'DatabaseCustomSql') {
    configs.databaseId ??= ''
    configs.tableName ??= ''
    configs.sql ??= ''
    configs.outputKey ??= 'db_output'
    return
  }

  if (nodeType === 'CreateConversation' || nodeType === 'ConversationList' || nodeType === 'ConversationUpdate' || nodeType === 'ConversationDelete') {
    configs.conversationId ??= ''
    configs.userId ??= ''
    configs.title ??= ''
    configs.agentId ??= ''
    configs.outputKey ??= 'conversation_output'
    return
  }

  if (nodeType === 'ConversationHistory' || nodeType === 'ClearConversationHistory') {
    configs.conversationId ??= ''
    configs.limit ??= 20
    configs.outputKey ??= 'conversation_history'
    return
  }

  if (nodeType === 'MessageList' || nodeType === 'CreateMessage' || nodeType === 'EditMessage' || nodeType === 'DeleteMessage') {
    configs.conversationId ??= ''
    configs.messageId ??= ''
    configs.content ??= ''
    configs.pageSize ??= 20
    configs.outputKey ??= 'message_output'
    return
  }

  if (nodeType === 'OutputEmitter' || nodeType === 'InputReceiver') {
    configs.templateText ??= ''
    configs.prompt ??= '请继续输入。'
    configs.timeoutSeconds ??= 300
    configs.outputKey ??= 'io_output'
  }
}
</script>

<style scoped>
.properties-panel {
  width: clamp(280px, 24vw, 340px);
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

.panel-header-main {
  display: flex;
  align-items: center;
  gap: 10px;
}

.node-icon {
  width: 24px;
  height: 24px;
  border-radius: 6px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: #0d1117;
  border: 1px solid #30363d;
}

.panel-title {
  font-weight: 600;
  color: #e6edf3;
}

.node-type-name {
  margin-top: 2px;
  color: #8f99a6;
  font-size: 12px;
}

.panel-body {
  flex: 1;
  overflow-y: auto;
  padding: 0 8px 8px;
  scrollbar-width: thin;
  scrollbar-color: #3b4755 #121a23;
}

.panel-tabs {
  height: 100%;
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

.panel-body::-webkit-scrollbar {
  width: 10px;
}

.panel-body::-webkit-scrollbar-track {
  background: #121a23;
}

.panel-body::-webkit-scrollbar-thumb {
  background: #3b4755;
  border-radius: 999px;
  border: 2px solid #121a23;
}
</style>

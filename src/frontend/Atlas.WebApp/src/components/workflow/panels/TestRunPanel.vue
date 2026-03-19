<template>
  <div class="test-run-panel">
    <div class="panel-header">
      <div class="panel-tabs">
        <button :class="['tab', activeTab === 'run' ? 'active' : '']" @click="activeTab = 'run'">测试运行</button>
        <button :class="['tab', activeTab === 'trace' ? 'active' : '']" @click="activeTab = 'trace'">执行追踪</button>
        <button :class="['tab', activeTab === 'versions' ? 'active' : '']" @click="activeTab = 'versions'">版本历史</button>
      </div>
      <a-button type="text" size="small" @click="$emit('close')">
        <CloseOutlined />
      </a-button>
    </div>

    <!-- 测试运行面板 -->
    <div v-show="activeTab === 'run'" class="panel-body">
      <div class="run-input-section">
        <div class="section-title">输入参数</div>
        <a-textarea
          v-model:value="inputJson"
          :rows="4"
          placeholder='{"userInput": "Hello, World!"}'
          style="font-family: monospace; font-size: 12px"
        />
        <div style="margin-top: 8px; display: flex; gap: 8px;">
          <a-button
            type="primary"
            size="small"
            :loading="isRunning"
            @click="startRun('sync')"
          >同步执行</a-button>
          <a-button
            size="small"
            :loading="isStreaming"
            @click="startRun('stream')"
          >流式执行</a-button>
          <a-button
            v-if="isRunning || isStreaming"
            size="small"
            danger
            @click="stopRun"
          >取消</a-button>
        </div>
      </div>

      <!-- 执行日志 -->
      <div class="run-log-section">
        <div class="section-title" style="display: flex; justify-content: space-between;">
          <span>执行日志</span>
          <a-button type="text" size="small" @click="clearLog">清空</a-button>
        </div>
        <div class="log-container" ref="logContainer">
          <div
            v-for="(event, idx) in executionEvents"
            :key="idx"
            :class="['log-event', `event-${event.type}`]"
          >
            <span class="event-time">{{ event.time }}</span>
            <span :class="['event-type', `type-${event.type}`]">{{ EVENT_LABELS[event.type] || event.type }}</span>
            <span class="event-msg">{{ event.message }}</span>
          </div>
          <div v-if="executionEvents.length === 0" class="log-empty">尚无执行日志</div>
        </div>
      </div>

      <!-- 最终输出 -->
      <div v-if="finalOutput" class="run-output-section">
        <div class="section-title">最终输出</div>
        <pre class="output-pre">{{ finalOutput }}</pre>
      </div>

      <!-- 问题回答中断 -->
      <div v-if="interruptQuestion" class="interrupt-section">
        <div class="section-title" style="color: #faad14;">⚠️ 等待用户输入</div>
        <p class="interrupt-question">{{ interruptQuestion }}</p>
        <a-input v-model:value="interruptAnswer" placeholder="输入你的回答..." />
        <a-button
          type="primary"
          size="small"
          style="margin-top: 8px"
          @click="submitAnswer"
        >提交回答</a-button>
      </div>
    </div>

    <!-- 执行追踪面板 -->
    <div v-show="activeTab === 'trace'" class="panel-body">
      <div v-if="!currentExecution" class="trace-empty">
        <p>运行工作流后查看执行追踪</p>
      </div>
      <div v-else>
        <div class="trace-summary">
          <div class="trace-stat">
            <div class="stat-label">总耗时</div>
            <div class="stat-value">{{ currentExecution.costMs }}ms</div>
          </div>
          <div class="trace-stat">
            <div class="stat-label">状态</div>
            <div :class="['stat-value', `status-${currentExecution.status}`]">{{ currentExecution.status }}</div>
          </div>
        </div>

        <div class="trace-nodes">
          <div class="section-title">节点执行详情</div>
          <div
            v-for="ne in currentExecution.nodeExecutions"
            :key="ne.nodeKey"
            :class="['trace-node', `status-${STATUS_LABELS[ne.status] ?? 'pending'}`]"
          >
            <div class="trace-node-header">
              <span :class="['trace-status-dot', `dot-${STATUS_LABELS[ne.status] ?? 'pending'}`]"></span>
              <span class="trace-node-name">{{ ne.nodeKey }}</span>
              <span class="trace-cost">{{ ne.durationMs ?? 0 }}ms</span>
            </div>
            <div v-if="ne.errorMessage" class="trace-error">
              {{ ne.errorMessage }}
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 版本历史面板 -->
    <div v-show="activeTab === 'versions'" class="panel-body">
      <div style="padding: 12px">
        <a-button type="primary" size="small" block @click="$emit('publish')">
          发布新版本
        </a-button>
      </div>
      <div v-if="versions.length === 0" class="trace-empty">
        <p>尚未发布任何版本</p>
      </div>
      <div v-for="ver in versions" :key="ver.versionNumber" class="version-item">
        <div class="version-header">
          <span class="version-tag">v{{ ver.versionNumber }}</span>
          <span class="version-time">{{ formatDate(ver.publishedAt) }}</span>
        </div>
        <div class="version-desc">{{ ver.changeLog || '无描述' }}</div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, nextTick } from 'vue'
import { CloseOutlined } from '@ant-design/icons-vue'
import type { WorkflowVersionItem, NodeExecutionItem, ExecutionStatus } from '@/types/workflow-v2'
import type { StreamRunHandle } from '@/services/api-workflow-v2'

const STATUS_LABELS: Record<ExecutionStatus, string> = {
  0: 'pending', 1: 'running', 2: 'success', 3: 'failed', 4: 'cancelled', 5: 'interrupted',
}
import { workflowV2Api } from '@/services/api-workflow-v2'

const props = defineProps<{
  workflowId: number
  versions: WorkflowVersionItem[]
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'publish'): void
  (e: 'node-status-update', nodeKey: string, status: string): void
}>()

const activeTab = ref<'run' | 'trace' | 'versions'>('run')
const inputJson = ref('{}')
const isRunning = ref(false)
const isStreaming = ref(false)
const logContainer = ref<HTMLElement | null>(null)
const finalOutput = ref('')
const interruptQuestion = ref('')
const interruptAnswer = ref('')
const currentExecutionId = ref<string | null>(null)

interface ExecutionEvent {
  type: string
  time: string
  message: string
}

const executionEvents = ref<ExecutionEvent[]>([])

const EVENT_LABELS: Record<string, string> = {
  execution_start: '执行开始',
  node_start: '节点开始',
  node_output: '节点输出',
  node_complete: '节点完成',
  node_failed: '节点失败',
  llm_output: '模型输出',
  execution_complete: '执行完成',
  execution_failed: '执行失败',
  execution_cancelled: '执行取消',
  execution_interrupted: '执行中断',
  workflow_error: '流程错误',
}

interface ExecutionSummary {
  costMs: number
  status: string
  nodeExecutions: NodeExecutionItem[]
}

const currentExecution = ref<ExecutionSummary | null>(null)

let streamHandle: StreamRunHandle | null = null

function addEvent(type: string, message: string) {
  executionEvents.value.push({
    type,
    time: new Date().toLocaleTimeString(),
    message,
  })
  nextTick(() => {
    if (logContainer.value) {
      logContainer.value.scrollTop = logContainer.value.scrollHeight
    }
  })
}

function clearLog() {
  executionEvents.value = []
  finalOutput.value = ''
  interruptQuestion.value = ''
  currentExecution.value = null
}

async function startRun(mode: 'sync' | 'stream') {
  let inputs: Record<string, unknown> = {}
  try {
    inputs = JSON.parse(inputJson.value)
  } catch {
    addEvent('error', '输入 JSON 格式错误')
    return
  }

  clearLog()
  finalOutput.value = ''
  interruptQuestion.value = ''

    if (mode === 'sync') {
    isRunning.value = true
    try {
      addEvent('execution_start', '同步执行开始...')
      const runRes = await workflowV2Api.runSync(props.workflowId, { inputs })
      if (!runRes.success || !runRes.data) {
        addEvent('workflow_error', runRes.message ?? '执行失败')
        return
      }
      const result = runRes.data
      currentExecutionId.value = result.executionId
      const processRes = await workflowV2Api.getProcess(Number(result.executionId))
      if (processRes.success && processRes.data) {
        const proc = processRes.data
        const nodeExecs = proc.nodeExecutions ?? []
        const totalCostMs = calcExecutionCostMs(proc.startedAt, proc.completedAt)
        currentExecution.value = {
          costMs: totalCostMs,
          status: STATUS_LABELS[proc.status] ?? String(proc.status),
          nodeExecutions: nodeExecs,
        }
        for (const ne of nodeExecs) {
          emit('node-status-update', ne.nodeKey, STATUS_LABELS[ne.status] ?? 'success')
        }
        finalOutput.value = proc.outputsJson ?? '{}'
        addEvent('execution_complete', `执行完成，耗时 ${totalCostMs}ms`)
      }
    } catch (err: unknown) {
      addEvent('workflow_error', String(err))
    } finally {
      isRunning.value = false
    }
  } else {
    isStreaming.value = true
    addEvent('execution_start', '流式执行开始...')
    try {
      streamHandle = workflowV2Api.runStream(
        props.workflowId,
        { inputs },
        {
          onExecutionStarted: (ev) => {
            currentExecutionId.value = ev.executionId
            addEvent('execution_start', `执行实例 ${ev.executionId} 已创建`)
          },
          onNodeStarted: ev => {
            addEvent('node_start', `[${ev.nodeKey}] 开始执行`)
            emit('node-status-update', ev.nodeKey, 'running')
          },
          onNodeOutput: ev => {
            addEvent('node_output', `[${ev.nodeKey}] 输出: ${JSON.stringify(ev.outputs)}`)
          },
          onNodeCompleted: ev => {
            addEvent('node_complete', `[${ev.nodeKey}] 完成，耗时 ${ev.durationMs}ms`)
            emit('node-status-update', ev.nodeKey, 'success')
          },
          onNodeFailed: ev => {
            addEvent('node_failed', `[${ev.nodeKey}] 失败: ${ev.errorMessage}`)
            emit('node-status-update', ev.nodeKey, 'failed')
          },
          onLlmOutput: content => {
            addEvent('llm_output', content)
          },
          onExecutionCompleted: async ev => {
            addEvent('execution_complete', `执行完成: ${ev.executionId}`)
            if (ev.outputsJson) {
              finalOutput.value = ev.outputsJson
            }
            await refreshExecutionDetail(ev.executionId)
            isStreaming.value = false
          },
          onExecutionFailed: async ev => {
            addEvent('execution_failed', ev.errorMessage || '执行失败')
            if (currentExecutionId.value) {
              await refreshExecutionDetail(currentExecutionId.value)
            }
            isStreaming.value = false
          },
          onExecutionCancelled: ev => {
            addEvent('execution_cancelled', `执行已取消: ${ev.executionId}`)
            isStreaming.value = false
          },
          onExecutionInterrupted: ev => {
            addEvent('execution_interrupted', `流程中断: ${ev.interruptType}`)
            interruptQuestion.value = `执行在节点 ${ev.nodeKey ?? '-'} 发生中断（${ev.interruptType}）`
            isStreaming.value = false
          },
          onError: err => {
            addEvent('workflow_error', String(err))
            isStreaming.value = false
          },
        }
      )
      await streamHandle.done
      streamHandle = null
    } catch (err: unknown) {
      addEvent('workflow_error', String(err))
      isStreaming.value = false
    }
  }
}

function stopRun() {
  if (streamHandle) {
    streamHandle.abort()
    streamHandle = null
  }
  isRunning.value = false
  isStreaming.value = false
  addEvent('cancelled', '执行已取消')
}

async function submitAnswer() {
  if (!currentExecutionId.value) return
  try {
    await workflowV2Api.resume(Number(currentExecutionId.value), { data: { answer: interruptAnswer.value } })
    interruptQuestion.value = ''
    interruptAnswer.value = ''
    addEvent('resumed', '已提交回答，继续执行...')
  } catch (err: unknown) {
    addEvent('error', String(err))
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString()
}

function calcExecutionCostMs(startedAt: string, completedAt?: string) {
  if (!completedAt) {
    return 0
  }

  const startTs = Date.parse(startedAt)
  const endTs = Date.parse(completedAt)
  if (Number.isNaN(startTs) || Number.isNaN(endTs)) {
    return 0
  }

  return Math.max(0, endTs - startTs)
}

async function refreshExecutionDetail(executionId: string) {
  const processRes = await workflowV2Api.getProcess(Number(executionId))
  if (!processRes.success || !processRes.data) {
    return
  }

  const proc = processRes.data
  const nodeExecs = proc.nodeExecutions ?? []
  const totalCostMs = calcExecutionCostMs(proc.startedAt, proc.completedAt)
  currentExecution.value = {
    costMs: totalCostMs,
    status: STATUS_LABELS[proc.status] ?? String(proc.status),
    nodeExecutions: nodeExecs,
  }

  for (const ne of nodeExecs) {
    emit('node-status-update', ne.nodeKey, STATUS_LABELS[ne.status] ?? 'success')
  }

  finalOutput.value = proc.outputsJson ?? '{}'
}
</script>

<style scoped>
.test-run-panel {
  width: 360px;
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
  border-bottom: 1px solid #30363d;
  padding-right: 8px;
}

.panel-tabs {
  display: flex;
}

.tab {
  padding: 10px 16px;
  font-size: 13px;
  background: none;
  border: none;
  border-bottom: 2px solid transparent;
  color: #7d8590;
  cursor: pointer;
  transition: color 0.2s, border-color 0.2s;
}

.tab.active {
  color: #58a6ff;
  border-bottom-color: #58a6ff;
}

.panel-body {
  flex: 1;
  overflow-y: auto;
  padding: 12px;
}

.section-title {
  font-size: 12px;
  font-weight: 700;
  color: #7d8590;
  text-transform: uppercase;
  margin-bottom: 8px;
}

.run-input-section,
.run-log-section,
.run-output-section,
.interrupt-section {
  margin-bottom: 16px;
}

.log-container {
  background: #0d1117;
  border: 1px solid #21262d;
  border-radius: 6px;
  padding: 8px;
  max-height: 200px;
  overflow-y: auto;
  font-size: 12px;
}

.log-event {
  display: flex;
  gap: 8px;
  margin-bottom: 4px;
  line-height: 1.4;
}

.event-time {
  color: #7d8590;
  font-family: monospace;
  flex-shrink: 0;
}

.event-type {
  font-weight: 700;
  flex-shrink: 0;
  min-width: 60px;
}

.type-node_start { color: #58a6ff; }
.type-node_output { color: #58a6ff; }
.type-node_complete { color: #52c41a; }
.type-node_failed { color: #ff4d4f; }
.type-execution_failed,
.type-workflow_error { color: #ff4d4f; }
.type-execution_complete { color: #52c41a; }
.type-execution_interrupted { color: #faad14; }
.type-execution_cancelled { color: #7d8590; }
.type-llm_output { color: #58a6ff; }
.type-cancelled { color: #7d8590; }

.event-msg { color: #e6edf3; }
.log-empty { color: #7d8590; text-align: center; padding: 16px; }

.output-pre {
  background: #0d1117;
  border: 1px solid #21262d;
  border-radius: 6px;
  padding: 8px;
  font-size: 12px;
  color: #52c41a;
  overflow: auto;
  max-height: 160px;
  white-space: pre-wrap;
}

.interrupt-section {
  background: #1c1700;
  border: 1px solid #faad14;
  border-radius: 8px;
  padding: 12px;
}

.interrupt-question {
  color: #e6edf3;
  margin-bottom: 8px;
}

/* Trace */
.trace-empty {
  text-align: center;
  color: #7d8590;
  padding: 32px;
}

.trace-summary {
  display: flex;
  gap: 16px;
  margin-bottom: 16px;
}

.trace-stat {
  flex: 1;
  background: #0d1117;
  border-radius: 6px;
  padding: 8px;
  text-align: center;
}

.stat-label { font-size: 11px; color: #7d8590; }
.stat-value { font-size: 18px; font-weight: 700; color: #e6edf3; margin-top: 4px; }

.trace-node {
  background: #0d1117;
  border: 1px solid #21262d;
  border-radius: 6px;
  padding: 8px;
  margin-bottom: 8px;
}

.trace-node-header {
  display: flex;
  align-items: center;
  gap: 8px;
}

.trace-status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.dot-success { background: #52c41a; }
.dot-failed { background: #ff4d4f; }
.dot-running { background: #1677ff; }
.dot-pending { background: #7d8590; }
.dot-cancelled { background: #7d8590; }
.dot-interrupted { background: #faad14; }

.trace-node-name { flex: 1; font-size: 13px; color: #e6edf3; }
.trace-cost { font-size: 11px; color: #7d8590; }
.trace-tokens { font-size: 11px; color: #58a6ff; margin-top: 4px; }
.trace-error { font-size: 11px; color: #ff4d4f; margin-top: 4px; }

/* Versions */
.version-item {
  background: #0d1117;
  border: 1px solid #21262d;
  border-radius: 6px;
  padding: 12px;
  margin-bottom: 8px;
}

.version-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 4px;
}

.version-tag {
  background: #1f3a5f;
  color: #58a6ff;
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 600;
}

.version-time { font-size: 11px; color: #7d8590; }
.version-desc { font-size: 12px; color: #e6edf3; margin-bottom: 4px; }
.version-stats { font-size: 11px; color: #7d8590; }
</style>

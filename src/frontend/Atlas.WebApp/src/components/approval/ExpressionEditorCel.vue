<template>
  <div class="expr-editor">
    <a-textarea
      :value="modelValue"
      :rows="rows"
      :placeholder="resolvedPlaceholder"
      @update:value="handleInput"
    />
    <div class="expr-toolbar">
      <a-space size="small" wrap>
        <a-tag
          v-for="v in displayVariables"
          :key="v"
          color="blue"
          class="expr-var-tag"
          @click="insertVariable(v)"
        >{{ v }}</a-tag>
        <a-tooltip v-if="serverVariables.length > 0" :title="t('approvalDesigner.celTooltipVars')">
          <a-tag
            v-for="sv in serverVariables"
            :key="`sv-${sv}`"
            color="purple"
            class="expr-var-tag"
            @click="insertVariable(sv)"
          >{{ sv }}</a-tag>
        </a-tooltip>
      </a-space>
      <a-space size="small">
        <a-button
          v-if="enableTryRun"
          size="small"
          :loading="tryRunning"
          @click="openTryRun"
        >{{ t('approvalDesigner.celBtnTryRun') }}</a-button>
        <span class="expr-status" :class="{ invalid: !isValid }">{{ statusText }}</span>
      </a-space>
    </div>

    <a-modal
      v-model:open="tryRunVisible"
      :title="t('approvalDesigner.celModalTryTitle')"
      width="600px"
      :footer="null"
    >
      <div class="try-run-body">
        <div class="try-run-section">
          <div class="try-run-label">{{ t('approvalDesigner.celTryContextLabel') }}</div>
          <a-textarea
            v-model:value="tryRunContext"
            :rows="5"
            placeholder='{"form.amount": "1500", "form.status": "approved"}'
          />
        </div>
        <a-button type="primary" :loading="tryRunning" style="margin-top: 12px" @click="runTryRun">
          {{ t('approvalDesigner.celBtnExecute') }}
        </a-button>
        <div v-if="tryRunResult !== null" class="try-run-result">
          <a-divider />
          <div v-if="tryRunResult.success">
            <a-tag color="green">{{ t('approvalDesigner.celTrySuccess') }}</a-tag>
            <span class="try-run-value">{{ t('approvalDesigner.celTryResultLabel') }}{{ tryRunResult.resultValue }}</span>
            <span v-if="tryRunResult.resultBool !== null" class="try-run-bool">
              {{ t('approvalDesigner.celTryBoolLabel', { value: tryRunResult.resultBool }) }}
            </span>
          </div>
          <div v-else>
            <a-tag color="red">{{ t('approvalDesigner.celTryFail') }}</a-tag>
            <span class="try-run-error">{{ tryRunResult.error }}</span>
          </div>
        </div>
      </div>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { validateExpression, evaluateExpression } from '@/services/api-expression'
import type { ExpressionEvaluateResponse } from '@/services/api-expression'

const { t } = useI18n()

const props = withDefaults(defineProps<{
  modelValue: string
  placeholder?: string
  rows?: number
  variables?: string[]
  enableTryRun?: boolean
}>(), {
  placeholder: undefined,
  rows: 4,
  variables: () => ['form', 'user', 'record', 'page', 'tenant'],
  enableTryRun: true,
})

const resolvedPlaceholder = computed(() => props.placeholder ?? t('approvalDesigner.celPhExpr'))

const emit = defineEmits<{
  'update:modelValue': [value: string]
  validate: [valid: boolean, message: string]
}>()

const forbiddenPatterns = [/javascript:/i, /eval\(/i, /function\(/i, /<script/i]

const clientValidate = (value: string) => {
  const expr = value.trim()
  if (!expr) return { valid: true, message: t('approvalDesigner.celValidateEmpty') }
  if (expr.length > 4096) return { valid: false, message: t('approvalDesigner.celValidateTooLong') }
  if (forbiddenPatterns.some((p) => p.test(expr))) return { valid: false, message: t('approvalDesigner.celValidateUnsafe') }
  let balance = 0
  for (const ch of expr) {
    if (ch === '(') balance += 1
    if (ch === ')') balance -= 1
    if (balance < 0) return { valid: false, message: t('approvalDesigner.celValidateParen') }
  }
  if (balance !== 0) return { valid: false, message: t('approvalDesigner.celValidateParen') }
  return { valid: true, message: t('approvalDesigner.celValidateOkLocal') }
}

const serverValidationMessage = ref('')
const serverValid = ref(true)
const serverVariables = ref<string[]>([])
let validateTimer: ReturnType<typeof setTimeout> | null = null

const runServerValidate = async (value: string) => {
  if (!value.trim()) {
    serverValid.value = true
    serverValidationMessage.value = ''
    serverVariables.value = []
    return
  }
  try {
    const res = await validateExpression({ expression: value })
    serverValid.value = res.isValid
    serverValidationMessage.value = res.isValid
      ? t('approvalDesigner.celValidateOkServer')
      : res.errors.join('；')
    serverVariables.value = res.variables ?? []
  } catch {
    // 网络错误时不阻断前端
    serverValid.value = true
    serverValidationMessage.value = ''
  }
}

const scheduleServerValidate = (value: string) => {
  if (validateTimer) clearTimeout(validateTimer)
  validateTimer = setTimeout(() => runServerValidate(value), 600)
}

const clientResult = computed(() => clientValidate(props.modelValue))
const isValid = computed(() => clientResult.value.valid && serverValid.value)
const statusText = computed(() => {
  if (!clientResult.value.valid) return clientResult.value.message
  if (!serverValid.value) return serverValidationMessage.value
  if (serverValidationMessage.value) return serverValidationMessage.value
  return clientResult.value.message
})

const displayVariables = computed(() => props.variables)

const handleInput = (value: string) => {
  emit('update:modelValue', value)
  const result = clientValidate(value)
  emit('validate', result.valid, result.message)
  scheduleServerValidate(value)
}

const insertVariable = (name: string) => {
  const next = `${props.modelValue}${props.modelValue ? ' ' : ''}${name}.`
  handleInput(next)
}

// 试运行
const tryRunVisible = ref(false)
const tryRunning = ref(false)
const tryRunContext = ref('{}')
const tryRunResult = ref<ExpressionEvaluateResponse | null>(null)

const openTryRun = () => {
  tryRunResult.value = null
  tryRunVisible.value = true
}

const runTryRun = async () => {
  tryRunning.value = true
  tryRunResult.value = null
  try {
    let record: Record<string, unknown> = {}
    try {
      record = JSON.parse(tryRunContext.value)
    } catch {
      tryRunResult.value = { success: false, resultValue: null, resultBool: null, error: t('approvalDesigner.celTryContextJsonErr') }
      return
    }
    tryRunResult.value = await evaluateExpression({
      expression: props.modelValue,
      record,
    })
  } catch (e) {
    tryRunResult.value = { success: false, resultValue: null, resultBool: null, error: String(e) }
  } finally {
    tryRunning.value = false
  }
}

// 组件挂载时初始校验
watch(() => props.modelValue, (v) => {
  if (v) scheduleServerValidate(v)
}, { immediate: true })
</script>

<style scoped>
.expr-editor {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.expr-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 4px;
}

.expr-var-tag {
  cursor: pointer;
  user-select: none;
}

.expr-status {
  font-size: 12px;
  color: #52c41a;
  white-space: nowrap;
}

.expr-status.invalid {
  color: #ff4d4f;
}

.try-run-body {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.try-run-label {
  font-size: 13px;
  color: #666;
  margin-bottom: 4px;
}

.try-run-result {
  margin-top: 8px;
}

.try-run-value {
  margin-left: 8px;
  font-weight: 500;
}

.try-run-bool {
  margin-left: 4px;
  color: #666;
  font-size: 12px;
}

.try-run-error {
  margin-left: 8px;
  color: #ff4d4f;
}
</style>

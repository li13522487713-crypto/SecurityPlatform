<template>
  <td v-bind="$attrs">
    <template v-if="editing">
      <a-input
        v-if="fieldType === 'text'"
        ref="inputRef"
        v-model:value="editValue"
        size="small"
        @blur="handleSave"
        @press-enter="handleSave"
        @keydown.esc="handleCancel"
      />
      <a-input-number
        v-else-if="fieldType === 'number'"
        ref="inputRef"
        v-model:value="editValue"
        size="small"
        :style="{ width: '100%' }"
        @blur="handleSave"
        @press-enter="handleSave"
        @keydown.esc="handleCancel"
      />
      <a-select
        v-else-if="fieldType === 'select'"
        ref="inputRef"
        v-model:value="editValue"
        size="small"
        :style="{ width: '100%' }"
        :options="selectOptions"
        allow-clear
        @change="handleSave"
        @blur="handleCancel"
      />
      <template v-else>
        <a-input
          ref="inputRef"
          v-model:value="editValue"
          size="small"
          @blur="handleSave"
          @press-enter="handleSave"
          @keydown.esc="handleCancel"
        />
      </template>
    </template>
    <div
      v-else
      class="editable-cell-value"
      :class="{ 'editable-cell-value--active': editable }"
      @click="startEdit"
    >
      <slot />
      <EditOutlined v-if="editable && !saving" class="editable-cell-icon" />
      <LoadingOutlined v-if="saving" class="editable-cell-icon" />
    </div>
  </td>
</template>

<script setup lang="ts">
import { nextTick, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { EditOutlined, LoadingOutlined } from "@ant-design/icons-vue";
import { message } from "ant-design-vue";

const { t } = useI18n();

interface SelectOption {
  label: string
  value: string | number
}

const props = withDefaults(defineProps<{
  /** 当前值 */
  value: string | number | null | undefined
  /** 字段类型：text | number | select */
  fieldType?: 'text' | 'number' | 'select'
  /** 是否可编辑 */
  editable?: boolean
  /** select 类型的选项 */
  selectOptions?: SelectOption[]
  /** 保存回调；返回 false 表示保存失败 */
  onSave?: (newValue: string | number | null) => Promise<boolean | void>
}>(), {
  fieldType: 'text',
  editable: true,
  selectOptions: () => [],
})

const emit = defineEmits<{
  'update:value': [value: string | number | null]
  saved: [value: string | number | null]
}>()

const editing = ref(false)
const saving = ref(false)
const editValue = ref<string | number | null | undefined>(props.value)
const inputRef = ref<HTMLElement | null>(null)

watch(() => props.value, (v) => {
  if (!editing.value) editValue.value = v
})

const startEdit = () => {
  if (!props.editable || saving.value) return
  editValue.value = props.value
  editing.value = true
  nextTick(() => {
    const el = inputRef.value as HTMLInputElement | null
    el?.focus?.()
  })
}

const handleSave = async () => {
  if (!editing.value) return
  editing.value = false
  const newVal = editValue.value ?? null
  if (newVal === props.value) return

  if (props.onSave) {
    saving.value = true
    try {
      const ok = await props.onSave(newVal as string | number | null)
      if (ok === false) {
        message.error(t("tableUi.saveFailed"))
        return
      }
      emit('update:value', newVal as string | number | null)
      emit('saved', newVal as string | number | null)
    } catch (e) {
      message.error((e as Error)?.message || t("tableUi.saveFailed"))
    } finally {
      saving.value = false
    }
  } else {
    emit('update:value', newVal as string | number | null)
    emit('saved', newVal as string | number | null)
  }
}

const handleCancel = () => {
  editing.value = false
  editValue.value = props.value
}
</script>

<style scoped>
.editable-cell-value {
  min-height: 22px;
  display: flex;
  align-items: center;
  gap: 6px;
}

.editable-cell-value--active {
  cursor: pointer;
}

.editable-cell-value--active:hover .editable-cell-icon {
  opacity: 1;
}

.editable-cell-icon {
  opacity: 0;
  font-size: 12px;
  color: #1677ff;
  transition: opacity 0.2s;
  flex-shrink: 0;
}
</style>

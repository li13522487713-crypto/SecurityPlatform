<template>
  <a-switch
    :checked="modelValue"
    :loading="loading"
    :checked-children="activeLabel"
    :un-checked-children="inactiveLabel"
    @change="handleChange"
  />
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { message } from "ant-design-vue";

const props = withDefaults(
  defineProps<{
    modelValue: boolean;
    activeText?: string;
    inactiveText?: string;
    successText?: string;
    failedText?: string;
    api: (value: boolean) => Promise<void>;
  }>(),
  {
    activeText: "启用",
    inactiveText: "停用",
    successText: "状态更新成功",
    failedText: "状态更新失败"
  }
);

const emit = defineEmits<{
  "update:modelValue": [value: boolean];
}>();

const loading = ref(false);
const activeLabel = computed(() => props.activeText);
const inactiveLabel = computed(() => props.inactiveText);

const handleChange = async (checked: boolean | string | number) => {
  const newValue = checked as boolean;

  loading.value = true;
  try {
    emit("update:modelValue", newValue);
    await props.api(newValue);
    message.success(props.successText);
  } catch (err) {
    emit("update:modelValue", !newValue);
    message.error(err instanceof Error ? err.message : props.failedText);
  } finally {
    loading.value = false;
  }
};
</script>

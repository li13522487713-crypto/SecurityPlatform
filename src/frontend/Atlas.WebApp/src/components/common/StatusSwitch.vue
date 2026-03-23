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
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const props = withDefaults(defineProps<{
  modelValue: boolean;
  activeText?: string;
  inactiveText?: string;
  api: (value: boolean) => Promise<void>;
}>(), {
  activeText: undefined,
  inactiveText: undefined
});

const activeLabel = computed(() => props.activeText ?? t("common.statusEnabled"));
const inactiveLabel = computed(() => props.inactiveText ?? t("common.statusDisabled"));

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
}>();

const loading = ref(false);

const handleChange = async (checked: boolean | string | number) => {
  const newValue = checked as boolean;
  
  loading.value = true;
  try {
    // 乐观更新
    emit('update:modelValue', newValue);
    // 调用 API
    await props.api(newValue);
    message.success(t("commonUi.statusUpdateSuccess"));
  } catch (err) {
    // 失败回滚
    emit('update:modelValue', !newValue);
    message.error(err instanceof Error ? err.message : t("commonUi.statusUpdateFailed"));
  } finally {
    loading.value = false;
  }
};
</script>

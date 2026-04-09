<template>
  <SharedStatusSwitch
    :model-value="modelValue"
    :api="api"
    :active-text="activeText ?? t('common.statusEnabled')"
    :inactive-text="inactiveText ?? t('common.statusDisabled')"
    :success-text="t('common.statusUpdateSuccess')"
    :failed-text="t('common.statusUpdateFailed')"
    @update:model-value="emit('update:modelValue', $event)"
  />
</template>

<script setup lang="ts">
import { StatusSwitch as SharedStatusSwitch } from "@atlas/shared-ui";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

defineProps<{
  modelValue: boolean;
  activeText?: string;
  inactiveText?: string;
  api: (value: boolean) => Promise<void>;
}>();

const emit = defineEmits<{
  "update:modelValue": [value: boolean];
}>();
</script>

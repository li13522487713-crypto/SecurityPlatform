<template>
  <div class="designer-form-schema">
    <a-alert
      type="info"
      show-icon
      style="margin-bottom: 12px"
      :message="t('approvalDesigner.schemaAlertAmis')"
    />
    <a-textarea
      v-model:value="localText"
      :rows="20"
      :placeholder="t('approvalDesigner.schemaPhExample')"
    />
    <div style="margin-top: 8px; display: flex; gap: 8px">
      <a-button size="small" @click="handleFormat">{{ t('approvalDesigner.schemaBtnFormat') }}</a-button>
      <a-button size="small" type="primary" @click="handleApply">{{ t('approvalDesigner.schemaBtnApply') }}</a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { message } from 'ant-design-vue';

const { t } = useI18n();

const props = defineProps<{
  schemaText: string;
}>();

const emit = defineEmits<{
  'update:schemaText': [value: string];
  'apply': [];
}>();

const localText = ref(props.schemaText);

watch(() => props.schemaText, (newVal) => {
  if (newVal !== localText.value) {
    localText.value = newVal;
  }
});

watch(localText, (newVal) => {
  emit('update:schemaText', newVal);
});

const handleFormat = () => {
  if (!localText.value.trim()) return;
  try {
    const parsed = JSON.parse(localText.value);
    const formatted = JSON.stringify(parsed, null, 2);
    localText.value = formatted;
    emit('update:schemaText', formatted);
  } catch {
    message.error(t('approvalDesigner.schemaMsgInvalidJson'));
  }
};

const handleApply = () => {
  emit('apply');
};
</script>

<style scoped>
.designer-form-schema {
  height: 100%;
  display: flex;
  flex-direction: column;
}
</style>

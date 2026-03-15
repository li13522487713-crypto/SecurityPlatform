<template>
  <div class="designer-form-schema">
    <a-alert
      type="info"
      show-icon
      style="margin-bottom: 12px"
      message="请输入 AMIS Schema（JSON），系统会自动提取字段供条件与权限配置使用"
    />
    <a-textarea
      v-model:value="localText"
      :rows="20"
      placeholder='{"type":"form","body":[{"type":"input-text","name":"title","label":"标题"}]}'
    />
    <div style="margin-top: 8px; display: flex; gap: 8px">
      <a-button size="small" @click="handleFormat">格式化 JSON</a-button>
      <a-button size="small" type="primary" @click="handleApply">应用并提取字段</a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import { message } from 'ant-design-vue';

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
    message.error('AMIS Schema JSON 格式不正确');
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

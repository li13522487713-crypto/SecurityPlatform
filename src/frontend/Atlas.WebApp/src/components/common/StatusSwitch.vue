<template>
  <a-switch 
    :checked="modelValue" 
    :loading="loading"
    @change="handleChange"
    :checked-children="activeText"
    :un-checked-children="inactiveText"
  />
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { message } from 'ant-design-vue';

const props = withDefaults(defineProps<{
  modelValue: boolean;
  activeText?: string;
  inactiveText?: string;
  api: (value: boolean) => Promise<void>;
}>(), {
  activeText: '启用',
  inactiveText: '停用'
});

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
    message.success('状态更新成功');
  } catch (err) {
    // 失败回滚
    emit('update:modelValue', !newValue);
    message.error(err instanceof Error ? err.message : '状态更新失败');
  } finally {
    loading.value = false;
  }
};
</script>

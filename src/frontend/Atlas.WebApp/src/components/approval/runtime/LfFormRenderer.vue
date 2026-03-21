<template>
  <div class="lf-form-renderer">
    <div v-if="loading" class="renderer-loading">
      <a-spin tip="加载表单..." />
    </div>
    <div v-else-if="!formJson || !formJson.widgetList?.length" class="renderer-empty">
      <a-empty description="暂无表单数据" />
    </div>
    <div v-else>
      <v-form-render
        v-if="vformReady"
        ref="formRenderRef"
        :form-json="formJson"
        :form-data="effectiveFormData"
        :option-data="optionData"
        :disabled="disabled || readOnly"
      />
      <div v-else class="renderer-loading">
        <a-spin tip="渲染器初始化中..." />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { getCurrentInstance, onMounted, ref, watch, onUnmounted } from 'vue';

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { FormJson } from '@/types/approval-definition';

const props = defineProps<{
  formJson?: FormJson;
  formData?: Record<string, unknown>;
  readOnly?: boolean;
  disabled?: boolean;
}>();

const vformReady = ref(false);
const loading = ref(true);
const formRenderRef = ref<VFormRenderInstance | null>(null);
const effectiveFormData = ref<Record<string, unknown>>({});
const optionData = ref<Record<string, unknown>>({});

watch(
  () => props.formData,
  (val) => {
    effectiveFormData.value = val ?? {};
    if (formRenderRef.value && vformReady.value) {
      formRenderRef.value.setFormData(effectiveFormData.value);
    }
  },
  { immediate: true }
);

onMounted(async () => {
  await initVForm();

  if (!isMounted.value) return;
  loading.value = false;
});

const initVForm = async () => {
  const instance = getCurrentInstance();
  if (!instance) return;

  const mod  = await import('vform3-builds');


  if (!isMounted.value) return;
  await import('vform3-builds/dist/designer.style.css');

  if (!isMounted.value) return;

  const app = instance.appContext.app;
  const globals = app.config.globalProperties as { __vform3_installed__?: boolean };
  if (!globals.__vform3_installed__) {
    app.use(mod.default);
    globals.__vform3_installed__ = true;
  }
  vformReady.value = true;
};

type VFormRenderInstance = {
  getFormData: () => Promise<Record<string, unknown>>;
  setFormData: (data: Record<string, unknown>) => void;
  resetForm: () => void;
};
</script>

<style scoped>
.lf-form-renderer {
  min-height: 60px;
}
.renderer-loading {
  display: flex;
  justify-content: center;
  align-items: center;
  padding: 32px;
}
.renderer-empty {
  padding: 24px 0;
}
</style>

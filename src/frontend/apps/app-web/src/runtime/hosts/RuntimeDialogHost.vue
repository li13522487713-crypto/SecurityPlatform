<template>
  <a-modal
    v-model:open="visible"
    :title="dialogTitle"
    :width="dialogWidth"
    :destroy-on-close="true"
    @cancel="handleClose"
  >
    <a-spin :spinning="loading">
      <AmisRenderer v-if="schema" :schema="schema" />
      <a-empty v-else :description="t('runtimePage.emptyNoPage')" />
    </a-spin>
    <template #footer>
      <a-button @click="handleClose">{{ t('common.cancel') }}</a-button>
    </template>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import AmisRenderer from "@/components/amis/amis-renderer.vue";
import type { AmisSchema } from "@/types/amis";

interface Props {
  open: boolean;
  dialogKey: string;
  schema?: AmisSchema | null;
  title?: string;
  width?: string | number;
  loading?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  schema: null,
  title: "",
  width: 720,
  loading: false,
});

const emit = defineEmits<{
  (e: "update:open", value: boolean): void;
  (e: "close"): void;
}>();

const { t } = useI18n();

const visible = ref(props.open);
const dialogTitle = ref(props.title || t("runtimePage.defaultTitle"));
const dialogWidth = ref(props.width);
const loading = ref(props.loading);
const schema = ref<AmisSchema | null>(props.schema ?? null);

watch(
  () => props.open,
  (val) => {
    visible.value = val;
  },
);

watch(
  () => props.schema,
  (val) => {
    schema.value = val ?? null;
  },
);

watch(
  () => props.title,
  (val) => {
    dialogTitle.value = val || t("runtimePage.defaultTitle");
  },
);

watch(
  () => props.loading,
  (val) => {
    loading.value = val;
  },
);

function handleClose() {
  visible.value = false;
  emit("update:open", false);
  emit("close");
}
</script>

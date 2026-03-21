<template>
  <div class="designer-toolbar">
    <a-space>
      <a-button :loading="loading" type="primary" @click="emit('save')">{{ t("designerUi.save") }}</a-button>
      <a-button :loading="loading" @click="emit('validate')">{{ t("designerUi.validate") }}</a-button>
      <a-button :loading="loading" type="default" @click="emit('preview')">{{ t("designerUi.preview") }}</a-button>
      <a-button :loading="loading" type="dashed" @click="emit('publish')">{{ t("designerUi.publish") }}</a-button>
    </a-space>
    <slot />
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";

const { t } = useI18n();

interface Props {
  loading?: boolean;
}
defineProps<Props>();
const emit = defineEmits(["save", "validate", "preview", "publish"]);
</script>

<style scoped>
.designer-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 0;
}
</style>

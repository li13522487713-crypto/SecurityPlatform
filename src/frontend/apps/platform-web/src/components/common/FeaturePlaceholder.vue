<template>
  <div class="feature-placeholder">
    <a-result status="info" :title="t(titleKey)" :sub-title="t(descriptionKey)">
      <template #extra>
        <a-button v-if="backPath" type="primary" @click="router.push(backPath)">
          {{ t(backLabelKey || "featurePlaceholder.backDefault") }}
        </a-button>
      </template>
    </a-result>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
import { useRouter } from "vue-router";

withDefaults(
  defineProps<{
    titleKey: string;
    descriptionKey?: string;
    backPath?: string;
    backLabelKey?: string;
  }>(),
  {
    descriptionKey: "featurePlaceholder.comingSoonDesc",
    backPath: undefined,
    backLabelKey: undefined
  }
);

const { t } = useI18n();
const router = useRouter();
</script>

<style scoped>
.feature-placeholder {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 400px;
  padding: 48px 24px;
}
</style>

<template>
  <div class="palette-panel">
    <div class="title">{{ t("designerUi.paletteTitle") }}</div>
    <a-space wrap>
      <a-tag
        v-for="item in items"
        :key="item.type"
        color="blue"
        @click="() => emit('add', item.type)"
      >
        {{ item.label }}
      </a-tag>
    </a-space>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { paletteItems } from "./NodePalette";
import type { NodeType } from "@/types/workflow";

const { t } = useI18n();

const items = computed(() =>
  paletteItems.map((item) => ({
    ...item,
    label: t(`approvalPalette.${item.type.replace(/-/g, "_")}`)
  }))
);

const emit = defineEmits<{
  (e: "add", type: NodeType): void;
}>();
</script>

<style scoped>
.palette-panel {
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  padding: 12px;
  background: #fff;
}
.title {
  font-weight: 600;
  margin-bottom: 8px;
}
</style>

<template>
  <div class="flow-connection-style">
    <div class="field">
      <div class="label">{{ t("logicFlow.designerUi.connection.lineStyle") }}</div>
      <a-select :value="lineStyle" style="width: 100%" @update:value="emit('update:lineStyle', $event as LineStyle)">
        <a-select-option value="solid">{{ t("logicFlow.designerUi.connection.solid") }}</a-select-option>
        <a-select-option value="dashed">{{ t("logicFlow.designerUi.connection.dashed") }}</a-select-option>
        <a-select-option value="dotted">{{ t("logicFlow.designerUi.connection.dotted") }}</a-select-option>
      </a-select>
    </div>
    <div class="field">
      <div class="label">{{ t("logicFlow.designerUi.connection.color") }}</div>
      <input :value="color" type="color" class="color-input" @input="onColorInput" />
    </div>
    <div class="field">
      <div class="label">{{ t("logicFlow.designerUi.connection.label") }}</div>
      <a-input :value="label" @update:value="emit('update:label', $event)" />
    </div>
    <div class="field row">
      <span class="label">{{ t("logicFlow.designerUi.connection.animation") }}</span>
      <a-switch :checked="animated" @update:checked="emit('update:animated', $event)" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";

export type LineStyle = "solid" | "dashed" | "dotted";

defineProps<{
  lineStyle: LineStyle;
  color: string;
  label: string;
  animated: boolean;
}>();

const emit = defineEmits<{
  (e: "update:lineStyle", value: LineStyle): void;
  (e: "update:color", value: string): void;
  (e: "update:label", value: string): void;
  (e: "update:animated", value: boolean): void;
}>();

const { t } = useI18n();

function onColorInput(ev: Event): void {
  const el = ev.target as HTMLInputElement;
  emit("update:color", el.value);
}
</script>

<style scoped>
.field {
  margin-bottom: 12px;
}

.field.row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.label {
  margin-bottom: 4px;
  font-size: 14px;
  color: rgba(0, 0, 0, 0.85);
}

.color-input {
  width: 100%;
  height: 32px;
  padding: 0;
  border: 1px solid #d9d9d9;
  border-radius: 4px;
  cursor: pointer;
}
</style>

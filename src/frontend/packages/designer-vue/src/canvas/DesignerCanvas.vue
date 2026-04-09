<template>
  <a-card :title="cardTitle" class="atlas-designer-canvas">
    <template #extra>
      <a-space v-if="showModeSwitcher" size="small">
        <a-radio-group :value="currentMode" size="small" button-style="solid" @update:value="handleModeChange">
          <a-radio-button
            v-for="mode in enabledModes"
            :key="mode.value"
            :value="mode.value"
          >
            {{ mode.label }}
          </a-radio-button>
        </a-radio-group>
        <slot name="toolbar" />
      </a-space>
      <slot v-else name="toolbar" />
    </template>

    <template v-if="$slots.entity || $slots.default">
      <div
        v-show="currentMode === 'entity'"
      >
        <slot
          v-if="$slots.entity"
          name="entity"
          v-bind="{ mode: currentMode, context, modeSwitch: handleModeChange }"
        />
        <slot
          v-else
          v-bind="{ mode: currentMode, context, modeSwitch: handleModeChange }"
        />
      </div>
    </template>
    <template v-if="$slots.relation && currentMode === 'relation'">
      <slot
        name="relation"
        v-bind="{ mode: currentMode, context, modeSwitch: handleModeChange }"
      />
    </template>
    <template v-if="$slots.view && currentMode === 'view'">
      <slot
        name="view"
        v-bind="{ mode: currentMode, context, modeSwitch: handleModeChange }"
      />
    </template>
    <template v-if="$slots.transform && currentMode === 'transform'">
      <slot
        name="transform"
        v-bind="{ mode: currentMode, context, modeSwitch: handleModeChange }"
      />
    </template>
    <slot
      name="empty"
      v-if="!hasActiveSlot"
    />
    <a-empty
      v-else-if="isEmptyState"
      description="当前模式暂无可用设计器内容"
    />
  </a-card>
</template>

<script setup lang="ts">
import { computed, useSlots, watch } from "vue";

const designerModeMap = {
  entity: "实体模型",
  relation: "关系模型",
  view: "视图模型",
  transform: "转换模型",
} as const;

export type DesignerCanvasMode = "entity" | "relation" | "view" | "transform";

export interface DesignerCanvasContext {
  appId?: string;
  tableKey?: string;
  viewKey?: string;
}

const props = withDefaults(
  defineProps<{
    mode?: DesignerCanvasMode;
    title?: string;
    context?: DesignerCanvasContext;
    showModeSwitcher?: boolean;
    availableModes?: DesignerCanvasMode[];
    className?: string;
    emptyText?: string;
  }>(),
  {
    mode: "entity",
    showModeSwitcher: false,
    availableModes: () => ["entity", "relation", "view", "transform"],
    className: "",
  },
);

const emit = defineEmits<{
  "update:mode": [mode: DesignerCanvasMode];
  "mode-change": [mode: DesignerCanvasMode];
  "dirty-change": [dirty: boolean];
  submit: [];
}>();

const currentMode = computed({
  get: () => props.mode,
  set: (nextMode) => {
    emit("update:mode", nextMode);
    emit("mode-change", nextMode);
  },
});

const enabledModes = computed(() =>
  props.availableModes.map((mode) => ({ value: mode, label: designerModeMap[mode] })),
);

const slots = useSlots();

const hasSlot = (name: "entity" | "relation" | "view" | "transform" | "default" | "empty") => {
  if (!slots[name]) {
    return false;
  }
  return true;
};

const hasActiveSlot = computed(() => {
  if (currentMode.value === "entity") return hasSlot("entity") || hasSlot("default");
  if (currentMode.value === "relation") return hasSlot("relation");
  if (currentMode.value === "view") return hasSlot("view");
  return hasSlot("transform");
});

const isEmptyState = computed(() => !hasActiveSlot.value);

const cardTitle = computed(() => props.title || "数据设计器");

const showModeSwitcher = computed(() => props.showModeSwitcher);

function handleModeChange(mode: DesignerCanvasMode) {
  currentMode.value = mode;
}

watch(
  () => props.mode,
  (nextMode) => {
    if (!enabledModes.value.some((item) => item.value === nextMode)) {
      handleModeChange(enabledModes.value[0]?.value ?? "entity");
    }
  },
  { deep: true },
);
</script>

<style scoped>
.atlas-designer-canvas {
  width: 100%;
}
</style>


<template>
  <a-card :title="title" class="page-card" :data-testid="`e2e-page-card-${sanitizeTestId(title)}`">
    <div class="crud-toolbar" data-testid="e2e-crud-toolbar">
      <a-space wrap>
        <a-input
          v-model:value="keywordModel"
          :placeholder="searchPlaceholder"
          allow-clear
          data-testid="e2e-crud-search-input"
          @press-enter="$emit('search')"
        />
        <a-button data-testid="e2e-crud-search-submit" @click="$emit('search')">{{ t("crud.search") }}</a-button>
        <a-button data-testid="e2e-crud-search-reset" @click="$emit('reset')">{{ t("crud.reset") }}</a-button>
        <slot name="toolbar-actions" />
      </a-space>
      <a-space wrap data-testid="e2e-crud-toolbar-right">
        <slot name="toolbar-right" />
      </a-space>
    </div>

    <div v-if="$slots.filter" class="crud-filter-bar" data-testid="e2e-crud-filter-bar">
      <a-space wrap>
        <span class="crud-filter-label">{{ t("crud.filters") }}</span>
        <slot name="filter" />
      </a-space>
    </div>

    <div data-testid="e2e-crud-table-region">
      <slot name="table" />
    </div>

    <a-drawer
      v-model:open="drawerOpenModel"
      :title="drawerTitle"
      placement="right"
      :width="drawerWidth"
      destroy-on-close
      :data-testid="`e2e-crud-drawer-${sanitizeTestId(drawerTitle)}`"
      @close="$emit('close-form')"
    >
      <slot name="form" />
      <template #footer>
        <a-space data-testid="e2e-crud-drawer-footer">
          <a-button
            data-testid="e2e-crud-drawer-cancel"
            :disabled="submitDisabled || submitLoading"
            @click="$emit('close-form')"
          >
            {{ t("crud.cancel") }}
          </a-button>
          <a-button
            type="primary"
            :loading="submitLoading"
            :disabled="submitDisabled"
            data-testid="e2e-crud-drawer-submit"
            @click="$emit('submit')"
          >
            {{ t("crud.save") }}
          </a-button>
        </a-space>
      </template>
    </a-drawer>

    <slot name="extra-drawers" />
  </a-card>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";

const props = defineProps<{
  title: string;
  keyword: string;
  searchPlaceholder?: string;
  drawerOpen: boolean;
  drawerTitle: string;
  drawerWidth?: number | string;
  submitLoading?: boolean;
  submitDisabled?: boolean;
}>();

const emit = defineEmits<{
  (e: "update:keyword", value: string): void;
  (e: "update:drawerOpen", value: boolean): void;
  (e: "search"): void;
  (e: "reset"): void;
  (e: "close-form"): void;
  (e: "submit"): void;
}>();

const { t } = useI18n();

const submitLoading = computed(() => props.submitLoading ?? false);
const submitDisabled = computed(() => props.submitDisabled ?? false);

const keywordModel = computed({
  get: () => props.keyword,
  set: (value: string) => emit("update:keyword", value)
});

const drawerOpenModel = computed({
  get: () => props.drawerOpen,
  set: (value: boolean) => emit("update:drawerOpen", value)
});

function sanitizeTestId(value: string) {
  return value.replace(/[^a-zA-Z0-9]+/g, "-").replace(/^-+|-+$/g, "").toLowerCase();
}
</script>

<style scoped>
.crud-toolbar {
  margin-bottom: 12px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.crud-filter-bar {
  margin-bottom: 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.crud-filter-label {
  color: var(--color-text-tertiary);
}
</style>

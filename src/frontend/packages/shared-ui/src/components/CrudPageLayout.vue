<template>
  <a-card class="page-card" :data-testid="`e2e-page-card-${sanitizeTestId(title)}`">
    <template v-if="hasRichHead" #title>
      <div class="crud-page-card-head">
        <div class="crud-page-card-head-text">
          <span class="crud-page-card-title">{{ title }}</span>
          <div v-if="subtitle" class="crud-page-card-subtitle">{{ subtitle }}</div>
        </div>
      </div>
    </template>
    <template v-else #title>{{ title }}</template>
    <template v-if="$slots['card-extra']" #extra>
      <slot name="card-extra" />
    </template>
    <div class="crud-search-bar" data-testid="e2e-crud-search-bar">
      <div class="search-form-wrapper">
        <a-form layout="inline" style="width: 100%; display: flex; flex-wrap: wrap; gap: 0px 0;" @submit.prevent="$emit('search')">
          <a-form-item v-if="keywordModel !== undefined">
            <a-input
              v-model:value="keywordModel"
              :placeholder="searchPlaceholder"
              allow-clear
              data-testid="e2e-crud-search-input"
              style="width: 280px; max-width: 100%;"
            />
          </a-form-item>
          <slot name="search-filters" />
          <slot name="filter" />

          <div style="flex: auto; display: flex; justify-content: flex-end; align-items: flex-start;">
            <a-form-item style="margin-right: 0;">
              <a-space>
                <a-button type="primary" html-type="submit" data-testid="e2e-crud-search-submit">{{ t("crud.search") }}</a-button>
                <a-button data-testid="e2e-crud-search-reset" @click="$emit('reset')">{{ t("crud.reset") }}</a-button>
              </a-space>
            </a-form-item>
          </div>
        </a-form>
      </div>
    </div>

    <div class="crud-toolbar" data-testid="e2e-crud-toolbar">
      <div class="toolbar-left">
        <a-space wrap>
          <slot name="toolbar-actions" />
        </a-space>
      </div>
      <div class="toolbar-right" data-testid="e2e-crud-toolbar-right">
        <a-space wrap>
          <slot name="toolbar-right" />
        </a-space>
      </div>
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
            :disabled="submitDisabledComputed || submitLoadingComputed"
            @click="$emit('close-form')"
          >
            {{ t("crud.cancel") }}
          </a-button>
          <a-button
            type="primary"
            :loading="submitLoadingComputed"
            :disabled="submitDisabledComputed"
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
import { computed, useSlots } from "vue";
import { useI18n } from "vue-i18n";

const props = defineProps<{
  title: string;
  /** Optional subtitle under the card title (e.g. page description). */
  subtitle?: string;
  keyword?: string;
  searchPlaceholder?: string;
  drawerOpen?: boolean;
  drawerTitle?: string;
  drawerWidth?: number | string;
  submitLoading?: boolean;
  submitDisabled?: boolean;
}>();

const vueSlots = useSlots();
const hasRichHead = computed(() => Boolean(props.subtitle || vueSlots["card-extra"]));

const emit = defineEmits<{
  (e: "update:keyword", value: string | undefined): void;
  (e: "update:drawerOpen", value: boolean): void;
  (e: "search"): void;
  (e: "reset"): void;
  (e: "close-form"): void;
  (e: "submit"): void;
}>();

const { t } = useI18n();

const submitLoadingComputed = computed(() => props.submitLoading ?? false);
const submitDisabledComputed = computed(() => props.submitDisabled ?? false);

const keywordModel = computed({
  get: () => props.keyword,
  set: (value: string | undefined) => emit("update:keyword", value),
});

const drawerOpenModel = computed({
  get: () => props.drawerOpen ?? false,
  set: (value: boolean) => emit("update:drawerOpen", value),
});

function sanitizeTestId(value?: string) {
  if (!value) return "unknown";
  return value
    .replace(/[^a-zA-Z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .toLowerCase();
}
</script>

<style scoped>
.crud-search-bar {
  margin-bottom: 16px;
  padding: 16px;
  background-color: var(--color-fill-quaternary, #f7f9fa);
  border-radius: 8px;
}

.search-form-wrapper {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
}

.crud-toolbar {
  margin-bottom: 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
}

.toolbar-left,
.toolbar-right {
  display: flex;
  align-items: center;
}

.crud-page-card-head {
  display: flex;
  align-items: flex-start;
  min-width: 0;
}

.crud-page-card-head-text {
  min-width: 0;
}

.crud-page-card-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--color-text-heading, rgba(0, 0, 0, 0.88));
  line-height: 1.4;
}

.crud-page-card-subtitle {
  margin-top: 4px;
  font-size: 13px;
  font-weight: 400;
  color: var(--color-text-secondary, rgba(0, 0, 0, 0.45));
  line-height: 1.5;
}
</style>

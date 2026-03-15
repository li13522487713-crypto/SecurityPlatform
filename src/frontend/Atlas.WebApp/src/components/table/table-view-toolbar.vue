<template>
  <div class="table-view-toolbar" data-testid="e2e-table-view-toolbar">
    <a-space wrap>
      <a-select
        :value="controller.state.currentViewId"
        style="min-width: 220px"
        show-search
        allow-clear
        placeholder="Saved view"
        :filter-option="false"
        :options="viewOptions"
        :loading="controller.state.loading"
        data-testid="e2e-table-view-select"
        @search="handleSearch"
        @focus="handleFocus"
        @change="handleSelect"
      />
      <a-button type="primary" data-testid="e2e-table-view-save" @click="handleSave">Save</a-button>
      <a-button data-testid="e2e-table-view-save-as" @click="openSaveAs">Save as</a-button>
      <a-button
        :disabled="!controller.state.currentViewId"
        data-testid="e2e-table-view-set-default"
        @click="handleSetDefault"
      >
        Set default
      </a-button>
      <a-button data-testid="e2e-table-view-reset-current" @click="handleResetCurrent">Reset current</a-button>
      <a-button data-testid="e2e-table-view-reset-default" @click="handleResetDefault">Reset default</a-button>
      <a-dropdown>
        <a-button data-testid="e2e-table-view-density">Density: {{ densityLabel }}</a-button>
        <template #overlay>
          <a-menu @click="handleDensityChange">
            <a-menu-item key="compact">Compact</a-menu-item>
            <a-menu-item key="default">Default</a-menu-item>
            <a-menu-item key="comfortable">Comfortable</a-menu-item>
          </a-menu>
        </template>
      </a-dropdown>
      <a-button data-testid="e2e-table-view-columns" @click="columnsVisible = true">Columns</a-button>
    </a-space>
  </div>

  <a-drawer
    v-model:open="columnsVisible"
    title="Columns"
    placement="right"
    width="420"
    destroy-on-close
    data-testid="e2e-table-view-columns-drawer"
  >
    <a-input
      v-model:value="columnKeyword"
      placeholder="Search columns"
      allow-clear
      style="margin-bottom: 12px"
      data-testid="e2e-table-view-columns-search"
    />
    <div class="column-list">
      <div v-for="item in filteredColumns" :key="item.key" class="column-item">
        <a-checkbox
          :checked="item.visible"
          :disabled="!item.canHide"
          @update:checked="(checked: boolean) => controller.toggleColumn(item.key, checked)"
        >
          {{ item.title }}
        </a-checkbox>
        <div class="column-actions">
          <a-select
            :value="item.pinned ?? 'none'"
            size="small"
            style="width: 88px"
            @change="(val: string) => controller.setPinned(item.key, val === 'none' ? undefined : val as 'left' | 'right')"
          >
            <a-select-option value="none">None</a-select-option>
            <a-select-option value="left">Left</a-select-option>
            <a-select-option value="right">Right</a-select-option>
          </a-select>
          <a-button size="small" @click="controller.moveColumn(item.key, 'up')">Up</a-button>
          <a-button size="small" @click="controller.moveColumn(item.key, 'down')">Down</a-button>
        </div>
      </div>
    </div>
  </a-drawer>

  <a-drawer
    v-model:open="saveAsVisible"
    title="Save as view"
    placement="right"
    width="380"
    destroy-on-close
    data-testid="e2e-table-view-save-as-drawer"
  >
    <a-form layout="vertical">
      <a-form-item label="View name">
        <a-input
          v-model:value="saveAsName"
          placeholder="Enter view name"
          data-testid="e2e-table-view-save-as-name"
        />
      </a-form-item>
    </a-form>
    <template #footer>
      <a-space data-testid="e2e-table-view-save-as-footer">
        <a-button data-testid="e2e-table-view-save-as-cancel" @click="saveAsVisible = false">Cancel</a-button>
        <a-button type="primary" data-testid="e2e-table-view-save-as-submit" @click="handleSaveAs">Save</a-button>
      </a-space>
    </template>
  </a-drawer>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import type { MenuProps } from "ant-design-vue";
import type { TableViewController, TableViewDensity } from "@/composables/useTableView";

const props = defineProps<{
  controller: TableViewController;
}>();

const columnsVisible = ref(false);
const columnKeyword = ref("");
const saveAsVisible = ref(false);
const saveAsName = ref("");

const viewOptions = computed(() =>
  props.controller.state.views.map((item) => ({
    label: item.isDefault ? `${item.name} (default)` : item.name,
    value: item.id
  }))
);

const filteredColumns = computed(() => {
  const keyword = columnKeyword.value.trim();
  if (!keyword) {
    return props.controller.columnSettings;
  }
  return props.controller.columnSettings.filter((item) => item.title.includes(keyword));
});

const densityLabel = computed(() => {
  if (props.controller.state.density === "compact") return "Compact";
  if (props.controller.state.density === "comfortable") return "Comfortable";
  return "Default";
});

const handleSearch = async (value: string) => {
  await props.controller.searchViews(value);
};

const handleFocus = async () => {
  if (props.controller.state.views.length === 0) {
    await props.controller.searchViews();
  }
};

const handleSelect = async (value: string | number | null) => {
  if (value === null || value === undefined) {
    await props.controller.selectView(null);
    return;
  }
  await props.controller.selectView(value.toString());
};

const openSaveAs = () => {
  saveAsName.value = "";
  saveAsVisible.value = true;
};

const handleSaveAs = async () => {
  const name = saveAsName.value.trim();
  if (!name) {
    return;
  }
  await props.controller.saveAs(name);
  saveAsVisible.value = false;
};

const handleSave = async () => {
  if (!props.controller.state.currentViewId) {
    openSaveAs();
    return;
  }
  await props.controller.saveView();
};

const handleSetDefault = async () => {
  await props.controller.setDefault();
};

const handleResetDefault = async () => {
  await props.controller.resetToDefault();
};

const handleResetCurrent = async () => {
  await props.controller.resetCurrent();
};

const handleDensityChange: MenuProps["onClick"] = (info) => {
  props.controller.setDensity(info.key as TableViewDensity);
};
</script>

<style scoped>
.table-view-toolbar {
  display: flex;
  justify-content: flex-end;
  align-items: center;
  gap: 8px;
}

.column-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.column-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
}

.column-actions {
  display: flex;
  gap: 4px;
}
</style>

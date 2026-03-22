<template>
  <div class="table-view-toolbar" data-testid="e2e-table-view-toolbar">
    <a-space wrap>
      <a-select
        :value="controller.state.currentViewId"
        style="min-width: 220px"
        show-search
        allow-clear
        :placeholder="t('tableView.savedViewPlaceholder')"
        :filter-option="false"
        :options="viewOptions"
        :loading="controller.state.loading"
        data-testid="e2e-table-view-select"
        @search="handleSearch"
        @focus="handleFocus"
        @change="handleSelect"
      />
      <a-button type="primary" data-testid="e2e-table-view-save" @click="handleSave">{{ t("tableView.save") }}</a-button>
      <a-button data-testid="e2e-table-view-save-as" @click="openSaveAs">{{ t("tableView.saveAs") }}</a-button>
      <a-button
        :disabled="!controller.state.currentViewId"
        data-testid="e2e-table-view-set-default"
        @click="handleSetDefault"
      >
        {{ t("tableView.setDefault") }}
      </a-button>
      <a-button data-testid="e2e-table-view-reset-current" @click="handleResetCurrent">{{ t("tableView.resetCurrent") }}</a-button>
      <a-button data-testid="e2e-table-view-reset-default" @click="handleResetDefault">{{ t("tableView.resetDefault") }}</a-button>
      <a-dropdown>
        <a-button data-testid="e2e-table-view-density">{{ t("tableView.density") }}: {{ densityLabel }}</a-button>
        <template #overlay>
          <a-menu @click="handleDensityChange">
            <a-menu-item key="compact">{{ t("tableView.densityCompact") }}</a-menu-item>
            <a-menu-item key="default">{{ t("tableView.densityDefault") }}</a-menu-item>
            <a-menu-item key="comfortable">{{ t("tableView.densityComfortable") }}</a-menu-item>
          </a-menu>
        </template>
      </a-dropdown>
      <a-button data-testid="e2e-table-view-columns" @click="columnsVisible = true">{{ t("tableView.columns") }}</a-button>
    </a-space>
  </div>

  <a-drawer
    v-model:open="columnsVisible"
    :title="t('tableView.columnsTitle')"
    placement="right"
    width="420"
    destroy-on-close
    data-testid="e2e-table-view-columns-drawer"
  >
    <a-input
      v-model:value="columnKeyword"
      :placeholder="t('tableView.searchColumns')"
      allow-clear
      style="margin-bottom: 12px"
      data-testid="e2e-table-view-columns-search"
    />
    <div class="column-list">
      <a-tree
        class="column-tree"
        checkable
        draggable
        block-node
        :tree-data="treeData"
        :checked-keys="checkedKeys"
        @check="handleTreeCheck"
        @drop="handleTreeDrop"
      >
        <template #title="{ key, title, pinned, canHide }">
          <div class="column-item">
            <span class="column-title" :title="title">{{ title }}</span>
            <div class="column-actions">
              <a-select
                :value="pinned ?? 'none'"
                size="small"
                style="width: 88px"
                @change="(val: string) => controller.setPinned(key as string, val === 'none' ? undefined : val as 'left' | 'right')"
              >
                <a-select-option value="none">{{ t('tableView.pinNone') }}</a-select-option>
                <a-select-option value="left">{{ t('tableView.pinLeft') }}</a-select-option>
                <a-select-option value="right">{{ t('tableView.pinRight') }}</a-select-option>
              </a-select>
            </div>
          </div>
        </template>
      </a-tree>
    </div>
  </a-drawer>

  <a-drawer
    v-model:open="saveAsVisible"
    :title="t('tableView.saveAsView')"
    placement="right"
    width="380"
    destroy-on-close
    data-testid="e2e-table-view-save-as-drawer"
  >
    <a-form layout="vertical">
      <a-form-item :label="t('tableView.viewName')">
        <a-input
          v-model:value="saveAsName"
          :placeholder="t('tableView.enterViewName')"
          data-testid="e2e-table-view-save-as-name"
        />
      </a-form-item>
    </a-form>
    <template #footer>
      <a-space data-testid="e2e-table-view-save-as-footer">
        <a-button data-testid="e2e-table-view-save-as-cancel" @click="saveAsVisible = false">{{ t("common.cancel") }}</a-button>
        <a-button type="primary" data-testid="e2e-table-view-save-as-submit" @click="handleSaveAs">{{ t("common.save") }}</a-button>
      </a-space>
    </template>
  </a-drawer>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import type { MenuProps } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import type { TableViewController, TableViewDensity } from "@/composables/useTableView";

const props = defineProps<{
  controller: TableViewController;
}>();

const columnsVisible = ref(false);
const columnKeyword = ref("");
const saveAsVisible = ref(false);
const saveAsName = ref("");
const { t } = useI18n();

const viewOptions = computed(() =>
  props.controller.state.views.map((item) => ({
    label: item.isDefault ? `${item.name} (${t("tableView.defaultSuffix")})` : item.name,
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

const treeData = computed(() => {
  return filteredColumns.value.map(col => ({
    key: col.key,
    title: col.title,
    pinned: col.pinned,
    canHide: col.canHide,
    disabled: !col.canHide,
    children: (col as any).children // 保留扩展性
  }));
});

const checkedKeys = computed(() => {
  return filteredColumns.value.filter(col => col.visible).map(col => col.key);
});

const handleTreeCheck = (checked: any) => {
  const checkedKeyList: string[] = Array.isArray(checked) ? checked : checked.checked;
  props.controller.columnSettings.forEach(col => {
    if (col.canHide) {
      const isVisible = checkedKeyList.includes(col.key);
      if (col.visible !== isVisible) {
        props.controller.toggleColumn(col.key, isVisible);
      }
    }
  });
};

const handleTreeDrop = (info: any) => {
  const dragKey = info.dragNode.key;
  // 这里可以触发列重排序，由于 P0 简化先保持原有逻辑流接口
  // 如果落点在上方触发 moveColumn('up') etc.
  // 推荐后续在 updateTableViewConfig 直接写新 orders 数组
};

const densityLabel = computed(() => {
  if (props.controller.state.density === "compact") return t("tableView.densityCompact");
  if (props.controller.state.density === "comfortable") return t("tableView.densityComfortable");
  return t("tableView.densityDefault");
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

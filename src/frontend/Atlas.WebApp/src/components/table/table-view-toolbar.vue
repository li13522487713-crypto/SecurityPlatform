<template>
  <div class="table-view-toolbar">
    <a-space wrap>
      <a-select
        :value="controller.state.currentViewId"
        style="min-width: 220px"
        show-search
        allow-clear
        placeholder="未保存视图"
        :filter-option="false"
        :options="viewOptions"
        :loading="controller.state.loading"
        @search="handleSearch"
        @focus="handleFocus"
        @change="handleSelect"
      />
      <a-button type="primary" @click="handleSave">保存</a-button>
      <a-button @click="openSaveAs">另存为</a-button>
      <a-button :disabled="!controller.state.currentViewId" @click="handleSetDefault">
        设为默认
      </a-button>
      <a-button @click="handleResetCurrent">重置视图</a-button>
      <a-button @click="handleResetDefault">恢复默认</a-button>
      <a-dropdown>
        <a-button>
          密度：{{ densityLabel }}
        </a-button>
        <template #overlay>
          <a-menu @click="handleDensityChange">
            <a-menu-item key="compact">紧凑</a-menu-item>
            <a-menu-item key="default">默认</a-menu-item>
            <a-menu-item key="comfortable">舒适</a-menu-item>
          </a-menu>
        </template>
      </a-dropdown>
      <a-button @click="columnsVisible = true">列配置</a-button>
    </a-space>
  </div>

  <a-drawer
    v-model:open="columnsVisible"
    title="列配置"
    placement="right"
    width="360"
    destroy-on-close
  >
    <a-input
      v-model:value="columnKeyword"
      placeholder="搜索列"
      allow-clear
      style="margin-bottom: 12px"
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
          <a-button size="small" @click="controller.moveColumn(item.key, 'up')">上移</a-button>
          <a-button size="small" @click="controller.moveColumn(item.key, 'down')">下移</a-button>
        </div>
      </div>
    </div>
  </a-drawer>

  <a-drawer
    v-model:open="saveAsVisible"
    title="另存为视图"
    placement="right"
    width="380"
    destroy-on-close
  >
    <a-form layout="vertical">
      <a-form-item label="视图名称">
        <a-input v-model:value="saveAsName" placeholder="请输入视图名称" />
      </a-form-item>
    </a-form>
    <template #footer>
      <a-space>
        <a-button @click="saveAsVisible = false">取消</a-button>
        <a-button type="primary" @click="handleSaveAs">保存</a-button>
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
    label: item.isDefault ? `${item.name}（默认）` : item.name,
    value: item.id
  }))
);

const filteredColumns = computed(() => {
  const keyword = columnKeyword.value.trim();
  if (!keyword) return props.controller.columnSettings;
  return props.controller.columnSettings.filter((item) => item.title.includes(keyword));
});

const densityLabel = computed(() => {
  if (props.controller.state.density === "compact") return "紧凑";
  if (props.controller.state.density === "comfortable") return "舒适";
  return "默认";
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
  if (!name) return;
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

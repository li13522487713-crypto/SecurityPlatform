<template>
  <div class="menu-editor-panel">
    <div class="menu-editor-header">
      <h4>{{ t("designer.menuEditor.title") }}</h4>
      <a-button size="small" @click="addMenuItem">
        {{ t("designer.menuEditor.addItem") }}
      </a-button>
    </div>

    <a-table
      :data-source="menuItems"
      :pagination="false"
      row-key="key"
      size="small"
      bordered
    >
      <a-table-column :title="t('designer.menuEditor.icon')" width="100px">
        <template #default="{ record }">
          <a-input v-model:value="record.icon" size="small" placeholder="fa fa-home" />
        </template>
      </a-table-column>
      <a-table-column :title="t('designer.menuEditor.label')" width="140px">
        <template #default="{ record }">
          <a-input v-model:value="record.label" size="small" />
        </template>
      </a-table-column>
      <a-table-column :title="t('designer.menuEditor.targetPage')" width="180px">
        <template #default="{ record }">
          <a-select
            v-model:value="record.pageKey"
            size="small"
            show-search
            allow-clear
            style="width: 100%"
            :options="pageOptions"
          />
        </template>
      </a-table-column>
      <a-table-column :title="t('designer.menuEditor.permission')" width="140px">
        <template #default="{ record }">
          <a-input v-model:value="record.permissionCode" size="small" placeholder="e.g. app:view" />
        </template>
      </a-table-column>
      <a-table-column width="60px">
        <template #default="{ record }">
          <a-button type="link" danger size="small" @click="removeMenuItem(record.key)">
            x
          </a-button>
        </template>
      </a-table-column>
    </a-table>

    <div class="menu-editor-footer">
      <a-button type="primary" :loading="saving" @click="handleSave">
        {{ t("designer.menuEditor.save") }}
      </a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";

const { t } = useI18n();

export interface MenuItemConfig {
  key: string;
  icon: string;
  label: string;
  pageKey: string;
  permissionCode: string;
}

interface Props {
  pages?: { label: string; value: string }[];
  initialItems?: MenuItemConfig[];
}

const props = withDefaults(defineProps<Props>(), {
  pages: () => [],
  initialItems: () => [],
});

const emit = defineEmits<{
  (e: "save", items: MenuItemConfig[]): void;
}>();

const saving = ref(false);
const pageOptions = ref(props.pages);

let nextKey = 1;
function makeKey() {
  return `menu-${nextKey++}`;
}

const menuItems = reactive<MenuItemConfig[]>(
  props.initialItems.length > 0
    ? props.initialItems.map((item) => ({ ...item, key: item.key || makeKey() }))
    : [],
);

function addMenuItem() {
  menuItems.push({
    key: makeKey(),
    icon: "fa fa-file",
    label: "",
    pageKey: "",
    permissionCode: "",
  });
}

function removeMenuItem(key: string) {
  const idx = menuItems.findIndex((item) => item.key === key);
  if (idx >= 0) {
    menuItems.splice(idx, 1);
  }
}

async function handleSave() {
  saving.value = true;
  try {
    emit("save", [...menuItems]);
    message.success(t("designer.menuEditor.saveSuccess"));
  } finally {
    saving.value = false;
  }
}
</script>

<style scoped>
.menu-editor-panel {
  padding: 16px;
}

.menu-editor-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.menu-editor-header h4 {
  margin: 0;
}

.menu-editor-footer {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
</style>

<template>
  <a-card title="应用配置" class="page-card">
    <div class="toolbar">
      <a-space wrap>
        <a-input
          v-model:value="keyword"
          placeholder="搜索应用名称/标识"
          allow-clear
          @press-enter="handleSearch"
        />
        <a-button @click="handleSearch">查询</a-button>
        <a-button @click="handleReset">重置</a-button>
      </a-space>
      <TableViewToolbar :controller="tableViewController" />
    </div>

    <a-table
      :columns="tableColumns"
      :data-source="dataSource"
      :pagination="pagination"
      :loading="loading"
      :size="tableSize"
      row-key="id"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'isActive'">
          <a-tag v-if="record.isActive" color="green">启用</a-tag>
          <a-tag v-else color="red">停用</a-tag>
        </template>
        <template v-if="column.key === 'enableProjectScope'">
          <a-tag v-if="record.enableProjectScope" color="blue">开启</a-tag>
          <a-tag v-else>关闭</a-tag>
        </template>
        <template v-if="column.key === 'actions'">
          <a-space>
            <a-button v-if="canUpdate" type="link" @click="openEdit(record)">编辑</a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-drawer
      v-model:open="formVisible"
      title="编辑应用配置"
      placement="right"
      width="520"
      @close="closeForm"
      destroy-on-close
    >
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item label="应用标识">
          <a-input v-model:value="formModel.appId" disabled />
        </a-form-item>
        <a-form-item label="应用名称" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item label="状态">
          <a-switch v-model:checked="formModel.isActive" checked-children="启用" un-checked-children="停用" />
        </a-form-item>
        <a-form-item label="项目维度">
          <a-switch
            v-model:checked="formModel.enableProjectScope"
            checked-children="开启"
            un-checked-children="关闭"
          />
        </a-form-item>
        <a-form-item label="排序">
          <a-input-number v-model:value="formModel.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-input v-model:value="formModel.description" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="closeForm">取消</a-button>
          <a-button type="primary" @click="submitForm">保存</a-button>
        </a-space>
      </template>
    </a-drawer>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { FormInstance, TablePaginationConfig } from "ant-design-vue";
import type { Rule } from "ant-design-vue/es/form";
import { message } from "ant-design-vue";
import TableViewToolbar from "@/components/table/table-view-toolbar.vue";
import { useTableView } from "@/composables/useTableView";
import { getAppConfigsPaged, updateAppConfig } from "@/services/api";
import type { AppConfigListItem, AppConfigUpdateRequest } from "@/types/api";
import { getAuthProfile, hasPermission } from "@/utils/auth";

const baseColumns = [
  { title: "应用标识", dataIndex: "appId", key: "appId" },
  { title: "应用名称", dataIndex: "name", key: "name" },
  { title: "状态", key: "isActive" },
  { title: "项目维度", key: "enableProjectScope" },
  { title: "排序", dataIndex: "sortOrder", key: "sortOrder" },
  { title: "操作", key: "actions", view: { canHide: false } }
];

const dataSource = ref<AppConfigListItem[]>([]);
const loading = ref(false);
const keyword = ref("");
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => `共 ${total} 条`
});

const formVisible = ref(false);
const formRef = ref<FormInstance>();
const formModel = reactive<AppConfigUpdateRequest & { id: string; appId: string }>({
  id: "",
  appId: "",
  name: "",
  isActive: true,
  enableProjectScope: false,
  description: "",
  sortOrder: 0
});

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: "请输入应用名称" }]
};

const profile = getAuthProfile();
const canUpdate = hasPermission(profile, "apps:update");

const fetchData = async () => {
  loading.value = true;
  try {
    const result  = await getAppConfigsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined
    });
    if (!isMounted.value) return;
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (error) {
    message.error((error as Error).message || "查询失败");
  } finally {
    loading.value = false;
  }
};

const { controller: tableViewController, tableColumns, tableSize } = useTableView<AppConfigListItem>({
  tableKey: "system.apps",
  columns: baseColumns,
  pagination,
  onRefresh: fetchData
});

const handleSearch = () => {
  pagination.current = 1;
  fetchData();
};

const handleReset = () => {
  keyword.value = "";
  handleSearch();
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  fetchData();
};

const openEdit = (record: AppConfigListItem) => {
  formModel.id = record.id;
  formModel.appId = record.appId;
  formModel.name = record.name;
  formModel.isActive = record.isActive;
  formModel.enableProjectScope = record.enableProjectScope;
  formModel.description = record.description ?? "";
  formModel.sortOrder = record.sortOrder;
  formVisible.value = true;
};

const closeForm = () => {
  formVisible.value = false;
};

const submitForm = async () => {
  const valid  = await formRef.value?.validate().catch(() => false);
  if (!isMounted.value) return;
  if (!valid) return;

  try {
    const previousScope = dataSource.value.find((item) => item.id === formModel.id)?.enableProjectScope;
    await updateAppConfig(formModel.id, {
      name: formModel.name,
      isActive: formModel.isActive,
      enableProjectScope: formModel.enableProjectScope,
      description: formModel.description || undefined,
      sortOrder: formModel.sortOrder
    });
    if (!isMounted.value) return;
    message.success("更新成功，配置已刷新");
    if (previousScope !== formModel.enableProjectScope) {
      window.dispatchEvent(new CustomEvent("app-config-changed"));
    }
    formVisible.value = false;
    fetchData();
  } catch (error) {
    message.error((error as Error).message || "更新失败");
  }
};

onMounted(fetchData);
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
  display: flex;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
</style>

<template>
  <a-card title="应用配置" class="page-card">
    <div class="toolbar">
      <a-space>
        <a-input
          v-model:value="keyword"
          placeholder="搜索应用名称/标识"
          allow-clear
          @press-enter="fetchData"
        />
      </a-space>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataSource"
      :pagination="pagination"
      :loading="loading"
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

    <a-modal
      v-model:open="formVisible"
      title="编辑应用配置"
      @ok="submitForm"
      @cancel="closeForm"
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
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import type { FormInstance, TablePaginationConfig } from "ant-design-vue";
import type { Rule } from "ant-design-vue/es/form";
import { message } from "ant-design-vue";
import { getAppConfigsPaged, updateAppConfig } from "@/services/api";
import type { AppConfigListItem, AppConfigUpdateRequest } from "@/types/api";
import { getAuthProfile, hasPermission } from "@/utils/auth";

const columns = [
  { title: "应用标识", dataIndex: "appId" },
  { title: "应用名称", dataIndex: "name" },
  { title: "状态", key: "isActive" },
  { title: "项目维度", key: "enableProjectScope" },
  { title: "排序", dataIndex: "sortOrder" },
  { title: "操作", key: "actions" }
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
    const result = await getAppConfigsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined
    });
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (error) {
    message.error((error as Error).message || "查询失败");
  } finally {
    loading.value = false;
  }
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
  const valid = await formRef.value?.validate().catch(() => false);
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
}
</style>

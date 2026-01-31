<template>
  <a-card title="菜单管理" class="page-card">
    <div class="toolbar">
      <a-space>
        <a-input
          v-model:value="keyword"
          placeholder="搜索菜单名称/路径"
          allow-clear
          @press-enter="fetchData"
        />
        <a-button v-if="canCreate" type="primary" @click="openCreate">新增菜单</a-button>
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
        <template v-if="column.key === 'parent'">
          <span>{{ getParentName(record.parentId) }}</span>
        </template>
        <template v-if="column.key === 'hidden'">
          <a-tag v-if="record.isHidden" color="orange">隐藏</a-tag>
          <span v-else>-</span>
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
      :title="formMode === 'create' ? '新增菜单' : '编辑菜单'"
      @ok="submitForm"
      @cancel="closeForm"
      destroy-on-close
    >
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item label="菜单名称" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item label="菜单路径" name="path">
          <a-input v-model:value="formModel.path" />
        </a-form-item>
        <a-form-item label="上级菜单" name="parentId">
          <a-select
            v-model:value="formModel.parentId"
            :options="parentOptions"
            allow-clear
            placeholder="无"
          />
        </a-form-item>
        <a-form-item label="排序" name="sortOrder">
          <a-input-number v-model:value="formModel.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
        <a-form-item label="组件" name="component">
          <a-input v-model:value="formModel.component" />
        </a-form-item>
        <a-form-item label="图标" name="icon">
          <a-input v-model:value="formModel.icon" />
        </a-form-item>
        <a-form-item label="权限编码" name="permissionCode">
          <a-input v-model:value="formModel.permissionCode" />
        </a-form-item>
        <a-form-item label="隐藏菜单" name="isHidden">
          <a-switch v-model:checked="formModel.isHidden" />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import type { TablePaginationConfig, FormInstance } from "ant-design-vue";
import type { Rule } from "ant-design-vue/es/form";
import { message } from "ant-design-vue";
import { createMenu, getMenusAll, getMenusPaged, updateMenu } from "@/services/api";
import type { MenuCreateRequest, MenuListItem, MenuUpdateRequest } from "@/types/api";
import { getAuthProfile, hasPermission } from "@/utils/auth";

type FormMode = "create" | "edit";

interface SelectOption {
  label: string;
  value: number;
}

const columns = [
  { title: "菜单名称", dataIndex: "name" },
  { title: "路径", dataIndex: "path" },
  { title: "上级菜单", key: "parent" },
  { title: "排序", dataIndex: "sortOrder" },
  { title: "组件", dataIndex: "component" },
  { title: "图标", dataIndex: "icon" },
  { title: "权限编码", dataIndex: "permissionCode" },
  { title: "隐藏", key: "hidden" },
  { title: "操作", key: "actions" }
];

const dataSource = ref<MenuListItem[]>([]);
const loading = ref(false);
const keyword = ref("");
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => `共 ${total} 条`
});

const formVisible = ref(false);
const formMode = ref<FormMode>("create");
const formRef = ref<FormInstance>();
const formModel = reactive<MenuCreateRequest & MenuUpdateRequest>({
  name: "",
  path: "",
  parentId: undefined,
  sortOrder: 0,
  component: "",
  icon: "",
  permissionCode: "",
  isHidden: false
});

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: "请输入菜单名称" }],
  path: [{ required: true, message: "请输入菜单路径" }]
};

const parentOptions = ref<SelectOption[]>([]);
const parentNameMap = ref<Map<number, string>>(new Map());
const selectedId = ref<string | null>(null);
const profile = getAuthProfile();
const canCreate = hasPermission(profile, "menus:create");
const canUpdate = hasPermission(profile, "menus:update");

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getMenusPaged({
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

const fetchParents = async () => {
  try {
    const list = await getMenusAll();
    parentOptions.value = list.map((item) => ({
      label: item.name,
      value: Number(item.id)
    }));
    parentNameMap.value = new Map(
      list.map((item) => [Number(item.id), item.name])
    );
  } catch (error) {
    message.error((error as Error).message || "加载菜单失败");
  }
};

const getParentName = (parentId?: number | null) => {
  if (!parentId) return "-";
  return parentNameMap.value.get(Number(parentId)) ?? "-";
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  fetchData();
};

const resetForm = () => {
  formModel.name = "";
  formModel.path = "";
  formModel.parentId = undefined;
  formModel.sortOrder = 0;
  formModel.component = "";
  formModel.icon = "";
  formModel.permissionCode = "";
  formModel.isHidden = false;
};

const openCreate = () => {
  formMode.value = "create";
  selectedId.value = null;
  resetForm();
  formVisible.value = true;
};

const openEdit = (record: MenuListItem) => {
  formMode.value = "edit";
  selectedId.value = record.id;
  formModel.name = record.name;
  formModel.path = record.path;
  formModel.parentId = record.parentId ?? undefined;
  formModel.sortOrder = record.sortOrder;
  formModel.component = record.component ?? "";
  formModel.icon = record.icon ?? "";
  formModel.permissionCode = record.permissionCode ?? "";
  formModel.isHidden = record.isHidden;
  formVisible.value = true;
};

const closeForm = () => {
  formVisible.value = false;
};

const submitForm = async () => {
  const valid = await formRef.value?.validate().catch(() => false);
  if (!valid) return;

  try {
    if (formMode.value === "create") {
      await createMenu({
        name: formModel.name,
        path: formModel.path,
        parentId: formModel.parentId ?? undefined,
        sortOrder: formModel.sortOrder,
        component: formModel.component || undefined,
        icon: formModel.icon || undefined,
        permissionCode: formModel.permissionCode || undefined,
        isHidden: formModel.isHidden
      });
      message.success("创建成功");
    } else if (selectedId.value) {
      await updateMenu(selectedId.value, {
        name: formModel.name,
        path: formModel.path,
        parentId: formModel.parentId ?? undefined,
        sortOrder: formModel.sortOrder,
        component: formModel.component || undefined,
        icon: formModel.icon || undefined,
        permissionCode: formModel.permissionCode || undefined,
        isHidden: formModel.isHidden
      });
      message.success("更新成功");
    }
    formVisible.value = false;
    fetchData();
    fetchParents();
  } catch (error) {
    message.error((error as Error).message || "提交失败");
  }
};

onMounted(() => {
  fetchParents();
  fetchData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}
</style>

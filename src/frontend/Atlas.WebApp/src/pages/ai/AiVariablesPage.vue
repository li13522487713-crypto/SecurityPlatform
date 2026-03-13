<template>
  <a-card title="变量管理" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          placeholder="搜索变量 Key"
          style="width: 240px"
          @search="loadData"
        />
        <a-select
          v-model:value="scopeFilter"
          style="width: 140px"
          allow-clear
          placeholder="作用域"
          :options="scopeOptions"
          @change="loadData"
        />
        <a-button @click="handleReset">重置</a-button>
        <a-button @click="showSystemDefinitions">系统变量</a-button>
        <a-button type="primary" @click="openCreate">新增变量</a-button>
      </a-space>
    </div>

    <a-table row-key="id" :columns="columns" :data-source="list" :loading="loading" :pagination="false">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'scope'">
          <a-tag :color="scopeColor(record.scope)">
            {{ scopeLabel(record.scope) }}
          </a-tag>
        </template>
        <template v-if="column.key === 'value'">
          <a-typography-paragraph :ellipsis="{ rows: 1, expandable: true, symbol: '展开' }">
            {{ record.value || "-" }}
          </a-typography-paragraph>
        </template>
        <template v-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="openEdit(record.id)">编辑</a-button>
            <a-popconfirm title="确认删除该变量？" @confirm="handleDelete(record.id)">
              <a-button type="link" danger>删除</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <div class="pager">
      <a-pagination
        v-model:current="pageIndex"
        v-model:page-size="pageSize"
        :total="total"
        show-size-changer
        :page-size-options="['10', '20', '50']"
        @change="loadData"
      />
    </div>

    <a-modal
      v-model:open="modalOpen"
      :title="editingId ? '编辑变量' : '新增变量'"
      :confirm-loading="modalLoading"
      @ok="submitForm"
      @cancel="closeModal"
    >
      <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
        <a-form-item label="Key" name="key">
          <a-input v-model:value="form.key" />
        </a-form-item>
        <a-form-item label="Value" name="value">
          <a-textarea v-model:value="form.value" :rows="4" />
        </a-form-item>
        <a-form-item label="作用域" name="scope">
          <a-select v-model:value="form.scope" :options="scopeOptions" />
        </a-form-item>
        <a-form-item label="作用域ID" name="scopeId">
          <a-input-number v-model:value="form.scopeId" :min="1" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-drawer
      v-model:open="systemDrawerOpen"
      title="系统变量定义"
      width="680"
    >
      <a-table row-key="key" :data-source="systemVariables" :columns="systemColumns" :pagination="false" />
    </a-drawer>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { message } from "ant-design-vue";
import type { FormInstance } from "ant-design-vue";
import {
  createAiVariable,
  deleteAiVariable,
  getAiSystemVariableDefinitions,
  getAiVariableById,
  getAiVariablesPaged,
  updateAiVariable,
  type AiSystemVariableDefinition,
  type AiVariableListItem,
  type AiVariableScope
} from "@/services/api-ai-variable";

const keyword = ref("");
const scopeFilter = ref<AiVariableScope | undefined>(undefined);
const list = ref<AiVariableListItem[]>([]);
const loading = ref(false);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const columns = [
  { title: "Key", dataIndex: "key", key: "key", width: 220 },
  { title: "Value", key: "value" },
  { title: "作用域", key: "scope", width: 120 },
  { title: "Scope ID", dataIndex: "scopeId", key: "scopeId", width: 120 },
  { title: "更新时间", dataIndex: "updatedAt", key: "updatedAt", width: 200 },
  { title: "操作", key: "action", width: 140 }
];

const scopeOptions = [
  { label: "System", value: 0 },
  { label: "Project", value: 1 },
  { label: "Bot", value: 2 }
];

const modalOpen = ref(false);
const modalLoading = ref(false);
const editingId = ref<number | null>(null);
const formRef = ref<FormInstance>();
const form = reactive({
  key: "",
  value: "",
  scope: 0 as AiVariableScope,
  scopeId: undefined as number | undefined
});
const rules = {
  key: [{ required: true, message: "请输入变量 Key" }],
  scope: [{ required: true, message: "请选择作用域" }]
};

const systemDrawerOpen = ref(false);
const systemVariables = ref<AiSystemVariableDefinition[]>([]);
const systemColumns = [
  { title: "Key", dataIndex: "key", key: "key", width: 200 },
  { title: "名称", dataIndex: "name", key: "name", width: 150 },
  { title: "描述", dataIndex: "description", key: "description" },
  { title: "默认值", dataIndex: "defaultValue", key: "defaultValue", width: 150 }
];

async function loadData() {
  loading.value = true;
  try {
    const result = await getAiVariablesPaged(
      {
        pageIndex: pageIndex.value,
        pageSize: pageSize.value,
        keyword: keyword.value || undefined
      },
      {
        scope: scopeFilter.value
      }
    );
    list.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || "加载变量失败");
  } finally {
    loading.value = false;
  }
}

function handleReset() {
  keyword.value = "";
  scopeFilter.value = undefined;
  pageIndex.value = 1;
  void loadData();
}

function openCreate() {
  editingId.value = null;
  Object.assign(form, {
    key: "",
    value: "",
    scope: 0 as AiVariableScope,
    scopeId: undefined
  });
  modalOpen.value = true;
}

async function openEdit(id: number) {
  try {
    const detail = await getAiVariableById(id);
    editingId.value = id;
    Object.assign(form, {
      key: detail.key,
      value: detail.value ?? "",
      scope: detail.scope,
      scopeId: detail.scopeId
    });
    modalOpen.value = true;
  } catch (error: unknown) {
    message.error((error as Error).message || "加载变量详情失败");
  }
}

function closeModal() {
  modalOpen.value = false;
  formRef.value?.resetFields();
}

async function submitForm() {
  try {
    await formRef.value?.validate();
  } catch {
    return;
  }

  modalLoading.value = true;
  try {
    const payload = {
      key: form.key,
      value: form.value || undefined,
      scope: form.scope,
      scopeId: form.scopeId
    };

    if (editingId.value) {
      await updateAiVariable(editingId.value, payload);
      message.success("更新成功");
    } else {
      await createAiVariable(payload);
      message.success("创建成功");
    }

    modalOpen.value = false;
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || "提交失败");
  } finally {
    modalLoading.value = false;
  }
}

async function handleDelete(id: number) {
  try {
    await deleteAiVariable(id);
    message.success("删除成功");
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || "删除失败");
  }
}

async function showSystemDefinitions() {
  try {
    systemVariables.value = await getAiSystemVariableDefinitions();
    systemDrawerOpen.value = true;
  } catch (error: unknown) {
    message.error((error as Error).message || "加载系统变量失败");
  }
}

function scopeLabel(scope: AiVariableScope) {
  if (scope === 1) return "Project";
  if (scope === 2) return "Bot";
  return "System";
}

function scopeColor(scope: AiVariableScope) {
  if (scope === 1) return "purple";
  if (scope === 2) return "orange";
  return "blue";
}

onMounted(() => {
  void loadData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}

.pager {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
</style>

<template>
  <a-card title="模型配置" :bordered="false">
    <div class="crud-toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          placeholder="搜索名称/Provider/模型"
          allow-clear
          style="width: 280px"
          @search="loadData"
        />
        <a-button @click="handleReset">重置</a-button>
        <a-button type="primary" @click="openCreate">新建模型</a-button>
      </a-space>
    </div>

    <a-table
      row-key="id"
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      :pagination="pagination"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'isEnabled'">
          <a-tag :color="record.isEnabled ? 'green' : 'default'">
            {{ record.isEnabled ? "启用" : "停用" }}
          </a-tag>
        </template>
        <template v-if="column.key === 'actions'">
          <a-space>
            <a-button type="link" size="small" @click="openEdit(record)">编辑</a-button>
            <a-popconfirm title="确认删除该模型配置？" @confirm="handleDelete(record.id)">
              <a-button type="link" danger size="small">删除</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="modalVisible"
      :title="editingId ? '编辑模型配置' : '新建模型配置'"
      :confirm-loading="modalLoading"
      @ok="submitForm"
      @cancel="closeModal"
    >
      <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
        <a-form-item label="名称" name="name">
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item label="Provider" name="providerType">
          <a-select
            v-model:value="form.providerType"
            :options="providerOptions"
            :disabled="!!editingId"
          />
        </a-form-item>
        <a-form-item label="ApiKey" name="apiKey">
          <a-input-password v-model:value="form.apiKey" />
        </a-form-item>
        <a-form-item label="BaseUrl" name="baseUrl">
          <a-input v-model:value="form.baseUrl" />
        </a-form-item>
        <a-form-item label="默认模型" name="defaultModel">
          <a-input v-model:value="form.defaultModel" />
        </a-form-item>
        <a-form-item>
          <a-space>
            <a-switch v-model:checked="form.supportsEmbedding" />
            <span>支持 Embedding</span>
          </a-space>
        </a-form-item>
        <a-form-item v-if="editingId">
          <a-space>
            <a-switch v-model:checked="form.isEnabled" />
            <span>启用</span>
          </a-space>
        </a-form-item>
        <a-form-item>
          <a-button :loading="testing" @click="handleTestConnection">测试连接</a-button>
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import type { FormInstance, TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import {
  createModelConfig,
  deleteModelConfig,
  getModelConfigById,
  getModelConfigsPaged,
  type ModelConfigCreateRequest,
  type ModelConfigDto,
  testModelConfigConnection,
  updateModelConfig
} from "@/services/api-model-config";

const keyword = ref("");
const dataList = ref<ModelConfigDto[]>([]);
const loading = ref(false);
const testing = ref(false);

const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  pageSizeOptions: ["10", "20", "50"]
});

const columns = [
  { title: "名称", dataIndex: "name", key: "name" },
  { title: "Provider", dataIndex: "providerType", key: "providerType" },
  { title: "BaseUrl", dataIndex: "baseUrl", key: "baseUrl", ellipsis: true },
  { title: "模型", dataIndex: "defaultModel", key: "defaultModel" },
  { title: "状态", key: "isEnabled", width: 90 },
  { title: "操作", key: "actions", width: 140 }
];

const providerOptions = [
  { label: "OpenAI", value: "openai" },
  { label: "DeepSeek", value: "deepseek" },
  { label: "Ollama", value: "ollama" },
  { label: "Custom", value: "custom" }
];

const modalVisible = ref(false);
const modalLoading = ref(false);
const editingId = ref<number | null>(null);
const formRef = ref<FormInstance>();
const form = reactive({
  name: "",
  providerType: "openai",
  apiKey: "",
  baseUrl: "",
  defaultModel: "",
  supportsEmbedding: true,
  isEnabled: true
});

const rules = {
  name: [{ required: true, message: "请输入名称" }],
  providerType: [{ required: true, message: "请选择 Provider" }],
  apiKey: [{ required: true, message: "请输入 ApiKey" }],
  baseUrl: [{ required: true, message: "请输入 BaseUrl" }],
  defaultModel: [{ required: true, message: "请输入模型名称" }]
};

async function loadData() {
  loading.value = true;
  try {
    const result = await getModelConfigsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20,
      keyword: keyword.value || undefined
    });
    dataList.value = result.items;
    pagination.total = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || "加载失败");
  } finally {
    loading.value = false;
  }
}

function onTableChange(page: TablePaginationConfig) {
  pagination.current = page.current ?? 1;
  pagination.pageSize = page.pageSize ?? 20;
  void loadData();
}

function handleReset() {
  keyword.value = "";
  pagination.current = 1;
  void loadData();
}

function openCreate() {
  editingId.value = null;
  Object.assign(form, {
    name: "",
    providerType: "openai",
    apiKey: "",
    baseUrl: "",
    defaultModel: "",
    supportsEmbedding: true,
    isEnabled: true
  });
  modalVisible.value = true;
}

async function openEdit(record: ModelConfigDto) {
  try {
    const detail = await getModelConfigById(record.id);
    editingId.value = record.id;
    Object.assign(form, {
      name: detail.name,
      providerType: detail.providerType,
      apiKey: "",
      baseUrl: detail.baseUrl,
      defaultModel: detail.defaultModel,
      supportsEmbedding: detail.supportsEmbedding,
      isEnabled: detail.isEnabled
    });
    modalVisible.value = true;
  } catch (error: unknown) {
    message.error((error as Error).message || "加载详情失败");
  }
}

function closeModal() {
  modalVisible.value = false;
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
    if (editingId.value) {
      await updateModelConfig(editingId.value, {
        name: form.name,
        apiKey: form.apiKey,
        baseUrl: form.baseUrl,
        defaultModel: form.defaultModel,
        isEnabled: form.isEnabled,
        supportsEmbedding: form.supportsEmbedding
      });
      message.success("更新成功");
    } else {
      const payload: ModelConfigCreateRequest = {
        name: form.name,
        providerType: form.providerType,
        apiKey: form.apiKey,
        baseUrl: form.baseUrl,
        defaultModel: form.defaultModel,
        supportsEmbedding: form.supportsEmbedding
      };
      await createModelConfig(payload);
      message.success("创建成功");
    }

    modalVisible.value = false;
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || "提交失败");
  } finally {
    modalLoading.value = false;
  }
}

async function handleDelete(id: number) {
  try {
    await deleteModelConfig(id);
    message.success("删除成功");
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || "删除失败");
  }
}

async function handleTestConnection() {
  testing.value = true;
  try {
    const result = await testModelConfigConnection({
      providerType: form.providerType,
      apiKey: form.apiKey,
      baseUrl: form.baseUrl,
      model: form.defaultModel
    });
    if (result.success) {
      message.success(`连接成功，延迟 ${result.latencyMs ?? 0}ms`);
    } else {
      message.error(result.errorMessage || "连接失败");
    }
  } catch (error: unknown) {
    message.error((error as Error).message || "连接测试失败");
  } finally {
    testing.value = false;
  }
}

onMounted(() => {
  void loadData();
});
</script>

<style scoped>
.crud-toolbar {
  margin-bottom: 16px;
}
</style>

<template>
  <CrudPageLayout
    v-model:keyword="keyword"
    title="模型配置"
    search-placeholder="搜索名称 / Provider / 模型"
    :drawer-open="drawerVisible"
    :drawer-title="editingId ? '编辑模型配置' : '新建模型配置'"
    :drawer-width="560"
    @update:drawer-open="drawerVisible = $event"
    @search="handleSearch"
    @reset="handleReset"
    @close-form="closeDrawer"
    @submit="submitForm"
  >
    <template #toolbar-actions>
      <a-button type="primary" @click="openCreate">
        <template #icon><PlusOutlined /></template>
        新建模型
      </a-button>
    </template>

    <template #table>
      <!-- 统计概览 -->
      <a-row :gutter="16" class="stats-row">
        <a-col :span="6">
          <div class="stat-card">
            <div class="stat-value">{{ stats.total }}</div>
            <div class="stat-label">配置总数</div>
          </div>
        </a-col>
        <a-col :span="6">
          <div class="stat-card stat-card--success">
            <div class="stat-value">{{ stats.enabled }}</div>
            <div class="stat-label">已启用</div>
          </div>
        </a-col>
        <a-col :span="6">
          <div class="stat-card stat-card--warning">
            <div class="stat-value">{{ stats.disabled }}</div>
            <div class="stat-label">已停用</div>
          </div>
        </a-col>
        <a-col :span="6">
          <div class="stat-card stat-card--info">
            <div class="stat-value">{{ stats.embeddingCount }}</div>
            <div class="stat-label">支持 Embedding</div>
          </div>
        </a-col>
      </a-row>

      <a-table
        row-key="id"
        :columns="columns"
        :data-source="dataList"
        :loading="loading"
        :pagination="pagination"
        size="middle"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'name'">
            <div class="cell-name">
              <span class="name-text">{{ record.name }}</span>
              <a-tag v-if="record.supportsEmbedding" color="purple" class="embed-tag">
                Embedding
              </a-tag>
            </div>
          </template>

          <template v-if="column.key === 'providerType'">
            <a-tag :color="providerMeta(record.providerType).color">
              <component :is="providerMeta(record.providerType).icon" style="margin-right: 4px" />
              {{ providerMeta(record.providerType).label }}
            </a-tag>
          </template>

          <template v-if="column.key === 'baseUrl'">
            <a-typography-text :content="record.baseUrl" :ellipsis="{ tooltip: record.baseUrl }" class="url-text" />
          </template>

          <template v-if="column.key === 'defaultModel'">
            <a-tag>{{ record.defaultModel }}</a-tag>
          </template>

          <template v-if="column.key === 'apiKeyMasked'">
            <span class="api-key-masked">{{ record.apiKeyMasked || "••••••••" }}</span>
          </template>

          <template v-if="column.key === 'isEnabled'">
            <a-badge :status="record.isEnabled ? 'success' : 'default'" :text="record.isEnabled ? '启用' : '停用'" />
          </template>

          <template v-if="column.key === 'createdAt'">
            <span class="date-text">{{ formatDate(record.createdAt) }}</span>
          </template>

          <template v-if="column.key === 'actions'">
            <a-space>
              <a-tooltip title="编辑">
                <a-button type="link" size="small" @click="openEdit(record)">
                  <template #icon><EditOutlined /></template>
                </a-button>
              </a-tooltip>
              <a-popconfirm
                title="确认删除该模型配置？"
                ok-text="确认"
                cancel-text="取消"
                @confirm="handleDelete(record.id)"
              >
                <a-tooltip title="删除">
                  <a-button type="link" danger size="small">
                    <template #icon><DeleteOutlined /></template>
                  </a-button>
                </a-tooltip>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <template #form>
      <a-form ref="formRef" :model="form" layout="vertical" :rules="currentRules">
        <!-- 基本信息 -->
        <a-divider orientation="left" style="margin-top: 0">基本信息</a-divider>

        <a-form-item label="配置名称" name="name">
          <a-input v-model:value="form.name" placeholder="如：GPT-4o 生产环境" />
        </a-form-item>

        <a-form-item label="Provider 类型" name="providerType">
          <a-select
            v-model:value="form.providerType"
            :options="providerOptions"
            :disabled="!!editingId"
            @change="onProviderChange"
          >
            <template #option="{ label, value: val }">
              <a-space>
                <component :is="providerMeta(val).icon" />
                <span>{{ label }}</span>
              </a-space>
            </template>
          </a-select>
        </a-form-item>

        <!-- 连接配置 -->
        <a-divider orientation="left">连接配置</a-divider>

        <a-form-item label="API Key" name="apiKey">
          <a-input-password
            v-model:value="form.apiKey"
            :placeholder="editingId ? '留空则不修改' : '输入 API Key'"
          />
          <template #extra>
            <span class="field-hint">{{ providerHints.apiKeyHint }}</span>
          </template>
        </a-form-item>

        <a-form-item label="Base URL" name="baseUrl">
          <a-input v-model:value="form.baseUrl" :placeholder="providerHints.baseUrlPlaceholder" />
          <template #extra>
            <span class="field-hint">{{ providerHints.baseUrlHint }}</span>
          </template>
        </a-form-item>

        <a-form-item label="默认模型" name="defaultModel">
          <a-auto-complete
            v-model:value="form.defaultModel"
            :options="suggestedModels"
            placeholder="输入或选择模型名称"
            :filter-option="filterModelOption"
          />
          <template #extra>
            <span class="field-hint">可手动输入或从建议列表中选择</span>
          </template>
        </a-form-item>

        <!-- 功能与状态 -->
        <a-divider orientation="left">功能与状态</a-divider>

        <a-row :gutter="24">
          <a-col :span="12">
            <a-form-item label="支持 Embedding">
              <a-switch v-model:checked="form.supportsEmbedding" />
              <span class="switch-label">{{ form.supportsEmbedding ? "已开启" : "已关闭" }}</span>
            </a-form-item>
          </a-col>
          <a-col v-if="editingId" :span="12">
            <a-form-item label="启用状态">
              <a-switch v-model:checked="form.isEnabled" />
              <span class="switch-label">{{ form.isEnabled ? "已启用" : "已停用" }}</span>
            </a-form-item>
          </a-col>
        </a-row>

        <!-- 连接测试 -->
        <a-divider orientation="left">连接测试</a-divider>

        <div class="test-section">
          <a-button type="default" :loading="testing" @click="handleTestConnection">
            <template #icon><ApiOutlined /></template>
            测试连接
          </a-button>

          <a-alert
            v-if="testResult"
            :type="testResult.success ? 'success' : 'error'"
            :message="testResult.success ? '连接成功' : '连接失败'"
            :description="testResultDescription"
            show-icon
            closable
            class="test-result"
            @close="testResult = null"
          />
        </div>
      </a-form>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import type { FormInstance, TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  ApiOutlined,
  CloudOutlined,
  ThunderboltOutlined,
  RobotOutlined,
  ToolOutlined
} from "@ant-design/icons-vue";
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";
import {
  createModelConfig,
  deleteModelConfig,
  getModelConfigById,
  getModelConfigsPaged,
  type ModelConfigCreateRequest,
  type ModelConfigDto,
  type ModelConfigTestResult,
  testModelConfigConnection,
  updateModelConfig
} from "@/services/api-model-config";

interface ProviderInfo {
  label: string;
  color: string;
  icon: ReturnType<typeof CloudOutlined>;
}

const keyword = ref("");
const dataList = ref<ModelConfigDto[]>([]);
const loading = ref(false);
const testing = ref(false);
const testResult = ref<ModelConfigTestResult | null>(null);

const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  pageSizeOptions: ["10", "20", "50"],
  showTotal: (total: number) => `共 ${total} 条`
});

const stats = computed(() => {
  const total = dataList.value.length;
  const enabled = dataList.value.filter((d) => d.isEnabled).length;
  const disabled = total - enabled;
  const embeddingCount = dataList.value.filter((d) => d.supportsEmbedding).length;
  return { total, enabled, disabled, embeddingCount };
});

const columns = [
  { title: "名称", dataIndex: "name", key: "name", width: 200 },
  { title: "Provider", dataIndex: "providerType", key: "providerType", width: 130 },
  { title: "Base URL", dataIndex: "baseUrl", key: "baseUrl", ellipsis: true },
  { title: "默认模型", dataIndex: "defaultModel", key: "defaultModel", width: 160 },
  { title: "API Key", dataIndex: "apiKeyMasked", key: "apiKeyMasked", width: 120 },
  { title: "状态", key: "isEnabled", width: 90 },
  { title: "创建时间", dataIndex: "createdAt", key: "createdAt", width: 110 },
  { title: "操作", key: "actions", width: 100, fixed: "right" as const }
];

const providerOptions = [
  { label: "OpenAI", value: "openai" },
  { label: "DeepSeek", value: "deepseek" },
  { label: "Ollama", value: "ollama" },
  { label: "Custom", value: "custom" }
];

const providerModelSuggestions: Record<string, string[]> = {
  openai: ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo", "text-embedding-3-small", "text-embedding-3-large"],
  deepseek: ["deepseek-chat", "deepseek-coder", "deepseek-reasoner"],
  ollama: ["llama3", "llama3:70b", "mistral", "codellama", "nomic-embed-text"],
  custom: []
};

const providerBaseUrls: Record<string, string> = {
  openai: "https://api.openai.com/v1",
  deepseek: "https://api.deepseek.com/v1",
  ollama: "http://localhost:11434/v1",
  custom: ""
};

function providerMeta(type: string): ProviderInfo {
  const map: Record<string, ProviderInfo> = {
    openai: { label: "OpenAI", color: "green", icon: CloudOutlined },
    deepseek: { label: "DeepSeek", color: "blue", icon: ThunderboltOutlined },
    ollama: { label: "Ollama", color: "orange", icon: RobotOutlined },
    custom: { label: "Custom", color: "default", icon: ToolOutlined }
  };
  return map[type] ?? map["custom"];
}

const providerHints = computed(() => {
  const type = form.providerType;
  const hints: Record<string, { baseUrlPlaceholder: string; baseUrlHint: string; apiKeyHint: string }> = {
    openai: {
      baseUrlPlaceholder: "https://api.openai.com/v1",
      baseUrlHint: "OpenAI 官方 API 地址，也支持兼容代理地址",
      apiKeyHint: "以 sk- 开头的 API Key"
    },
    deepseek: {
      baseUrlPlaceholder: "https://api.deepseek.com/v1",
      baseUrlHint: "DeepSeek 官方 API 地址",
      apiKeyHint: "DeepSeek 平台颁发的 API Key"
    },
    ollama: {
      baseUrlPlaceholder: "http://localhost:11434/v1",
      baseUrlHint: "本地 Ollama 服务地址，通常为 localhost:11434",
      apiKeyHint: "Ollama 本地部署通常无需 API Key，可留空"
    },
    custom: {
      baseUrlPlaceholder: "https://your-api-endpoint.com/v1",
      baseUrlHint: "兼容 OpenAI API 格式的自定义服务地址",
      apiKeyHint: "按目标服务要求填写 API Key"
    }
  };
  return hints[type] ?? hints["custom"];
});

const suggestedModels = computed(() =>
  (providerModelSuggestions[form.providerType] ?? []).map((m) => ({ value: m }))
);

function filterModelOption(input: string, option: { value: string }) {
  return option.value.toLowerCase().includes(input.toLowerCase());
}

const drawerVisible = ref(false);
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

const baseRules = {
  name: [{ required: true, message: "请输入配置名称" }],
  providerType: [{ required: true, message: "请选择 Provider 类型" }],
  baseUrl: [{ required: true, message: "请输入 Base URL" }],
  defaultModel: [{ required: true, message: "请输入或选择默认模型" }]
};

const createRules = {
  ...baseRules,
  apiKey: [{ required: true, message: "请输入 API Key" }]
};

const currentRules = computed(() => (editingId.value ? baseRules : createRules));

function formatDate(dateStr: string) {
  if (!dateStr) return "-";
  const d = new Date(dateStr);
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

const testResultDescription = computed(() => {
  if (!testResult.value) return "";
  if (testResult.value.success) {
    return `响应延迟 ${testResult.value.latencyMs ?? 0}ms`;
  }
  return testResult.value.errorMessage || "无法连接到目标服务";
});

function onProviderChange(value: string) {
  const suggestedUrl = providerBaseUrls[value];
  if (suggestedUrl && !form.baseUrl) {
    form.baseUrl = suggestedUrl;
  }
  form.defaultModel = "";
  testResult.value = null;
}

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

function handleSearch() {
  pagination.current = 1;
  void loadData();
}

function handleReset() {
  keyword.value = "";
  pagination.current = 1;
  void loadData();
}

function resetForm() {
  Object.assign(form, {
    name: "",
    providerType: "openai",
    apiKey: "",
    baseUrl: "",
    defaultModel: "",
    supportsEmbedding: true,
    isEnabled: true
  });
  testResult.value = null;
}

function openCreate() {
  editingId.value = null;
  resetForm();
  drawerVisible.value = true;
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
    testResult.value = null;
    drawerVisible.value = true;
  } catch (error: unknown) {
    message.error((error as Error).message || "加载详情失败");
  }
}

function closeDrawer() {
  drawerVisible.value = false;
  formRef.value?.resetFields();
  testResult.value = null;
}

async function submitForm() {
  try {
    await formRef.value?.validate();
  } catch {
    return;
  }

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

    drawerVisible.value = false;
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || "提交失败");
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
  testResult.value = null;
  try {
    testResult.value = await testModelConfigConnection({
      providerType: form.providerType,
      apiKey: form.apiKey,
      baseUrl: form.baseUrl,
      model: form.defaultModel
    });
  } catch (error: unknown) {
    testResult.value = {
      success: false,
      errorMessage: (error as Error).message || "连接测试失败"
    };
  } finally {
    testing.value = false;
  }
}

onMounted(() => {
  void loadData();
});
</script>

<style scoped>
.stats-row {
  margin-bottom: 16px;
}

.stat-card {
  background: var(--color-bg-container);
  border: 1px solid var(--color-border);
  border-radius: var(--border-radius-md);
  padding: 12px 16px;
  text-align: center;
  transition: box-shadow 0.2s;
}

.stat-card:hover {
  box-shadow: var(--shadow-sm);
}

.stat-card--success {
  border-left: 3px solid var(--color-success);
}

.stat-card--warning {
  border-left: 3px solid var(--color-warning);
}

.stat-card--info {
  border-left: 3px solid #722ed1;
}

.stat-value {
  font-size: 24px;
  font-weight: 600;
  color: var(--color-text-primary);
  line-height: 1.4;
}

.stat-label {
  font-size: 13px;
  color: var(--color-text-tertiary);
  margin-top: 2px;
}

.cell-name {
  display: flex;
  align-items: center;
  gap: 6px;
}

.name-text {
  font-weight: 500;
}

.embed-tag {
  font-size: 11px;
  line-height: 18px;
  padding: 0 4px;
}

.url-text {
  font-size: 12px;
  color: var(--color-text-tertiary);
  font-family: "SF Mono", "Fira Code", monospace;
}

.api-key-masked {
  font-family: "SF Mono", "Fira Code", monospace;
  color: var(--color-text-quaternary);
  font-size: 12px;
  letter-spacing: 1px;
}

.date-text {
  font-size: 13px;
  color: var(--color-text-tertiary);
}

.field-hint {
  font-size: 12px;
  color: var(--color-text-tertiary);
}

.switch-label {
  margin-left: 8px;
  color: var(--color-text-secondary);
  font-size: 13px;
}

.test-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.test-result {
  margin-top: 4px;
}
</style>

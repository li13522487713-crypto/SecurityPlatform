<template>
  <a-card title="系统参数配置" :bordered="false">
    <div class="crud-toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          placeholder="搜索参数键或名称"
          allow-clear
          style="width: 260px"
          @search="loadConfigs"
        />
        <a-button @click="handleReset">重置</a-button>
        <a-button type="primary" @click="openCreate">新增参数</a-button>
      </a-space>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      :pagination="pagination"
      row-key="id"
      :locale="{ emptyText: '暂无参数数据' }"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'isBuiltIn'">
          <a-tag v-if="record.isBuiltIn" color="gold">
            <template #icon><LockOutlined /></template>
            内置
          </a-tag>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-button type="link" size="small" @click="openEdit(record)">编辑</a-button>
            <a-popconfirm
              v-if="!record.isBuiltIn"
              title="确认删除该参数？"
              ok-text="删除"
              cancel-text="取消"
              @confirm="handleDelete(record.id)"
            >
              <a-button type="link" danger size="small">删除</a-button>
            </a-popconfirm>
            <a-tooltip v-else title="内置参数不可删除">
              <a-button type="link" danger size="small" disabled>删除</a-button>
            </a-tooltip>
          </a-space>
        </template>
      </template>
    </a-table>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="modalVisible"
      :title="editTarget ? '编辑参数' : '新增参数'"
      :confirm-loading="modalLoading"
      @ok="submitForm"
      @cancel="closeModal"
    >
      <a-form :model="form" layout="vertical" :rules="rules" ref="formRef">
        <a-form-item label="参数键" name="configKey">
          <a-input
            v-model:value="form.configKey"
            :disabled="!!editTarget"
            placeholder="如 sys.file.upload.path"
          />
        </a-form-item>
        <a-form-item label="参数名称" name="configName">
          <a-input v-model:value="form.configName" placeholder="请输入参数名称" />
        </a-form-item>
        <a-form-item label="参数值" name="configValue">
          <a-textarea v-model:value="form.configValue" :rows="3" placeholder="请输入参数值" />
        </a-form-item>
        <a-form-item label="备注" name="remark">
          <a-textarea v-model:value="form.remark" :rows="2" placeholder="备注（选填）" />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from "vue";
import { message } from "ant-design-vue";
import { LockOutlined } from "@ant-design/icons-vue";
import type { TablePaginationConfig } from "ant-design-vue";
import {
  getSystemConfigsPaged,
  createSystemConfig,
  updateSystemConfig,
  deleteSystemConfig,
  type SystemConfigDto
} from "@/services/system-config";

const keyword = ref("");
const dataList = ref<SystemConfigDto[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  pageSizeOptions: ["10", "20", "50"]
});

const columns = [
  { title: "参数键", dataIndex: "configKey", key: "configKey", ellipsis: true },
  { title: "参数名称", dataIndex: "configName", key: "configName" },
  { title: "参数值", dataIndex: "configValue", key: "configValue", ellipsis: true },
  { title: "类型", key: "isBuiltIn", width: 90 },
  { title: "备注", dataIndex: "remark", key: "remark", ellipsis: true },
  { title: "操作", key: "actions", width: 140, fixed: "right" as const }
];

async function loadConfigs() {
  loading.value = true;
  try {
    const result = await getSystemConfigsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20,
      keyword: keyword.value || undefined
    });
    dataList.value = result.items as SystemConfigDto[];
    pagination.total = Number(result.total);
  } catch (e: any) {
    message.error(e.message || "加载失败");
  } finally {
    loading.value = false;
  }
}

function handleReset() {
  keyword.value = "";
  pagination.current = 1;
  loadConfigs();
}

function onTableChange(pag: TablePaginationConfig) {
  pagination.current = pag.current ?? 1;
  pagination.pageSize = pag.pageSize ?? 20;
  loadConfigs();
}

// ── 弹窗 ─────────────────────────────────────────────────────────────────────
const modalVisible = ref(false);
const modalLoading = ref(false);
const editTarget = ref<SystemConfigDto | null>(null);
const formRef = ref();
const form = reactive({ configKey: "", configName: "", configValue: "", remark: "" });

const rules = {
  configKey: [
    { required: true, message: "请输入参数键" },
    { pattern: /^[a-zA-Z][a-zA-Z0-9_.]{0,127}$/, message: "只允许字母、数字、点和下划线，必须以字母开头" }
  ],
  configName: [{ required: true, message: "请输入参数名称" }],
  configValue: [{ required: true, message: "请输入参数值" }]
};

function openCreate() {
  editTarget.value = null;
  Object.assign(form, { configKey: "", configName: "", configValue: "", remark: "" });
  modalVisible.value = true;
}

function openEdit(item: SystemConfigDto) {
  editTarget.value = item;
  Object.assign(form, {
    configKey: item.configKey,
    configName: item.configName,
    configValue: item.configValue,
    remark: item.remark || ""
  });
  modalVisible.value = true;
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
    if (editTarget.value) {
      await updateSystemConfig(editTarget.value.id, {
        configValue: form.configValue,
        configName: form.configName,
        remark: form.remark || undefined
      });
      message.success("更新成功");
    } else {
      await createSystemConfig({
        configKey: form.configKey,
        configValue: form.configValue,
        configName: form.configName,
        remark: form.remark || undefined
      });
      message.success("创建成功");
    }
    modalVisible.value = false;
    loadConfigs();
  } catch (e: any) {
    message.error(e.message || "操作失败");
  } finally {
    modalLoading.value = false;
  }
}

async function handleDelete(id: string) {
  try {
    await deleteSystemConfig(id);
    message.success("删除成功");
    loadConfigs();
  } catch (e: any) {
    message.error(e.message || "删除失败");
  }
}

onMounted(() => {
  loadConfigs();
});
</script>

<style scoped>
.crud-toolbar {
  margin-bottom: 16px;
}
</style>

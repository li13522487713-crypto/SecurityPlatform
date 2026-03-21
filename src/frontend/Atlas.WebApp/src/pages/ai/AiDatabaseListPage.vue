<template>
  <a-card title="数据库管理" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          placeholder="搜索数据库名称"
          style="width: 260px"
          @search="loadData"
        />
        <a-button @click="handleReset">重置</a-button>
        <a-button type="primary" @click="openCreate">新建数据库</a-button>
      </a-space>
    </div>

    <a-table
      row-key="id"
      :columns="columns"
      :data-source="list"
      :loading="loading"
      :pagination="false"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'botId'">
          <a-tag v-if="record.botId" color="blue">Bot {{ record.botId }}</a-tag>
          <span v-else>-</span>
        </template>
        <template v-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="goDetail(record.id)">详情</a-button>
            <a-button type="link" @click="openEdit(record.id)">编辑</a-button>
            <a-button type="link" @click="openImport(record.id)">导入</a-button>
            <a-popconfirm title="确认删除该数据库？" @confirm="handleDelete(record.id)">
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
      v-model:open="modalVisible"
      :title="editingId ? '编辑数据库' : '新建数据库'"
      :confirm-loading="modalLoading"
      width="760px"
      @ok="submitForm"
      @cancel="closeModal"
    >
      <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
        <a-form-item label="名称" name="name">
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-textarea v-model:value="form.description" :rows="3" />
        </a-form-item>
        <a-form-item label="绑定 Bot ID" name="botId">
          <a-input-number v-model:value="form.botId" :min="1" style="width: 100%" />
        </a-form-item>
        <a-form-item label="Schema(JSON)" name="tableSchema">
          <a-textarea
            v-model:value="form.tableSchema"
            :rows="8"
            placeholder='[{"name":"column1","type":"string"}]'
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <database-import-modal
      :open="importOpen"
      :database-id="importDatabaseId"
      @cancel="closeImport"
      @success="handleImportSuccess"
    />
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useRouter } from "vue-router";
import DatabaseImportModal from "@/components/ai/database/DatabaseImportModal.vue";
import {
  createAiDatabase,
  deleteAiDatabase,
  getAiDatabaseById,
  getAiDatabasesPaged,
  updateAiDatabase,
  type AiDatabaseListItem
} from "@/services/api-ai-database";

const router = useRouter();
const list = ref<AiDatabaseListItem[]>([]);
const loading = ref(false);
const keyword = ref("");
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const columns = [
  { title: "名称", dataIndex: "name", key: "name", width: 220 },
  { title: "描述", dataIndex: "description", key: "description", ellipsis: true },
  { title: "记录数", dataIndex: "recordCount", key: "recordCount", width: 100 },
  { title: "Bot", key: "botId", width: 120 },
  { title: "更新时间", dataIndex: "updatedAt", key: "updatedAt", width: 200 },
  { title: "操作", key: "action", width: 260 }
];

const modalVisible = ref(false);
const modalLoading = ref(false);
const editingId = ref<number | null>(null);
const formRef = ref<FormInstance>();
const form = reactive({
  name: "",
  description: "",
  botId: undefined as number | undefined,
  tableSchema: '[{"name":"id","type":"string"},{"name":"value","type":"string"}]'
});

const rules = {
  name: [{ required: true, message: "请输入数据库名称" }],
  tableSchema: [{ required: true, message: "请输入 Schema" }]
};

const importOpen = ref(false);
const importDatabaseId = ref(0);

async function loadData() {
  loading.value = true;
  try {
    const result  = await getAiDatabasesPaged(
      { pageIndex: pageIndex.value, pageSize: pageSize.value },
      keyword.value || undefined
    );

    if (!isMounted.value) return;
    list.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || "加载数据库失败");
  } finally {
    loading.value = false;
  }
}

function handleReset() {
  keyword.value = "";
  pageIndex.value = 1;
  void loadData();
}

function goDetail(id: number) {
  void router.push(`/ai/databases/${id}`);
}

function openCreate() {
  editingId.value = null;
  Object.assign(form, {
    name: "",
    description: "",
    botId: undefined,
    tableSchema: '[{"name":"id","type":"string"},{"name":"value","type":"string"}]'
  });
  modalVisible.value = true;
}

async function openEdit(id: number) {
  try {
    const detail  = await getAiDatabaseById(id);

    if (!isMounted.value) return;
    editingId.value = id;
    Object.assign(form, {
      name: detail.name,
      description: detail.description ?? "",
      botId: detail.botId,
      tableSchema: detail.tableSchema
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

    if (!isMounted.value) return;
  } catch {
    return;
  }

  modalLoading.value = true;
  try {
    if (editingId.value) {
      await updateAiDatabase(editingId.value, {
        name: form.name,
        description: form.description || undefined,
        botId: form.botId,
        tableSchema: form.tableSchema
      });

      if (!isMounted.value) return;
      message.success("更新成功");
    } else {
      await createAiDatabase({
        name: form.name,
        description: form.description || undefined,
        botId: form.botId,
        tableSchema: form.tableSchema
      });

      if (!isMounted.value) return;
      message.success("创建成功");
    }

    modalVisible.value = false;
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || "提交失败");
  } finally {
    modalLoading.value = false;
  }
}

async function handleDelete(id: number) {
  try {
    await deleteAiDatabase(id);

    if (!isMounted.value) return;
    message.success("删除成功");
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || "删除失败");
  }
}

function openImport(databaseId: number) {
  importDatabaseId.value = databaseId;
  importOpen.value = true;
}

function closeImport() {
  importOpen.value = false;
}

function handleImportSuccess() {
  importOpen.value = false;
  void loadData();
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

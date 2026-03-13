<template>
  <a-card title="知识库管理" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          placeholder="搜索知识库名称"
          style="width: 260px"
          @search="loadData"
        />
        <a-button type="primary" @click="openCreate">新建知识库</a-button>
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
        <template v-if="column.key === 'type'">
          <a-tag :color="typeColor(record.type)">{{ typeLabel(record.type) }}</a-tag>
        </template>
        <template v-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="goDetail(record.id)">详情</a-button>
            <a-popconfirm title="确认删除该知识库？" @confirm="handleDelete(record.id)">
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
      title="新建知识库"
      :confirm-loading="modalLoading"
      @ok="submitCreate"
      @cancel="closeModal"
    >
      <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
        <a-form-item label="名称" name="name">
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-textarea v-model:value="form.description" :rows="3" />
        </a-form-item>
        <a-form-item label="类型" name="type">
          <a-select v-model:value="form.type" :options="typeOptions" />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import type { FormInstance } from "ant-design-vue";
import {
  createKnowledgeBase,
  deleteKnowledgeBase,
  getKnowledgeBasesPaged,
  type KnowledgeBaseDto
} from "@/services/api-knowledge";

const router = useRouter();
const list = ref<KnowledgeBaseDto[]>([]);
const loading = ref(false);
const keyword = ref("");
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const columns = [
  { title: "名称", dataIndex: "name", key: "name" },
  { title: "类型", dataIndex: "type", key: "type", width: 120 },
  { title: "文档数", dataIndex: "documentCount", key: "documentCount", width: 120 },
  { title: "分片数", dataIndex: "chunkCount", key: "chunkCount", width: 120 },
  { title: "创建时间", dataIndex: "createdAt", key: "createdAt", width: 220 },
  { title: "操作", key: "action", width: 180 }
];

const typeOptions = [
  { label: "文本", value: 0 },
  { label: "表格", value: 1 },
  { label: "图片", value: 2 }
];

const modalVisible = ref(false);
const modalLoading = ref(false);
const formRef = ref<FormInstance>();
const form = reactive({
  name: "",
  description: "",
  type: 0 as 0 | 1 | 2
});

const rules = {
  name: [{ required: true, message: "请输入知识库名称" }]
};

function typeLabel(type: number) {
  if (type === 1) return "表格";
  if (type === 2) return "图片";
  return "文本";
}

function typeColor(type: number) {
  if (type === 1) return "purple";
  if (type === 2) return "orange";
  return "blue";
}

async function loadData() {
  loading.value = true;
  try {
    const result = await getKnowledgeBasesPaged(
      { pageIndex: pageIndex.value, pageSize: pageSize.value },
      keyword.value || undefined
    );
    list.value = result.items;
    total.value = Number(result.total);
  } catch (err: unknown) {
    message.error((err as Error).message || "加载知识库失败");
  } finally {
    loading.value = false;
  }
}

function goDetail(id: number) {
  void router.push(`/ai/knowledge-bases/${id}`);
}

function openCreate() {
  Object.assign(form, { name: "", description: "", type: 0 });
  modalVisible.value = true;
}

function closeModal() {
  modalVisible.value = false;
  formRef.value?.resetFields();
}

async function submitCreate() {
  try {
    await formRef.value?.validate();
  } catch {
    return;
  }

  modalLoading.value = true;
  try {
    await createKnowledgeBase({
      name: form.name,
      description: form.description || undefined,
      type: form.type
    });
    message.success("创建成功");
    modalVisible.value = false;
    await loadData();
  } catch (err: unknown) {
    message.error((err as Error).message || "创建失败");
  } finally {
    modalLoading.value = false;
  }
}

async function handleDelete(id: number) {
  try {
    await deleteKnowledgeBase(id);
    message.success("删除成功");
    await loadData();
  } catch (err: unknown) {
    message.error((err as Error).message || "删除失败");
  }
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

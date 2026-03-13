<template>
  <a-card title="AI 工作流" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          placeholder="搜索工作流名称"
          style="width: 260px"
          @search="loadData"
        />
        <a-button type="primary" @click="openCreate">新建工作流</a-button>
      </a-space>
    </div>

    <a-table row-key="id" :columns="columns" :data-source="list" :loading="loading" :pagination="false">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="statusColor(record.status)">{{ record.status }}</a-tag>
        </template>
        <template v-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="goEditor(record.id)">编辑</a-button>
            <a-button type="link" @click="handlePublish(record.id)">发布</a-button>
            <a-button type="link" @click="handleCopy(record.id)">复制</a-button>
            <a-popconfirm title="确认删除该工作流？" @confirm="handleDelete(record.id)">
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
      title="新建工作流"
      :confirm-loading="modalLoading"
      @ok="submitCreate"
      @cancel="closeModal"
    >
      <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
        <a-form-item label="名称" name="name">
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="form.description" :rows="3" />
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
  copyAiWorkflow,
  createAiWorkflow,
  deleteAiWorkflow,
  getAiWorkflowsPaged,
  publishAiWorkflow,
  type AiWorkflowDefinitionDto
} from "@/services/api-ai-workflow";

const router = useRouter();
const list = ref<AiWorkflowDefinitionDto[]>([]);
const loading = ref(false);
const keyword = ref("");
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const columns = [
  { title: "名称", dataIndex: "name", key: "name" },
  { title: "状态", dataIndex: "status", key: "status", width: 120 },
  { title: "版本", dataIndex: "publishVersion", key: "publishVersion", width: 100 },
  { title: "更新时间", dataIndex: "updatedAt", key: "updatedAt", width: 220 },
  { title: "操作", key: "action", width: 280 }
];

const modalVisible = ref(false);
const modalLoading = ref(false);
const formRef = ref<FormInstance>();
const form = reactive({
  name: "",
  description: ""
});

const rules = {
  name: [{ required: true, message: "请输入工作流名称" }]
};

function statusColor(status: string) {
  if (status === "Published") return "green";
  if (status === "Disabled") return "default";
  return "blue";
}

async function loadData() {
  loading.value = true;
  try {
    const result = await getAiWorkflowsPaged(
      { pageIndex: pageIndex.value, pageSize: pageSize.value },
      keyword.value || undefined
    );
    list.value = result.items;
    total.value = Number(result.total);
  } catch (err: unknown) {
    message.error((err as Error).message || "加载工作流失败");
  } finally {
    loading.value = false;
  }
}

function goEditor(id: number) {
  void router.push(`/ai/workflows/${id}/edit`);
}

function openCreate() {
  Object.assign(form, { name: "", description: "" });
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
    const id = await createAiWorkflow({
      name: form.name,
      description: form.description || undefined,
      canvasJson: JSON.stringify({ nodes: [], edges: [] }),
      definitionJson: "{}"
    });
    message.success("创建成功");
    modalVisible.value = false;
    await loadData();
    goEditor(id);
  } catch (err: unknown) {
    message.error((err as Error).message || "创建失败");
  } finally {
    modalLoading.value = false;
  }
}

async function handleDelete(id: number) {
  try {
    await deleteAiWorkflow(id);
    message.success("删除成功");
    await loadData();
  } catch (err: unknown) {
    message.error((err as Error).message || "删除失败");
  }
}

async function handlePublish(id: number) {
  try {
    await publishAiWorkflow(id);
    message.success("发布成功");
    await loadData();
  } catch (err: unknown) {
    message.error((err as Error).message || "发布失败");
  }
}

async function handleCopy(id: number) {
  try {
    await copyAiWorkflow(id);
    message.success("复制成功");
    await loadData();
  } catch (err: unknown) {
    message.error((err as Error).message || "复制失败");
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

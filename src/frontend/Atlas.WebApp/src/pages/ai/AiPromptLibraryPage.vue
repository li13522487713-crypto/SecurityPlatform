<template>
  <a-card title="Prompt 模板库" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          placeholder="搜索 Prompt 名称"
          style="width: 260px"
          @search="loadData"
        />
        <a-input
          v-model:value="category"
          placeholder="分类筛选"
          style="width: 180px"
          allow-clear
          @press-enter="loadData"
        />
        <a-button @click="handleReset">重置</a-button>
        <a-button @click="openInsertPreview">插入预览</a-button>
        <a-button type="primary" @click="openCreate">新增模板</a-button>
      </a-space>
    </div>

    <a-table row-key="id" :columns="columns" :data-source="list" :loading="loading" :pagination="false">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'content'">
          <a-typography-paragraph :ellipsis="{ rows: 2, expandable: true, symbol: '展开' }">
            {{ record.content }}
          </a-typography-paragraph>
        </template>
        <template v-if="column.key === 'tags'">
          <a-space wrap>
            <a-tag v-for="tag in record.tags" :key="tag">{{ tag }}</a-tag>
            <span v-if="record.tags.length === 0">-</span>
          </a-space>
        </template>
        <template v-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="openEdit(record.id)">编辑</a-button>
            <a-popconfirm title="确认删除该模板？" @confirm="handleDelete(record.id)">
              <a-button type="link" danger :disabled="record.isSystem">删除</a-button>
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
      :title="editingId ? '编辑 Prompt 模板' : '新增 Prompt 模板'"
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
          <a-textarea v-model:value="form.description" :rows="2" />
        </a-form-item>
        <a-form-item label="分类" name="category">
          <a-input v-model:value="form.category" />
        </a-form-item>
        <a-form-item label="标签（逗号分隔）" name="tags">
          <a-input v-model:value="tagsText" />
        </a-form-item>
        <a-form-item label="内容" name="content">
          <a-textarea v-model:value="form.content" :rows="10" />
        </a-form-item>
        <a-form-item v-if="!editingId" label="系统模板">
          <a-switch v-model:checked="form.isSystem" />
        </a-form-item>
      </a-form>
    </a-modal>

    <prompt-insert-modal
      :open="insertModalOpen"
      @cancel="insertModalOpen = false"
      @insert="handleInsertPrompt"
    />

    <a-modal
      v-model:open="insertPreviewOpen"
      title="插入结果预览"
      :footer="null"
      width="760px"
    >
      <pre class="preview-block">{{ insertedContent }}</pre>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import PromptInsertModal from "@/components/ai/PromptInsertModal.vue";
import {
  createAiPromptTemplate,
  deleteAiPromptTemplate,
  getAiPromptTemplateById,
  getAiPromptTemplatesPaged,
  updateAiPromptTemplate,
  type AiPromptTemplateListItem
} from "@/services/api-ai-prompt";

const keyword = ref("");
const category = ref("");
const list = ref<AiPromptTemplateListItem[]>([]);
const loading = ref(false);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const columns = [
  { title: "名称", dataIndex: "name", key: "name", width: 220 },
  { title: "分类", dataIndex: "category", key: "category", width: 140 },
  { title: "标签", key: "tags", width: 220 },
  { title: "内容", key: "content" },
  { title: "操作", key: "action", width: 140 }
];

const modalOpen = ref(false);
const modalLoading = ref(false);
const editingId = ref<number | null>(null);
const formRef = ref<FormInstance>();
const form = reactive({
  name: "",
  description: "",
  category: "",
  content: "",
  isSystem: false
});
const tagsText = ref("");
const rules = {
  name: [{ required: true, message: "请输入名称" }],
  content: [{ required: true, message: "请输入内容" }]
};

const insertModalOpen = ref(false);
const insertPreviewOpen = ref(false);
const insertedContent = ref("");

async function loadData() {
  loading.value = true;
  try {
    const result = await getAiPromptTemplatesPaged(
      { pageIndex: pageIndex.value, pageSize: pageSize.value, keyword: keyword.value || undefined },
      category.value || undefined
    );
    list.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || "加载 Prompt 模板失败");
  } finally {
    loading.value = false;
  }
}

function handleReset() {
  keyword.value = "";
  category.value = "";
  pageIndex.value = 1;
  void loadData();
}

function openCreate() {
  editingId.value = null;
  Object.assign(form, {
    name: "",
    description: "",
    category: "",
    content: "",
    isSystem: false
  });
  tagsText.value = "";
  modalOpen.value = true;
}

async function openEdit(id: number) {
  try {
    const detail = await getAiPromptTemplateById(id);
    editingId.value = id;
    Object.assign(form, {
      name: detail.name,
      description: detail.description ?? "",
      category: detail.category ?? "",
      content: detail.content,
      isSystem: detail.isSystem
    });
    tagsText.value = detail.tags.join(",");
    modalOpen.value = true;
  } catch (error: unknown) {
    message.error((error as Error).message || "加载 Prompt 详情失败");
  }
}

function closeModal() {
  modalOpen.value = false;
  formRef.value?.resetFields();
}

function parseTags() {
  return tagsText.value
    .split(",")
    .map((x) => x.trim())
    .filter((x) => x.length > 0);
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
      await updateAiPromptTemplate(editingId.value, {
        name: form.name,
        description: form.description || undefined,
        category: form.category || undefined,
        content: form.content,
        tags: parseTags()
      });
      message.success("更新成功");
    } else {
      await createAiPromptTemplate({
        name: form.name,
        description: form.description || undefined,
        category: form.category || undefined,
        content: form.content,
        tags: parseTags(),
        isSystem: form.isSystem
      });
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
    await deleteAiPromptTemplate(id);
    message.success("删除成功");
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || "删除失败");
  }
}

function openInsertPreview() {
  insertModalOpen.value = true;
}

function handleInsertPrompt(content: string) {
  insertModalOpen.value = false;
  insertedContent.value = content;
  insertPreviewOpen.value = true;
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

.preview-block {
  margin: 0;
  padding: 12px;
  border-radius: 8px;
  background: #fafafa;
  max-height: 400px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-all;
}
</style>

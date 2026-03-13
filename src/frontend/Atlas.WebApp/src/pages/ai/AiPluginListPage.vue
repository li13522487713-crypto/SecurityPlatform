<template>
  <a-card title="插件管理" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          placeholder="搜索插件名称"
          style="width: 260px"
          @search="loadData"
        />
        <a-button @click="showBuiltInMetadata">内置插件</a-button>
        <a-button @click="handleReset">重置</a-button>
        <a-button type="primary" @click="openCreate">新建插件</a-button>
      </a-space>
    </div>

    <a-table row-key="id" :columns="columns" :data-source="list" :loading="loading" :pagination="false">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'type'">
          <a-tag :color="record.type === 1 ? 'purple' : 'blue'">
            {{ record.type === 1 ? "内置" : "自定义" }}
          </a-tag>
        </template>
        <template v-if="column.key === 'status'">
          <a-tag :color="record.status === 1 ? 'green' : 'default'">
            {{ record.status === 1 ? "已发布" : "草稿" }}
          </a-tag>
        </template>
        <template v-if="column.key === 'lock'">
          <a-tag :color="record.isLocked ? 'red' : 'default'">
            {{ record.isLocked ? "已锁定" : "未锁定" }}
          </a-tag>
        </template>
        <template v-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="goDetail(record.id)">详情</a-button>
            <a-button type="link" @click="openEdit(record.id)">编辑</a-button>
            <a-popconfirm title="确认删除该插件？" @confirm="handleDelete(record.id)">
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
      :title="editingId ? '编辑插件' : '新建插件'"
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
        <a-form-item label="图标" name="icon">
          <a-input v-model:value="form.icon" />
        </a-form-item>
        <a-form-item label="分类" name="category">
          <a-input v-model:value="form.category" />
        </a-form-item>
        <a-form-item label="类型" name="type">
          <a-select v-model:value="form.type" :options="typeOptions" />
        </a-form-item>
        <a-form-item label="定义 JSON" name="definitionJson">
          <a-textarea v-model:value="form.definitionJson" :rows="8" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-drawer v-model:open="builtInDrawerOpen" title="内置插件元数据" width="760">
      <a-table row-key="code" :columns="builtInColumns" :data-source="builtInList" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'tags'">
            <a-space wrap>
              <a-tag v-for="tag in record.tags" :key="tag">{{ tag }}</a-tag>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-drawer>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { useRouter } from "vue-router";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import {
  createAiPlugin,
  deleteAiPlugin,
  getAiPluginBuiltInMetadata,
  getAiPluginById,
  getAiPluginsPaged,
  updateAiPlugin,
  type AiPluginBuiltInMetaItem,
  type AiPluginListItem,
  type AiPluginType
} from "@/services/api-ai-plugin";

const router = useRouter();
const keyword = ref("");
const list = ref<AiPluginListItem[]>([]);
const loading = ref(false);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const columns = [
  { title: "名称", dataIndex: "name", key: "name", width: 220 },
  { title: "分类", dataIndex: "category", key: "category", width: 140 },
  { title: "类型", key: "type", width: 100 },
  { title: "状态", key: "status", width: 100 },
  { title: "锁定", key: "lock", width: 100 },
  { title: "更新时间", dataIndex: "updatedAt", key: "updatedAt", width: 200 },
  { title: "操作", key: "action", width: 180 }
];

const typeOptions = [
  { label: "自定义", value: 0 },
  { label: "内置", value: 1 }
];

const modalOpen = ref(false);
const modalLoading = ref(false);
const editingId = ref<number | null>(null);
const formRef = ref<FormInstance>();
const form = reactive({
  name: "",
  description: "",
  icon: "",
  category: "",
  type: 0 as AiPluginType,
  definitionJson: "{}"
});
const rules = {
  name: [{ required: true, message: "请输入插件名称" }]
};

const builtInDrawerOpen = ref(false);
const builtInList = ref<AiPluginBuiltInMetaItem[]>([]);
const builtInColumns = [
  { title: "Code", dataIndex: "code", key: "code", width: 180 },
  { title: "名称", dataIndex: "name", key: "name", width: 150 },
  { title: "分类", dataIndex: "category", key: "category", width: 120 },
  { title: "描述", dataIndex: "description", key: "description" },
  { title: "标签", key: "tags", width: 220 }
];

async function loadData() {
  loading.value = true;
  try {
    const result = await getAiPluginsPaged(
      { pageIndex: pageIndex.value, pageSize: pageSize.value },
      keyword.value || undefined
    );
    list.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || "加载插件列表失败");
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
  void router.push(`/ai/plugins/${id}`);
}

function openCreate() {
  editingId.value = null;
  Object.assign(form, {
    name: "",
    description: "",
    icon: "",
    category: "",
    type: 0 as AiPluginType,
    definitionJson: "{}"
  });
  modalOpen.value = true;
}

async function openEdit(id: number) {
  try {
    const detail = await getAiPluginById(id);
    editingId.value = id;
    Object.assign(form, {
      name: detail.name,
      description: detail.description ?? "",
      icon: detail.icon ?? "",
      category: detail.category ?? "",
      type: detail.type,
      definitionJson: detail.definitionJson
    });
    modalOpen.value = true;
  } catch (error: unknown) {
    message.error((error as Error).message || "加载插件详情失败");
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
      name: form.name,
      description: form.description || undefined,
      icon: form.icon || undefined,
      category: form.category || undefined,
      type: form.type,
      definitionJson: form.definitionJson || undefined
    };

    if (editingId.value) {
      await updateAiPlugin(editingId.value, payload);
      message.success("更新成功");
    } else {
      await createAiPlugin(payload);
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
    await deleteAiPlugin(id);
    message.success("删除成功");
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || "删除失败");
  }
}

async function showBuiltInMetadata() {
  try {
    builtInList.value = await getAiPluginBuiltInMetadata();
    builtInDrawerOpen.value = true;
  } catch (error: unknown) {
    message.error((error as Error).message || "加载内置插件失败");
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

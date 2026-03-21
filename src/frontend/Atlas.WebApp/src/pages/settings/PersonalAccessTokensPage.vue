<template>
  <a-card title="个人访问令牌（PAT）" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          placeholder="搜索令牌名称"
          style="width: 260px"
          @search="loadData"
        />
        <a-button @click="handleReset">重置</a-button>
        <a-button type="primary" @click="openCreate">创建令牌</a-button>
      </a-space>
    </div>

    <a-table row-key="id" :columns="columns" :data-source="list" :loading="loading" :pagination="false">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'scopes'">
          <a-space wrap>
            <a-tag v-for="scope in record.scopes" :key="scope">{{ scope }}</a-tag>
            <span v-if="record.scopes.length === 0">-</span>
          </a-space>
        </template>
        <template v-if="column.key === 'status'">
          <a-tag :color="record.revokedAt ? 'red' : 'green'">
            {{ record.revokedAt ? "已撤销" : "有效" }}
          </a-tag>
        </template>
        <template v-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="openEdit(record)">编辑</a-button>
            <a-popconfirm title="确认撤销该令牌？" @confirm="handleRevoke(record.id)">
              <a-button type="link" danger :disabled="!!record.revokedAt">撤销</a-button>
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
      :title="editingId ? '编辑 PAT' : '创建 PAT'"
      :confirm-loading="modalLoading"
      @ok="submitForm"
      @cancel="closeModal"
    >
      <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
        <a-form-item label="名称" name="name">
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item label="Scopes（逗号分隔）" name="scopesText">
          <a-input v-model:value="form.scopesText" placeholder="open:chat,open:workflow" />
        </a-form-item>
        <a-form-item label="过期时间（可选）">
          <a-date-picker
            v-model:value="form.expiresAt"
            show-time
            value-format="YYYY-MM-DDTHH:mm:ssZ"
            style="width: 100%"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="tokenModalOpen"
      title="PAT 创建成功"
      :footer="null"
      width="760"
    >
      <a-alert
        type="warning"
        show-icon
        message="请立即复制并妥善保存该 Token，关闭后将无法再次查看明文。"
        style="margin-bottom: 12px"
      />
      <a-typography-paragraph copyable>{{ createdToken }}</a-typography-paragraph>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import dayjs from "dayjs";
import {
  createPersonalAccessToken,
  getPersonalAccessTokensPaged,
  revokePersonalAccessToken,
  updatePersonalAccessToken,
  type PersonalAccessTokenListItem
} from "@/services/api-pat";

const keyword = ref("");
const list = ref<PersonalAccessTokenListItem[]>([]);
const loading = ref(false);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const columns = [
  { title: "名称", dataIndex: "name", key: "name", width: 220 },
  { title: "前缀", dataIndex: "tokenPrefix", key: "tokenPrefix", width: 180 },
  { title: "权限范围", key: "scopes" },
  { title: "状态", key: "status", width: 100 },
  { title: "过期时间", dataIndex: "expiresAt", key: "expiresAt", width: 220 },
  { title: "操作", key: "action", width: 140 }
];

const modalOpen = ref(false);
const modalLoading = ref(false);
const editingId = ref<number | null>(null);
const formRef = ref<FormInstance>();
const form = reactive({
  name: "",
  scopesText: "",
  expiresAt: undefined as string | undefined
});
const rules = {
  name: [{ required: true, message: "请输入名称" }]
};

const tokenModalOpen = ref(false);
const createdToken = ref("");

async function loadData() {
  loading.value = true;
  try {
    const result  = await getPersonalAccessTokensPaged(
      {
        pageIndex: pageIndex.value,
        pageSize: pageSize.value,
        keyword: keyword.value || undefined
      },
      keyword.value || undefined
    );

    if (!isMounted.value) return;
    list.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || "加载 PAT 列表失败");
  } finally {
    loading.value = false;
  }
}

function handleReset() {
  keyword.value = "";
  pageIndex.value = 1;
  void loadData();
}

function openCreate() {
  editingId.value = null;
  Object.assign(form, {
    name: "",
    scopesText: "",
    expiresAt: undefined
  });
  modalOpen.value = true;
}

function openEdit(record: PersonalAccessTokenListItem) {
  editingId.value = record.id;
  Object.assign(form, {
    name: record.name,
    scopesText: record.scopes.join(","),
    expiresAt: record.expiresAt ? dayjs(record.expiresAt).format("YYYY-MM-DDTHH:mm:ssZ") : undefined
  });
  modalOpen.value = true;
}

function closeModal() {
  modalOpen.value = false;
  formRef.value?.resetFields();
}

function parseScopes() {
  return form.scopesText
    .split(",")
    .map((x) => x.trim())
    .filter((x) => x.length > 0);
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
      await updatePersonalAccessToken(editingId.value, {
        name: form.name,
        scopes: parseScopes(),
        expiresAt: form.expiresAt || undefined
      });

      if (!isMounted.value) return;
      message.success("更新成功");
    } else {
      const result  = await createPersonalAccessToken({
        name: form.name,
        scopes: parseScopes(),
        expiresAt: form.expiresAt || undefined
      });

      if (!isMounted.value) return;
      createdToken.value = result.plainTextToken;
      tokenModalOpen.value = true;
      message.success("创建成功");
    }

    modalOpen.value = false;
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || "提交失败");
  } finally {
    modalLoading.value = false;
  }
}

async function handleRevoke(id: number) {
  try {
    await revokePersonalAccessToken(id);

    if (!isMounted.value) return;
    message.success("撤销成功");
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || "撤销失败");
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

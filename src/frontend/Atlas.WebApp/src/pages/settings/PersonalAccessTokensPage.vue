<template>
  <a-card :title="t('settings.pat.cardTitle')" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('settings.pat.searchPlaceholder')"
          style="width: 260px"
          @search="loadData"
        />
        <a-button @click="handleReset">{{ t("settings.pat.reset") }}</a-button>
        <a-button type="primary" @click="openCreate">{{ t("settings.pat.create") }}</a-button>
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
            {{ record.revokedAt ? t("settings.pat.statusRevoked") : t("settings.pat.statusActive") }}
          </a-tag>
        </template>
        <template v-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="openEdit(record)">{{ t("settings.pat.edit") }}</a-button>
            <a-popconfirm :title="t('settings.pat.revokeConfirm')" @confirm="handleRevoke(record.id)">
              <a-button type="link" danger :disabled="!!record.revokedAt">{{ t("settings.pat.revoke") }}</a-button>
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
      :title="editingId ? t('settings.pat.modalEdit') : t('settings.pat.modalCreate')"
      :confirm-loading="modalLoading"
      @ok="submitForm"
      @cancel="closeModal"
    >
      <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
        <a-form-item :label="t('settings.pat.labelName')" name="name">
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item :label="t('settings.pat.labelScopes')" name="scopesText">
          <a-input v-model:value="form.scopesText" :placeholder="t('settings.pat.scopesTextPlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('settings.pat.labelExpires')">
          <a-date-picker
            v-model:value="form.expiresAt"
            show-time
            value-format="YYYY-MM-DDTHH:mm:ssZ"
            style="width: 100%"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal v-model:open="tokenModalOpen" :title="t('settings.pat.successTitle')" :footer="null" width="760">
      <a-alert type="warning" show-icon :message="t('settings.pat.successMsg')" style="margin-bottom: 12px" />
      <a-typography-paragraph copyable>{{ createdToken }}</a-typography-paragraph>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => {
  isMounted.value = true;
});
onUnmounted(() => {
  isMounted.value = false;
});

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

const { t } = useI18n();

const keyword = ref("");
const list = ref<PersonalAccessTokenListItem[]>([]);
const loading = ref(false);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const columns = computed(() => [
  { title: t("settings.pat.labelName"), dataIndex: "name", key: "name", width: 220 },
  { title: t("settings.pat.colPrefix"), dataIndex: "tokenPrefix", key: "tokenPrefix", width: 180 },
  { title: t("settings.pat.colScopes"), key: "scopes" },
  { title: t("settings.pat.colStatus"), key: "status", width: 100 },
  { title: t("settings.pat.colExpires"), dataIndex: "expiresAt", key: "expiresAt", width: 220 },
  { title: t("settings.pat.colActions"), key: "action", width: 140 }
]);

const modalOpen = ref(false);
const modalLoading = ref(false);
const editingId = ref<number | null>(null);
const formRef = ref<FormInstance>();
const form = reactive({
  name: "",
  scopesText: "",
  expiresAt: undefined as string | undefined
});
const rules = computed(() => ({
  name: [{ required: true, message: t("settings.pat.ruleName") }]
}));

const tokenModalOpen = ref(false);
const createdToken = ref("");

async function loadData() {
  loading.value = true;
  try {
    const result = await getPersonalAccessTokensPaged(
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
    message.error((error as Error).message || t("settings.pat.loadFailed"));
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
      message.success(t("settings.pat.updateOk"));
    } else {
      const result = await createPersonalAccessToken({
        name: form.name,
        scopes: parseScopes(),
        expiresAt: form.expiresAt || undefined
      });

      if (!isMounted.value) return;
      createdToken.value = result.plainTextToken;
      tokenModalOpen.value = true;
      message.success(t("settings.pat.createOk"));
    }

    modalOpen.value = false;
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("settings.pat.submitFailed"));
  } finally {
    modalLoading.value = false;
  }
}

async function handleRevoke(id: number) {
  try {
    await revokePersonalAccessToken(id);

    if (!isMounted.value) return;
    message.success(t("settings.pat.revokeOk"));
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("settings.pat.revokeFailed"));
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

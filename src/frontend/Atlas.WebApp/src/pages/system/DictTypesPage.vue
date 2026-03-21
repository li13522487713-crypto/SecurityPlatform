<template>
  <div class="dict-page">
    <a-row :gutter="16">
      <!-- 左侧：字典类型列表 -->
      <a-col :span="8">
        <a-card :title="t('dict.titleTypes')" :bordered="false">
          <template #extra>
            <a-button type="primary" size="small" @click="openCreateType">{{ t("common.create") }}</a-button>
          </template>
          <div style="margin-bottom: 12px">
            <a-input-search
              v-model:value="typeKeyword"
              :placeholder="t('dict.searchTypePlaceholder')"
              allow-clear
              @search="loadTypes"
              @press-enter="loadTypes"
            />
          </div>
          <a-spin :spinning="typeLoading">
            <a-list
              :data-source="typeList"
              :locale="{ emptyText: t('dict.emptyTypes') }"
            >
              <template #renderItem="{ item }">
                <a-list-item
                  :class="{ 'dict-type-item-active': selectedType?.id === item.id }"
                  class="dict-type-item"
                  @click="selectType(item)"
                >
                  <a-list-item-meta>
                    <template #title>
                      <span>{{ item.name }}</span>
                      <a-tag v-if="!item.status" color="default" style="margin-left: 6px">{{ t("dict.statusDisabled") }}</a-tag>
                    </template>
                    <template #description>{{ item.code }}</template>
                  </a-list-item-meta>
                  <template #actions>
                    <a-button type="link" size="small" @click.stop="openEditType(item)">{{ t("common.edit") }}</a-button>
                    <a-popconfirm
                      :title="t('dict.deleteTypeConfirm')"
                      :ok-text="t('common.delete')"
                      :cancel-text="t('common.cancel')"
                      @confirm="handleDeleteType(item.id)"
                    >
                      <a-button type="link" danger size="small" @click.stop>{{ t("common.delete") }}</a-button>
                    </a-popconfirm>
                  </template>
                </a-list-item>
              </template>
            </a-list>
            <a-pagination
              v-if="typePagination.total > typePagination.pageSize"
              v-model:current="typePagination.pageIndex"
              :page-size="typePagination.pageSize"
              :total="typePagination.total"
              size="small"
              style="margin-top: 12px; text-align: right"
              @change="loadTypes"
            />
          </a-spin>
        </a-card>
      </a-col>

      <!-- 右侧：字典数据 -->
      <a-col :span="16">
        <a-card
          :title="selectedType ? t('dict.titleDataWithType', { name: selectedType.name, code: selectedType.code }) : t('dict.titleData')"
          :bordered="false"
        >
          <template #extra>
            <a-button
              v-if="selectedType"
              type="primary"
              size="small"
              @click="openCreateData"
            >{{ t("dict.createData") }}</a-button>
          </template>

          <a-empty v-if="!selectedType" :description="t('dict.selectTypeHint')" />

          <template v-else>
            <div style="margin-bottom: 12px">
              <a-input-search
                v-model:value="dataKeyword"
                :placeholder="t('dict.searchDataPlaceholder')"
                allow-clear
                @search="loadData"
              />
            </div>
            <a-table
              :columns="dataColumns"
              :data-source="dataList"
              :loading="dataLoading"
              :pagination="dataPagination"
              row-key="id"
              size="small"
              :locale="{ emptyText: t('dict.emptyData') }"
              @change="onDataTableChange"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.key === 'status'">
                  <a-tag :color="record.status ? 'success' : 'default'">
                    {{ record.status ? t("dict.statusEnabled") : t("dict.statusDisabled") }}
                  </a-tag>
                </template>
                <template v-else-if="column.key === 'listClass'">
                  <a-tag v-if="record.listClass" :color="record.listClass">{{ record.label }}</a-tag>
                  <span v-else>-</span>
                </template>
                <template v-else-if="column.key === 'actions'">
                  <a-space>
                    <a-button type="link" size="small" @click="openEditData(record)">{{ t("common.edit") }}</a-button>
                    <a-popconfirm
                      :title="t('dict.deleteDataConfirm')"
                      :ok-text="t('common.delete')"
                      :cancel-text="t('common.cancel')"
                      @confirm="handleDeleteData(record.id)"
                    >
                      <a-button type="link" danger size="small">{{ t("common.delete") }}</a-button>
                    </a-popconfirm>
                  </a-space>
                </template>
              </template>
            </a-table>
          </template>
        </a-card>
      </a-col>
    </a-row>

    <!-- 字典类型弹窗 -->
    <a-modal
      v-model:open="typeModalVisible"
      :title="typeEditTarget ? t('dict.editType') : t('dict.createType')"
      :confirm-loading="typeModalLoading"
      @ok="submitTypeForm"
      @cancel="closeTypeModal"
    >
      <a-form :model="typeForm" layout="vertical" :rules="typeRules" ref="typeFormRef">
        <a-form-item :label="t('dict.typeCode')" name="code">
          <a-input
            v-model:value="typeForm.code"
            :disabled="!!typeEditTarget"
            :placeholder="t('dict.typeCodePlaceholder')"
          />
        </a-form-item>
        <a-form-item :label="t('dict.typeName')" name="name">
          <a-input v-model:value="typeForm.name" :placeholder="t('dict.typeNamePlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('dict.status')" name="status">
          <a-switch
            v-model:checked="typeForm.status"
            :checked-children="t('dict.statusEnabled')"
            :un-checked-children="t('dict.statusDisabled')"
          />
        </a-form-item>
        <a-form-item :label="t('dict.remark')" name="remark">
          <a-textarea v-model:value="typeForm.remark" :rows="2" :placeholder="t('dict.remarkPlaceholder')" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 字典数据弹窗 -->
    <a-modal
      v-model:open="dataModalVisible"
      :title="dataEditTarget ? t('dict.editData') : t('dict.createData')"
      :confirm-loading="dataModalLoading"
      @ok="submitDataForm"
      @cancel="closeDataModal"
    >
      <a-form :model="dataForm" layout="vertical" :rules="dataRules" ref="dataFormRef">
        <a-form-item :label="t('dict.label')" name="label">
          <a-input v-model:value="dataForm.label" :placeholder="t('dict.labelPlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('dict.value')" name="value">
          <a-input v-model:value="dataForm.value" :placeholder="t('dict.valuePlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('dict.sortOrder')" name="sortOrder">
          <a-input-number v-model:value="dataForm.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
        <a-form-item :label="t('dict.status')" name="status">
          <a-switch
            v-model:checked="dataForm.status"
            :checked-children="t('dict.statusEnabled')"
            :un-checked-children="t('dict.statusDisabled')"
          />
        </a-form-item>
        <a-form-item :label="t('dict.listClass')" name="listClass">
          <a-select v-model:value="dataForm.listClass" allow-clear :placeholder="t('dict.listClassPlaceholder')">
            <a-select-option value="success">{{ t("dict.listClassSuccess") }}</a-select-option>
            <a-select-option value="warning">{{ t("dict.listClassWarning") }}</a-select-option>
            <a-select-option value="error">{{ t("dict.listClassError") }}</a-select-option>
            <a-select-option value="processing">{{ t("dict.listClassProcessing") }}</a-select-option>
            <a-select-option value="default">{{ t("dict.listClassDefault") }}</a-select-option>
          </a-select>
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import type { TablePaginationConfig } from "ant-design-vue";
import {
  getDictTypesPaged,
  createDictType,
  updateDictType,
  deleteDictType,
  getDictDataPaged,
  createDictData,
  updateDictData,
  deleteDictData,
  type DictTypeDto,
  type DictDataDto
} from "@/services/dict";

const { t } = useI18n();

// ── 字典类型 ────────────────────────────────────────────────────────────────
const typeKeyword = ref("");
const typeList = ref<DictTypeDto[]>([]);
const typeLoading = ref(false);
const selectedType = ref<DictTypeDto | null>(null);
const typePagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 });

async function loadTypes() {
  typeLoading.value = true;
  try {
    const result  = await getDictTypesPaged({
      pageIndex: typePagination.pageIndex,
      pageSize: typePagination.pageSize,
      keyword: typeKeyword.value || undefined
    });

    if (!isMounted.value) return;
    typeList.value = result.items as DictTypeDto[];
    typePagination.total = Number(result.total);
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : undefined) || t("dict.loadTypesFailed"));
  } finally {
    typeLoading.value = false;
  }
}

function selectType(item: DictTypeDto) {
  selectedType.value = item;
  dataKeyword.value = "";
  dataPagination.current = 1;
  loadData();
}

// ── 字典数据 ────────────────────────────────────────────────────────────────
const dataKeyword = ref("");
const dataList = ref<DictDataDto[]>([]);
const dataLoading = ref(false);
const dataPagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: false
});

const dataColumns = [
  { title: t("dict.colLabel"), dataIndex: "label", key: "label" },
  { title: t("dict.colValue"), dataIndex: "value", key: "value" },
  { title: t("dict.colSortOrder"), dataIndex: "sortOrder", key: "sortOrder", width: 70 },
  { title: t("dict.colStatus"), key: "status", width: 80 },
  { title: t("dict.colListClass"), key: "listClass", width: 120 },
  { title: t("dict.colActions"), key: "actions", width: 120, fixed: "right" as const }
];

async function loadData() {
  if (!selectedType.value) return;
  dataLoading.value = true;
  try {
    const result  = await getDictDataPaged(selectedType.value.code, {
      pageIndex: dataPagination.current ?? 1,
      pageSize: dataPagination.pageSize ?? 20,
      keyword: dataKeyword.value || undefined
    });

    if (!isMounted.value) return;
    dataList.value = result.items as DictDataDto[];
    dataPagination.total = Number(result.total);
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : undefined) || t("dict.loadDataFailed"));
  } finally {
    dataLoading.value = false;
  }
}

function onDataTableChange(pagination: TablePaginationConfig) {
  dataPagination.current = pagination.current ?? 1;
  dataPagination.pageSize = pagination.pageSize ?? 20;
  loadData();
}

// ── 字典类型弹窗 ─────────────────────────────────────────────────────────────
const typeModalVisible = ref(false);
const typeModalLoading = ref(false);
const typeEditTarget = ref<DictTypeDto | null>(null);
const typeFormRef = ref();
const typeForm = reactive({ code: "", name: "", status: true, remark: "" });

const typeRules = {
  code: [
    { required: true, message: t("dict.typeCodeRequired") },
    { pattern: /^[a-z][a-z0-9_]{0,63}$/, message: t("dict.typeCodePattern") }
  ],
  name: [{ required: true, message: t("dict.typeNameRequired") }]
};

function openCreateType() {
  typeEditTarget.value = null;
  Object.assign(typeForm, { code: "", name: "", status: true, remark: "" });
  typeModalVisible.value = true;
}

function openEditType(item: DictTypeDto) {
  typeEditTarget.value = item;
  Object.assign(typeForm, { code: item.code, name: item.name, status: item.status, remark: item.remark || "" });
  typeModalVisible.value = true;
}

function closeTypeModal() {
  typeModalVisible.value = false;
  typeFormRef.value?.resetFields();
}

async function submitTypeForm() {
  try {
    await typeFormRef.value?.validate();

    if (!isMounted.value) return;
  } catch {
    return;
  }
  typeModalLoading.value = true;
  try {
    if (typeEditTarget.value) {
      await updateDictType(typeEditTarget.value.id, {
        name: typeForm.name,
        status: typeForm.status,
        remark: typeForm.remark || undefined
      });

      if (!isMounted.value) return;
      message.success(t("dict.updateTypeSuccess"));
    } else {
      await createDictType({
        code: typeForm.code,
        name: typeForm.name,
        status: typeForm.status,
        remark: typeForm.remark || undefined
      });

      if (!isMounted.value) return;
      message.success(t("dict.createTypeSuccess"));
    }
    typeModalVisible.value = false;
    loadTypes();
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : undefined) || t("dict.operationFailed"));
  } finally {
    typeModalLoading.value = false;
  }
}

async function handleDeleteType(id: string) {
  try {
    await deleteDictType(id);

    if (!isMounted.value) return;
    message.success(t("dict.deleteTypeSuccess"));
    if (selectedType.value?.id === id) {
      selectedType.value = null;
      dataList.value = [];
    }
    loadTypes();
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : undefined) || t("dict.deleteTypeFailed"));
  }
}

// ── 字典数据弹窗 ─────────────────────────────────────────────────────────────
const dataModalVisible = ref(false);
const dataModalLoading = ref(false);
const dataEditTarget = ref<DictDataDto | null>(null);
const dataFormRef = ref();
const dataForm = reactive({
  label: "",
  value: "",
  sortOrder: 0,
  status: true,
  cssClass: "",
  listClass: undefined as string | undefined
});

const dataRules = {
  label: [{ required: true, message: t("dict.labelRequired") }],
  value: [{ required: true, message: t("dict.valueRequired") }]
};

function openCreateData() {
  dataEditTarget.value = null;
  Object.assign(dataForm, { label: "", value: "", sortOrder: 0, status: true, cssClass: "", listClass: undefined });
  dataModalVisible.value = true;
}

function openEditData(item: DictDataDto) {
  dataEditTarget.value = item;
  Object.assign(dataForm, {
    label: item.label,
    value: item.value,
    sortOrder: item.sortOrder,
    status: item.status,
    cssClass: item.cssClass || "",
    listClass: item.listClass || undefined
  });
  dataModalVisible.value = true;
}

function closeDataModal() {
  dataModalVisible.value = false;
  dataFormRef.value?.resetFields();
}

async function submitDataForm() {
  try {
    await dataFormRef.value?.validate();

    if (!isMounted.value) return;
  } catch {
    return;
  }
  if (!selectedType.value) return;
  dataModalLoading.value = true;
  try {
    const payload = {
      label: dataForm.label,
      value: dataForm.value,
      sortOrder: dataForm.sortOrder,
      status: dataForm.status,
      cssClass: dataForm.cssClass || undefined,
      listClass: dataForm.listClass || undefined
    };
    if (dataEditTarget.value) {
      await updateDictData(dataEditTarget.value.id, payload);

      if (!isMounted.value) return;
      message.success(t("dict.updateDataSuccess"));
    } else {
      await createDictData(selectedType.value.code, payload);

      if (!isMounted.value) return;
      message.success(t("dict.createDataSuccess"));
    }
    dataModalVisible.value = false;
    loadData();
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : undefined) || t("dict.operationFailed"));
  } finally {
    dataModalLoading.value = false;
  }
}

async function handleDeleteData(id: string) {
  try {
    await deleteDictData(id);

    if (!isMounted.value) return;
    message.success(t("dict.deleteDataSuccess"));
    loadData();
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : undefined) || t("dict.deleteDataFailed"));
  }
}

onMounted(() => {
  loadTypes();
});
</script>

<style scoped>
.dict-page {
  padding: 0;
}
.dict-type-item {
  cursor: pointer;
  padding: 8px 12px;
  border-radius: 6px;
  transition: background-color 0.2s;
}
.dict-type-item:hover {
  background-color: #f5f5f5;
}
.dict-type-item-active {
  background-color: #e6f4ff;
}
</style>




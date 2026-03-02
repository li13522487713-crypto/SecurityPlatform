<template>
  <div class="dict-page">
    <a-row :gutter="16">
      <!-- 左侧：字典类型列表 -->
      <a-col :span="8">
        <a-card title="字典类型" :bordered="false">
          <template #extra>
            <a-button type="primary" size="small" @click="openCreateType">新增</a-button>
          </template>
          <div style="margin-bottom: 12px">
            <a-input-search
              v-model:value="typeKeyword"
              placeholder="搜索字典类型"
              allow-clear
              @search="loadTypes"
              @press-enter="loadTypes"
            />
          </div>
          <a-spin :spinning="typeLoading">
            <a-list
              :data-source="typeList"
              :locale="{ emptyText: '暂无字典类型' }"
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
                      <a-tag v-if="!item.status" color="default" style="margin-left: 6px">禁用</a-tag>
                    </template>
                    <template #description>{{ item.code }}</template>
                  </a-list-item-meta>
                  <template #actions>
                    <a-button type="link" size="small" @click.stop="openEditType(item)">编辑</a-button>
                    <a-popconfirm
                      title="确认删除该字典类型及其所有数据？"
                      ok-text="删除"
                      cancel-text="取消"
                      @confirm="handleDeleteType(item.id)"
                    >
                      <a-button type="link" danger size="small" @click.stop>删除</a-button>
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
          :title="selectedType ? `字典数据 — ${selectedType.name} (${selectedType.code})` : '字典数据'"
          :bordered="false"
        >
          <template #extra>
            <a-button
              v-if="selectedType"
              type="primary"
              size="small"
              @click="openCreateData"
            >新增数据</a-button>
          </template>

          <a-empty v-if="!selectedType" description="请从左侧选择字典类型" />

          <template v-else>
            <div style="margin-bottom: 12px">
              <a-input-search
                v-model:value="dataKeyword"
                placeholder="搜索字典数据"
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
              :locale="{ emptyText: '暂无字典数据' }"
              @change="onDataTableChange"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.key === 'status'">
                  <a-tag :color="record.status ? 'success' : 'default'">
                    {{ record.status ? '启用' : '禁用' }}
                  </a-tag>
                </template>
                <template v-else-if="column.key === 'listClass'">
                  <a-tag v-if="record.listClass" :color="record.listClass">{{ record.label }}</a-tag>
                  <span v-else>-</span>
                </template>
                <template v-else-if="column.key === 'actions'">
                  <a-space>
                    <a-button type="link" size="small" @click="openEditData(record)">编辑</a-button>
                    <a-popconfirm
                      title="确认删除该字典数据？"
                      ok-text="删除"
                      cancel-text="取消"
                      @confirm="handleDeleteData(record.id)"
                    >
                      <a-button type="link" danger size="small">删除</a-button>
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
      :title="typeEditTarget ? '编辑字典类型' : '新增字典类型'"
      :confirm-loading="typeModalLoading"
      @ok="submitTypeForm"
      @cancel="closeTypeModal"
    >
      <a-form :model="typeForm" layout="vertical" :rules="typeRules" ref="typeFormRef">
        <a-form-item label="字典编码" name="code">
          <a-input
            v-model:value="typeForm.code"
            :disabled="!!typeEditTarget"
            placeholder="小写字母、数字和下划线，如 sys_user_sex"
          />
        </a-form-item>
        <a-form-item label="字典名称" name="name">
          <a-input v-model:value="typeForm.name" placeholder="请输入字典名称" />
        </a-form-item>
        <a-form-item label="状态" name="status">
          <a-switch v-model:checked="typeForm.status" checked-children="启用" un-checked-children="禁用" />
        </a-form-item>
        <a-form-item label="备注" name="remark">
          <a-textarea v-model:value="typeForm.remark" :rows="2" placeholder="备注（选填）" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 字典数据弹窗 -->
    <a-modal
      v-model:open="dataModalVisible"
      :title="dataEditTarget ? '编辑字典数据' : '新增字典数据'"
      :confirm-loading="dataModalLoading"
      @ok="submitDataForm"
      @cancel="closeDataModal"
    >
      <a-form :model="dataForm" layout="vertical" :rules="dataRules" ref="dataFormRef">
        <a-form-item label="显示标签" name="label">
          <a-input v-model:value="dataForm.label" placeholder="如：男" />
        </a-form-item>
        <a-form-item label="数据值" name="value">
          <a-input v-model:value="dataForm.value" placeholder="如：0" />
        </a-form-item>
        <a-form-item label="排序" name="sortOrder">
          <a-input-number v-model:value="dataForm.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
        <a-form-item label="状态" name="status">
          <a-switch v-model:checked="dataForm.status" checked-children="启用" un-checked-children="禁用" />
        </a-form-item>
        <a-form-item label="标签样式" name="listClass">
          <a-select v-model:value="dataForm.listClass" allow-clear placeholder="可选，用于表格标签颜色">
            <a-select-option value="success">success（绿）</a-select-option>
            <a-select-option value="warning">warning（橙）</a-select-option>
            <a-select-option value="error">error（红）</a-select-option>
            <a-select-option value="processing">processing（蓝）</a-select-option>
            <a-select-option value="default">default（灰）</a-select-option>
          </a-select>
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from "vue";
import { message } from "ant-design-vue";
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

// ── 字典类型 ────────────────────────────────────────────────────────────────
const typeKeyword = ref("");
const typeList = ref<DictTypeDto[]>([]);
const typeLoading = ref(false);
const selectedType = ref<DictTypeDto | null>(null);
const typePagination = reactive({ pageIndex: 1, pageSize: 20, total: 0 });

async function loadTypes() {
  typeLoading.value = true;
  try {
    const result = await getDictTypesPaged({
      pageIndex: typePagination.pageIndex,
      pageSize: typePagination.pageSize,
      keyword: typeKeyword.value || undefined
    });
    typeList.value = result.items as DictTypeDto[];
    typePagination.total = Number(result.total);
  } catch (e: any) {
    message.error(e.message || "加载字典类型失败");
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
  { title: "标签", dataIndex: "label", key: "label" },
  { title: "值", dataIndex: "value", key: "value" },
  { title: "排序", dataIndex: "sortOrder", key: "sortOrder", width: 70 },
  { title: "状态", key: "status", width: 80 },
  { title: "样式预览", key: "listClass", width: 120 },
  { title: "操作", key: "actions", width: 120, fixed: "right" as const }
];

async function loadData() {
  if (!selectedType.value) return;
  dataLoading.value = true;
  try {
    const result = await getDictDataPaged(selectedType.value.code, {
      pageIndex: dataPagination.current ?? 1,
      pageSize: dataPagination.pageSize ?? 20,
      keyword: dataKeyword.value || undefined
    });
    dataList.value = result.items as DictDataDto[];
    dataPagination.total = Number(result.total);
  } catch (e: any) {
    message.error(e.message || "加载字典数据失败");
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
    { required: true, message: "请输入字典编码" },
    { pattern: /^[a-z][a-z0-9_]{0,63}$/, message: "只允许小写字母、数字和下划线，且必须以字母开头" }
  ],
  name: [{ required: true, message: "请输入字典名称" }]
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
      message.success("更新成功");
    } else {
      await createDictType({
        code: typeForm.code,
        name: typeForm.name,
        status: typeForm.status,
        remark: typeForm.remark || undefined
      });
      message.success("创建成功");
    }
    typeModalVisible.value = false;
    loadTypes();
  } catch (e: any) {
    message.error(e.message || "操作失败");
  } finally {
    typeModalLoading.value = false;
  }
}

async function handleDeleteType(id: string) {
  try {
    await deleteDictType(id);
    message.success("删除成功");
    if (selectedType.value?.id === id) {
      selectedType.value = null;
      dataList.value = [];
    }
    loadTypes();
  } catch (e: any) {
    message.error(e.message || "删除失败");
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
  label: [{ required: true, message: "请输入显示标签" }],
  value: [{ required: true, message: "请输入数据值" }]
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
      message.success("更新成功");
    } else {
      await createDictData(selectedType.value.code, payload);
      message.success("创建成功");
    }
    dataModalVisible.value = false;
    loadData();
  } catch (e: any) {
    message.error(e.message || "操作失败");
  } finally {
    dataModalLoading.value = false;
  }
}

async function handleDeleteData(id: string) {
  try {
    await deleteDictData(id);
    message.success("删除成功");
    loadData();
  } catch (e: any) {
    message.error(e.message || "删除失败");
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

<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card :title="t('ai.marketplace.pageTitle')" :bordered="false">
      <template #extra>
        <a-space>
          <a-button @click="openCategoryModal">{{ t("ai.marketplace.categoryManage") }}</a-button>
          <a-button @click="resetFilters">{{ t("common.reset") }}</a-button>
          <a-button type="primary" @click="openCreateProduct">{{ t("ai.marketplace.publishProduct") }}</a-button>
        </a-space>
      </template>

      <div class="toolbar">
        <a-space wrap>
          <a-input-search
            v-model:value="filters.keyword"
            :placeholder="t('ai.marketplace.searchPlaceholder')"
            style="width: 260px"
            @search="loadProducts"
          />
          <a-select
            v-model:value="filters.categoryId"
            allow-clear
            :placeholder="t('ai.marketplace.placeholderCategory')"
            style="width: 180px"
            :options="categoryOptions"
            @change="loadProducts"
          />
          <a-select
            v-model:value="filters.productType"
            allow-clear
            :placeholder="t('ai.marketplace.placeholderType')"
            style="width: 160px"
            :options="productTypeOptions"
            @change="loadProducts"
          />
          <a-select
            v-model:value="filters.status"
            allow-clear
            :placeholder="t('ai.marketplace.placeholderStatus')"
            style="width: 160px"
            :options="statusOptions"
            @change="loadProducts"
          />
        </a-space>
      </div>

      <a-table row-key="id" :columns="columns" :data-source="products" :loading="loading" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'productType'">
            <a-tag color="blue">{{ formatProductType(record.productType) }}</a-tag>
          </template>
          <template v-if="column.key === 'status'">
            <a-tag :color="statusColor(record.status)">{{ formatStatus(record.status) }}</a-tag>
          </template>
          <template v-if="column.key === 'favorite'">
            <a-space>
              <span>{{ record.favoriteCount }}</span>
              <a-button type="link" size="small" @click="toggleFavorite(record)">
                {{ record.isFavorited ? t("ai.marketplace.unfavorite") : t("ai.marketplace.favorite") }}
              </a-button>
            </a-space>
          </template>
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button type="link" @click="goDetail(record.id)">{{ t("ai.plugin.detail") }}</a-button>
              <a-button type="link" @click="openEditProduct(record.id)">{{ t("common.edit") }}</a-button>
              <a-button type="link" @click="handlePublish(record)">{{ t("ai.workflow.publish") }}</a-button>
              <a-popconfirm :title="t('ai.marketplace.deleteProductConfirm')" @confirm="handleDeleteProduct(record.id)">
                <a-button type="link" danger>{{ t("common.delete") }}</a-button>
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
          @change="loadProducts"
        />
      </div>
    </a-card>

    <a-modal
      v-model:open="productModalOpen"
      :title="editingProductId ? t('ai.marketplace.modalProductEdit') : t('ai.marketplace.modalProductCreate')"
      :confirm-loading="productSubmitting"
      width="760px"
      @ok="submitProduct"
      @cancel="closeProductModal"
    >
      <a-form ref="productFormRef" :model="productForm" layout="vertical" :rules="productRules">
        <a-form-item :label="t('ai.promptLib.labelCategory')" name="categoryId">
          <a-select v-model:value="productForm.categoryId" :options="categoryOptions" />
        </a-form-item>
        <a-form-item :label="t('ai.promptLib.colName')" name="name">
          <a-input v-model:value="productForm.name" />
        </a-form-item>
        <a-form-item :label="t('ai.marketplace.labelSummary')" name="summary">
          <a-input v-model:value="productForm.summary" />
        </a-form-item>
        <a-form-item :label="t('ai.promptLib.labelDescription')" name="description">
          <a-textarea v-model:value="productForm.description" :rows="3" />
        </a-form-item>
        <a-form-item :label="t('ai.marketplace.labelIconUrl')" name="icon">
          <a-input v-model:value="productForm.icon" />
        </a-form-item>
        <a-form-item :label="t('ai.plugin.labelType')" name="productType">
          <a-select v-model:value="productForm.productType" :options="productTypeOptions" />
        </a-form-item>
        <a-form-item :label="t('ai.marketplace.labelTags')" name="tagsInput">
          <a-input v-model:value="productForm.tagsInput" />
        </a-form-item>
        <a-form-item :label="t('ai.marketplace.labelSourceId')" name="sourceResourceId">
          <a-input-number v-model:value="productForm.sourceResourceId" :min="1" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="categoryModalOpen"
      :title="t('ai.marketplace.modalCategoryTitle')"
      :confirm-loading="categorySubmitting"
      width="680px"
      @ok="submitCategory"
      @cancel="closeCategoryModal"
    >
      <a-form ref="categoryFormRef" :model="categoryForm" layout="vertical" :rules="categoryRules">
        <a-form-item :label="t('ai.promptLib.colName')" name="name">
          <a-input v-model:value="categoryForm.name" />
        </a-form-item>
        <a-form-item :label="t('ai.marketplace.labelCode')" name="code">
          <a-input v-model:value="categoryForm.code" />
        </a-form-item>
        <a-form-item :label="t('ai.promptLib.labelDescription')" name="description">
          <a-input v-model:value="categoryForm.description" />
        </a-form-item>
        <a-form-item :label="t('ai.shortcuts.labelSort')" name="sortOrder">
          <a-input-number v-model:value="categoryForm.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>

      <a-divider />
      <a-table row-key="id" :columns="categoryColumns" :data-source="categories" :pagination="false" size="small">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button type="link" @click="fillCategoryForm(record)">{{ t("common.edit") }}</a-button>
              <a-popconfirm :title="t('ai.marketplace.deleteCategoryConfirm')" @confirm="handleDeleteCategory(record.id)">
                <a-button type="link" danger>{{ t("common.delete") }}</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-modal>
  </a-space>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRouter } from "vue-router";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import {
  createAiMarketplaceCategory,
  createAiMarketplaceProduct,
  deleteAiMarketplaceCategory,
  deleteAiMarketplaceProduct,
  favoriteAiMarketplaceProduct,
  getAiMarketplaceCategories,
  getAiMarketplaceProductById,
  getAiMarketplaceProductsPaged,
  publishAiMarketplaceProduct,
  unfavoriteAiMarketplaceProduct,
  updateAiMarketplaceCategory,
  updateAiMarketplaceProduct,
  type AiMarketplaceProductListItem,
  type AiMarketplaceProductStatus,
  type AiMarketplaceProductType,
  type AiProductCategoryItem
} from "@/services/api-ai-marketplace";

const router = useRouter();
const loading = ref(false);
const products = ref<AiMarketplaceProductListItem[]>([]);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const categories = ref<AiProductCategoryItem[]>([]);
const categoryOptions = computed(() => categories.value.map((item) => ({ label: item.name, value: item.id })));

const filters = reactive({
  keyword: "",
  categoryId: undefined as number | undefined,
  productType: undefined as AiMarketplaceProductType | undefined,
  status: undefined as AiMarketplaceProductStatus | undefined
});

const productTypeOptions = computed(() => [
  { label: t("ai.marketplace.typeAgent"), value: 1 },
  { label: t("ai.marketplace.typeWorkflow"), value: 2 },
  { label: t("ai.marketplace.typePrompt"), value: 3 },
  { label: t("ai.marketplace.typePlugin"), value: 4 },
  { label: t("ai.marketplace.typeApp"), value: 5 }
]);

const statusOptions = computed(() => [
  { label: t("ai.marketplace.statusDraft"), value: 0 },
  { label: t("ai.marketplace.statusPublished"), value: 1 },
  { label: t("ai.marketplace.statusArchived"), value: 2 }
]);

const columns = computed(() => [
  { title: t("ai.promptLib.colName"), dataIndex: "name", key: "name", width: 200 },
  { title: t("ai.promptLib.labelCategory"), dataIndex: "categoryName", key: "categoryName", width: 140 },
  { title: t("ai.plugin.labelType"), key: "productType", width: 100 },
  { title: t("ai.workflow.colStatus"), key: "status", width: 100 },
  { title: t("ai.marketplace.colDownload"), dataIndex: "downloadCount", key: "downloadCount", width: 90 },
  { title: t("ai.marketplace.colFavorite"), key: "favorite", width: 140 },
  { title: t("ai.workflow.colUpdatedAt"), dataIndex: "updatedAt", key: "updatedAt", width: 180 },
  { title: t("ai.colActions"), key: "action", width: 220 }
]);

const productModalOpen = ref(false);
const productSubmitting = ref(false);
const productFormRef = ref<FormInstance>();
const editingProductId = ref<number | null>(null);
const productForm = reactive({
  categoryId: undefined as number | undefined,
  name: "",
  summary: "",
  description: "",
  icon: "",
  productType: 1 as AiMarketplaceProductType,
  tagsInput: "",
  sourceResourceId: undefined as number | undefined
});
const productRules = computed(() => ({
  categoryId: [{ required: true, message: t("ai.marketplace.ruleCategory") }],
  name: [{ required: true, message: t("ai.marketplace.ruleName") }]
}));

const categoryModalOpen = ref(false);
const categorySubmitting = ref(false);
const categoryFormRef = ref<FormInstance>();
const editingCategoryId = ref<number | null>(null);
const categoryForm = reactive({
  name: "",
  code: "",
  description: "",
  sortOrder: 10
});
const categoryRules = computed(() => ({
  name: [{ required: true, message: t("ai.marketplace.ruleCategoryName") }],
  code: [{ required: true, message: t("ai.marketplace.ruleCategoryCode") }]
}));
const categoryColumns = computed(() => [
  { title: t("ai.promptLib.colName"), dataIndex: "name", key: "name", width: 180 },
  { title: t("ai.marketplace.labelCode"), dataIndex: "code", key: "code", width: 180 },
  { title: t("ai.shortcuts.labelSort"), dataIndex: "sortOrder", key: "sortOrder", width: 100 },
  { title: t("ai.colActions"), key: "action", width: 140 }
]);

function formatProductType(type: AiMarketplaceProductType) {
  return productTypeOptions.value.find((item) => item.value === type)?.label ?? t("ai.unknown");
}

function formatStatus(status: AiMarketplaceProductStatus) {
  return statusOptions.value.find((item) => item.value === status)?.label ?? t("ai.unknown");
}

function statusColor(status: AiMarketplaceProductStatus) {
  if (status === 1) {
    return "green";
  }

  if (status === 2) {
    return "default";
  }

  return "blue";
}

function parseTags(input: string) {
  return input
    .split(",")
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}

async function loadCategories() {
  categories.value = await getAiMarketplaceCategories();

  if (!isMounted.value) return;
}

async function loadProducts() {
  loading.value = true;
  try {
    const result  = await getAiMarketplaceProductsPaged(
      {
        pageIndex: pageIndex.value,
        pageSize: pageSize.value
      },
      {
        keyword: filters.keyword || undefined,
        categoryId: filters.categoryId,
        productType: filters.productType,
        status: filters.status
      }
    );

    if (!isMounted.value) return;
    products.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.marketplace.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function resetFilters() {
  filters.keyword = "";
  filters.categoryId = undefined;
  filters.productType = undefined;
  filters.status = undefined;
  pageIndex.value = 1;
  void loadProducts();
}

function goDetail(id: number) {
  void router.push(`/ai/marketplace/${id}`);
}

function openCreateProduct() {
  editingProductId.value = null;
  Object.assign(productForm, {
    categoryId: categories.value[0]?.id,
    name: "",
    summary: "",
    description: "",
    icon: "",
    productType: 1 as AiMarketplaceProductType,
    tagsInput: "",
    sourceResourceId: undefined
  });
  productModalOpen.value = true;
}

async function openEditProduct(id: number) {
  try {
    const detail  = await getAiMarketplaceProductById(id);

    if (!isMounted.value) return;
    editingProductId.value = id;
    Object.assign(productForm, {
      categoryId: detail.categoryId,
      name: detail.name,
      summary: detail.summary ?? "",
      description: detail.description ?? "",
      icon: detail.icon ?? "",
      productType: detail.productType,
      tagsInput: detail.tags.join(","),
      sourceResourceId: detail.sourceResourceId
    });
    productModalOpen.value = true;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.marketplace.loadDetailFailed"));
  }
}

function closeProductModal() {
  productModalOpen.value = false;
  productFormRef.value?.resetFields();
}

async function submitProduct() {
  try {
    await productFormRef.value?.validate();

    if (!isMounted.value) return;
  } catch {
    return;
  }

  productSubmitting.value = true;
  try {
    const payload = {
      categoryId: Number(productForm.categoryId),
      name: productForm.name,
      summary: productForm.summary || undefined,
      description: productForm.description || undefined,
      icon: productForm.icon || undefined,
      tags: parseTags(productForm.tagsInput),
      productType: productForm.productType,
      sourceResourceId: productForm.sourceResourceId
    };
    if (editingProductId.value) {
      await updateAiMarketplaceProduct(editingProductId.value, payload);

      if (!isMounted.value) return;
      message.success(t("ai.marketplace.productUpdateOk"));
    } else {
      await createAiMarketplaceProduct(payload);

      if (!isMounted.value) return;
      message.success(t("ai.marketplace.productCreateOk"));
    }

    productModalOpen.value = false;
    await loadProducts();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.marketplace.submitFailed"));
  } finally {
    productSubmitting.value = false;
  }
}

async function handleDeleteProduct(id: number) {
  try {
    await deleteAiMarketplaceProduct(id);

    if (!isMounted.value) return;
    message.success(t("crud.deleteSuccess"));
    await loadProducts();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.deleteFailed"));
  }
}

async function handlePublish(record: AiMarketplaceProductListItem) {
  try {
    const version = record.status === 0 ? "1.0.0" : record.version;
    await publishAiMarketplaceProduct(record.id, { version });

    if (!isMounted.value) return;
    message.success(t("ai.marketplace.publishSuccess"));
    await loadProducts();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.marketplace.publishFailed"));
  }
}

async function toggleFavorite(record: AiMarketplaceProductListItem) {
  try {
    if (record.isFavorited) {
      await unfavoriteAiMarketplaceProduct(record.id);

      if (!isMounted.value) return;
    } else {
      await favoriteAiMarketplaceProduct(record.id);

      if (!isMounted.value) return;
    }

    await loadProducts();


    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.marketplace.favoriteUpdateFailed"));
  }
}

function openCategoryModal() {
  editingCategoryId.value = null;
  Object.assign(categoryForm, {
    name: "",
    code: "",
    description: "",
    sortOrder: 10
  });
  categoryModalOpen.value = true;
}

function closeCategoryModal() {
  categoryModalOpen.value = false;
  categoryFormRef.value?.resetFields();
}

function fillCategoryForm(item: AiProductCategoryItem) {
  editingCategoryId.value = item.id;
  Object.assign(categoryForm, {
    name: item.name,
    code: item.code,
    description: item.description ?? "",
    sortOrder: item.sortOrder
  });
}

async function submitCategory() {
  try {
    await categoryFormRef.value?.validate();

    if (!isMounted.value) return;
  } catch {
    return;
  }

  categorySubmitting.value = true;
  try {
    const payload = {
      name: categoryForm.name,
      code: categoryForm.code,
      description: categoryForm.description || undefined,
      sortOrder: categoryForm.sortOrder
    };
    if (editingCategoryId.value) {
      await updateAiMarketplaceCategory(editingCategoryId.value, payload);

      if (!isMounted.value) return;
      message.success(t("ai.marketplace.categoryUpdateOk"));
    } else {
      await createAiMarketplaceCategory(payload);

      if (!isMounted.value) return;
      message.success(t("ai.marketplace.categoryCreateOk"));
    }

    editingCategoryId.value = null;
    await Promise.allSettled([
      loadCategories(),
      loadProducts()
    ]);

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.marketplace.saveCategoryFailed"));
  } finally {
    categorySubmitting.value = false;
  }
}

async function handleDeleteCategory(id: number) {
  try {
    await deleteAiMarketplaceCategory(id);

    if (!isMounted.value) return;
    message.success(t("ai.marketplace.categoryDeleteOk"));
    await loadCategories();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.marketplace.deleteCategoryFailed"));
  }
}

onMounted(async () => {
  await Promise.allSettled([
    loadCategories(),
    loadProducts()
  ]);

  if (!isMounted.value) return;
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

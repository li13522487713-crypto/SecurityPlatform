<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card title="AI 市场" :bordered="false">
      <template #extra>
        <a-space>
          <a-button @click="openCategoryModal">分类管理</a-button>
          <a-button @click="resetFilters">重置</a-button>
          <a-button type="primary" @click="openCreateProduct">发布商品</a-button>
        </a-space>
      </template>

      <div class="toolbar">
        <a-space wrap>
          <a-input-search
            v-model:value="filters.keyword"
            placeholder="搜索商品名称/描述"
            style="width: 260px"
            @search="loadProducts"
          />
          <a-select
            v-model:value="filters.categoryId"
            allow-clear
            placeholder="分类"
            style="width: 180px"
            :options="categoryOptions"
            @change="loadProducts"
          />
          <a-select
            v-model:value="filters.productType"
            allow-clear
            placeholder="类型"
            style="width: 160px"
            :options="productTypeOptions"
            @change="loadProducts"
          />
          <a-select
            v-model:value="filters.status"
            allow-clear
            placeholder="状态"
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
                {{ record.isFavorited ? "取消收藏" : "收藏" }}
              </a-button>
            </a-space>
          </template>
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button type="link" @click="goDetail(record.id)">详情</a-button>
              <a-button type="link" @click="openEditProduct(record.id)">编辑</a-button>
              <a-button type="link" @click="handlePublish(record)">发布</a-button>
              <a-popconfirm title="确认删除该商品？" @confirm="handleDeleteProduct(record.id)">
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
          @change="loadProducts"
        />
      </div>
    </a-card>

    <a-modal
      v-model:open="productModalOpen"
      :title="editingProductId ? '编辑商品' : '发布商品'"
      :confirm-loading="productSubmitting"
      width="760px"
      @ok="submitProduct"
      @cancel="closeProductModal"
    >
      <a-form ref="productFormRef" :model="productForm" layout="vertical" :rules="productRules">
        <a-form-item label="分类" name="categoryId">
          <a-select v-model:value="productForm.categoryId" :options="categoryOptions" />
        </a-form-item>
        <a-form-item label="名称" name="name">
          <a-input v-model:value="productForm.name" />
        </a-form-item>
        <a-form-item label="摘要" name="summary">
          <a-input v-model:value="productForm.summary" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-textarea v-model:value="productForm.description" :rows="3" />
        </a-form-item>
        <a-form-item label="图标 URL" name="icon">
          <a-input v-model:value="productForm.icon" />
        </a-form-item>
        <a-form-item label="类型" name="productType">
          <a-select v-model:value="productForm.productType" :options="productTypeOptions" />
        </a-form-item>
        <a-form-item label="标签（逗号分隔）" name="tagsInput">
          <a-input v-model:value="productForm.tagsInput" />
        </a-form-item>
        <a-form-item label="来源资源 ID" name="sourceResourceId">
          <a-input-number v-model:value="productForm.sourceResourceId" :min="1" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="categoryModalOpen"
      title="分类管理"
      :confirm-loading="categorySubmitting"
      width="680px"
      @ok="submitCategory"
      @cancel="closeCategoryModal"
    >
      <a-form ref="categoryFormRef" :model="categoryForm" layout="vertical" :rules="categoryRules">
        <a-form-item label="名称" name="name">
          <a-input v-model:value="categoryForm.name" />
        </a-form-item>
        <a-form-item label="编码" name="code">
          <a-input v-model:value="categoryForm.code" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-input v-model:value="categoryForm.description" />
        </a-form-item>
        <a-form-item label="排序" name="sortOrder">
          <a-input-number v-model:value="categoryForm.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>

      <a-divider />
      <a-table row-key="id" :columns="categoryColumns" :data-source="categories" :pagination="false" size="small">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button type="link" @click="fillCategoryForm(record)">编辑</a-button>
              <a-popconfirm title="确认删除该分类？" @confirm="handleDeleteCategory(record.id)">
                <a-button type="link" danger>删除</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-modal>
  </a-space>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
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

const productTypeOptions = [
  { label: "Agent", value: 1 },
  { label: "工作流", value: 2 },
  { label: "Prompt", value: 3 },
  { label: "插件", value: 4 },
  { label: "应用", value: 5 }
];
const statusOptions = [
  { label: "草稿", value: 0 },
  { label: "已发布", value: 1 },
  { label: "已归档", value: 2 }
];

const columns = [
  { title: "名称", dataIndex: "name", key: "name", width: 200 },
  { title: "分类", dataIndex: "categoryName", key: "categoryName", width: 140 },
  { title: "类型", key: "productType", width: 100 },
  { title: "状态", key: "status", width: 100 },
  { title: "下载", dataIndex: "downloadCount", key: "downloadCount", width: 90 },
  { title: "收藏", key: "favorite", width: 140 },
  { title: "更新时间", dataIndex: "updatedAt", key: "updatedAt", width: 180 },
  { title: "操作", key: "action", width: 220 }
];

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
const productRules = {
  categoryId: [{ required: true, message: "请选择分类" }],
  name: [{ required: true, message: "请输入名称" }]
};

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
const categoryRules = {
  name: [{ required: true, message: "请输入分类名称" }],
  code: [{ required: true, message: "请输入分类编码" }]
};
const categoryColumns = [
  { title: "名称", dataIndex: "name", key: "name", width: 180 },
  { title: "编码", dataIndex: "code", key: "code", width: 180 },
  { title: "排序", dataIndex: "sortOrder", key: "sortOrder", width: 100 },
  { title: "操作", key: "action", width: 140 }
];

function formatProductType(type: AiMarketplaceProductType) {
  return productTypeOptions.find((item) => item.value === type)?.label ?? "未知";
}

function formatStatus(status: AiMarketplaceProductStatus) {
  return statusOptions.find((item) => item.value === status)?.label ?? "未知";
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
}

async function loadProducts() {
  loading.value = true;
  try {
    const result = await getAiMarketplaceProductsPaged(
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
    products.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || "加载市场商品失败");
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
    const detail = await getAiMarketplaceProductById(id);
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
    message.error((error as Error).message || "加载商品详情失败");
  }
}

function closeProductModal() {
  productModalOpen.value = false;
  productFormRef.value?.resetFields();
}

async function submitProduct() {
  try {
    await productFormRef.value?.validate();
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
      message.success("商品更新成功");
    } else {
      await createAiMarketplaceProduct(payload);
      message.success("商品创建成功");
    }

    productModalOpen.value = false;
    await loadProducts();
  } catch (error: unknown) {
    message.error((error as Error).message || "提交商品失败");
  } finally {
    productSubmitting.value = false;
  }
}

async function handleDeleteProduct(id: number) {
  try {
    await deleteAiMarketplaceProduct(id);
    message.success("删除成功");
    await loadProducts();
  } catch (error: unknown) {
    message.error((error as Error).message || "删除失败");
  }
}

async function handlePublish(record: AiMarketplaceProductListItem) {
  try {
    const version = record.status === 0 ? "1.0.0" : record.version;
    await publishAiMarketplaceProduct(record.id, { version });
    message.success("发布成功");
    await loadProducts();
  } catch (error: unknown) {
    message.error((error as Error).message || "发布失败");
  }
}

async function toggleFavorite(record: AiMarketplaceProductListItem) {
  try {
    if (record.isFavorited) {
      await unfavoriteAiMarketplaceProduct(record.id);
    } else {
      await favoriteAiMarketplaceProduct(record.id);
    }

    await loadProducts();
  } catch (error: unknown) {
    message.error((error as Error).message || "更新收藏失败");
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
      message.success("分类更新成功");
    } else {
      await createAiMarketplaceCategory(payload);
      message.success("分类创建成功");
    }

    editingCategoryId.value = null;
    await loadCategories();
    await loadProducts();
  } catch (error: unknown) {
    message.error((error as Error).message || "保存分类失败");
  } finally {
    categorySubmitting.value = false;
  }
}

async function handleDeleteCategory(id: number) {
  try {
    await deleteAiMarketplaceCategory(id);
    message.success("分类删除成功");
    await loadCategories();
  } catch (error: unknown) {
    message.error((error as Error).message || "删除分类失败");
  }
}

onMounted(async () => {
  await loadCategories();
  await loadProducts();
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

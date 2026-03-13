<template>
  <a-card :title="`AI 市场详情 #${productId}`" :bordered="false">
    <template #extra>
      <a-space>
        <a-button @click="goBack">返回</a-button>
        <a-button :loading="publishLoading" @click="handlePublish">发布</a-button>
        <a-button :loading="downloadLoading" @click="handleMarkDownload">记录下载</a-button>
        <a-button :loading="favoriteLoading" @click="toggleFavorite">
          {{ detail?.isFavorited ? "取消收藏" : "收藏" }}
        </a-button>
      </a-space>
    </template>

    <a-spin :spinning="loading">
      <a-descriptions v-if="detail" :column="2" bordered size="small">
        <a-descriptions-item label="名称">{{ detail.name }}</a-descriptions-item>
        <a-descriptions-item label="分类">{{ detail.categoryName }}</a-descriptions-item>
        <a-descriptions-item label="版本">{{ detail.version }}</a-descriptions-item>
        <a-descriptions-item label="状态">{{ formatStatus(detail.status) }}</a-descriptions-item>
        <a-descriptions-item label="下载数">{{ detail.downloadCount }}</a-descriptions-item>
        <a-descriptions-item label="收藏数">{{ detail.favoriteCount }}</a-descriptions-item>
        <a-descriptions-item label="摘要" :span="2">{{ detail.summary || "-" }}</a-descriptions-item>
        <a-descriptions-item label="描述" :span="2">{{ detail.description || "-" }}</a-descriptions-item>
        <a-descriptions-item label="标签" :span="2">
          <a-space wrap>
            <a-tag v-for="tag in detail.tags" :key="tag">{{ tag }}</a-tag>
          </a-space>
        </a-descriptions-item>
      </a-descriptions>
      <a-empty v-else description="商品不存在" />
    </a-spin>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  favoriteAiMarketplaceProduct,
  getAiMarketplaceProductById,
  markAiMarketplaceProductDownloaded,
  publishAiMarketplaceProduct,
  unfavoriteAiMarketplaceProduct,
  type AiMarketplaceProductDetail,
  type AiMarketplaceProductStatus
} from "@/services/api-ai-marketplace";

const route = useRoute();
const router = useRouter();
const productId = computed(() => Number(route.params.id));

const detail = ref<AiMarketplaceProductDetail | null>(null);
const loading = ref(false);
const publishLoading = ref(false);
const favoriteLoading = ref(false);
const downloadLoading = ref(false);

function formatStatus(status: AiMarketplaceProductStatus) {
  if (status === 1) {
    return "已发布";
  }

  if (status === 2) {
    return "已归档";
  }

  return "草稿";
}

async function loadDetail() {
  loading.value = true;
  try {
    detail.value = await getAiMarketplaceProductById(productId.value);
  } catch (error: unknown) {
    message.error((error as Error).message || "加载市场商品失败");
    detail.value = null;
  } finally {
    loading.value = false;
  }
}

function goBack() {
  void router.push("/ai/marketplace");
}

async function handlePublish() {
  if (!detail.value) {
    return;
  }

  publishLoading.value = true;
  try {
    const version = detail.value.status === 0 ? "1.0.0" : detail.value.version;
    await publishAiMarketplaceProduct(detail.value.id, { version });
    message.success("发布成功");
    await loadDetail();
  } catch (error: unknown) {
    message.error((error as Error).message || "发布失败");
  } finally {
    publishLoading.value = false;
  }
}

async function toggleFavorite() {
  if (!detail.value) {
    return;
  }

  favoriteLoading.value = true;
  try {
    if (detail.value.isFavorited) {
      await unfavoriteAiMarketplaceProduct(detail.value.id);
    } else {
      await favoriteAiMarketplaceProduct(detail.value.id);
    }

    await loadDetail();
  } catch (error: unknown) {
    message.error((error as Error).message || "更新收藏状态失败");
  } finally {
    favoriteLoading.value = false;
  }
}

async function handleMarkDownload() {
  if (!detail.value) {
    return;
  }

  downloadLoading.value = true;
  try {
    await markAiMarketplaceProductDownloaded(detail.value.id);
    message.success("已记录下载");
    await loadDetail();
  } catch (error: unknown) {
    message.error((error as Error).message || "记录下载失败");
  } finally {
    downloadLoading.value = false;
  }
}

onMounted(() => {
  void loadDetail();
});
</script>

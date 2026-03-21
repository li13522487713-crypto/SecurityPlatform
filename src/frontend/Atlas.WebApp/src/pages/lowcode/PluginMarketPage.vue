<template>
  <div class="plugin-market-page">
    <a-page-header :title="t('lowcode.plugin.title')" :subtitle="t('lowcode.plugin.subtitle')" />

    <a-card :bordered="false" class="filter-card">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('lowcode.plugin.phSearch')"
          allow-clear
          style="width: 280px"
          @search="handleSearch"
        />
        <a-select
          v-model:value="selectedCategory"
          :placeholder="t('lowcode.plugin.phCategory')"
          allow-clear
          style="width: 160px"
          @change="handleSearch"
        >
          <a-select-option value="General">{{ t("lowcode.plugin.catGeneral") }}</a-select-option>
          <a-select-option value="FieldType">{{ t("lowcode.plugin.catField") }}</a-select-option>
          <a-select-option value="Validator">{{ t("lowcode.plugin.catValidator") }}</a-select-option>
          <a-select-option value="DataSource">{{ t("lowcode.plugin.catDataSource") }}</a-select-option>
          <a-select-option value="FlowNode">{{ t("lowcode.plugin.catFlowNode") }}</a-select-option>
          <a-select-option value="GridRenderer">{{ t("lowcode.plugin.catGrid") }}</a-select-option>
          <a-select-option value="Theme">{{ t("lowcode.plugin.catTheme") }}</a-select-option>
        </a-select>
      </a-space>
    </a-card>

    <a-spin :spinning="loading">
      <div class="plugin-grid">
        <a-card
          v-for="entry in entries"
          :key="entry.code"
          hoverable
          class="plugin-card"
          @click="openDetail(entry.code)"
        >
          <template #cover>
            <div class="plugin-icon-wrap">
              <img v-if="entry.iconUrl" :src="entry.iconUrl" :alt="entry.name" class="plugin-icon" />
              <AppstoreOutlined v-else class="plugin-icon-default" />
            </div>
          </template>
          <a-card-meta :title="entry.name" :description="entry.description" />
          <div class="plugin-meta">
            <a-tag :color="categoryColor(entry.category)">{{ entry.category }}</a-tag>
            <span class="plugin-version">v{{ entry.latestVersion }}</span>
            <span class="plugin-downloads">{{ t("lowcode.plugin.installs", { n: entry.downloads }) }}</span>
          </div>
        </a-card>
      </div>

      <a-empty v-if="!loading && entries.length === 0" :description="t('lowcode.plugin.empty')" />
    </a-spin>

    <a-pagination
      v-if="total > 0"
      v-model:current="pageIndex"
      :total="total"
      :page-size="pageSize"
      show-quick-jumper
      class="pagination"
      @change="fetchEntries"
    />

    <PluginDetailDrawer
      v-if="detailCode"
      :code="detailCode"
      @close="detailCode = null"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { AppstoreOutlined } from "@ant-design/icons-vue";
import { message } from "ant-design-vue";
import { searchPluginMarket } from "@/services/api-plugin";
import type { PluginMarketEntry } from "@/types/plugin";
import PluginDetailDrawer from "./PluginDetailDrawer.vue";

const { t } = useI18n();

const loading = ref(false);
const keyword = ref("");
const selectedCategory = ref<string | undefined>();
const entries = ref<PluginMarketEntry[]>([]);
const total = ref(0);
const pageIndex = ref(1);
const pageSize = 20;
const detailCode = ref<string | null>(null);

async function fetchEntries() {
  loading.value = true;
  try {
    const res = await searchPluginMarket({
      keyword: keyword.value || undefined,
      category: selectedCategory.value,
      pageIndex: pageIndex.value,
      pageSize,
    });
    if (res.success && res.data) {
      entries.value = res.data.items;
      total.value = res.data.total;
    }
  } catch {
    message.error(t("lowcode.plugin.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  pageIndex.value = 1;
  fetchEntries();
}

function openDetail(code: string) {
  detailCode.value = code;
}

function categoryColor(cat: string) {
  const map: Record<string, string> = {
    General: "default",
    FieldType: "blue",
    Validator: "green",
    DataSource: "orange",
    FlowNode: "purple",
    GridRenderer: "cyan",
    Theme: "magenta",
  };
  return map[cat] ?? "default";
}

onMounted(fetchEntries);
</script>

<style scoped>
.plugin-market-page {
  padding: 0 24px 24px;
}
.filter-card {
  margin-bottom: 16px;
}
.plugin-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 16px;
  margin-bottom: 16px;
}
.plugin-card {
  cursor: pointer;
}
.plugin-icon-wrap {
  height: 120px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f5f5f5;
}
.plugin-icon {
  max-height: 80px;
  max-width: 80%;
  object-fit: contain;
}
.plugin-icon-default {
  font-size: 48px;
  color: #bfbfbf;
}
.plugin-meta {
  margin-top: 8px;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.plugin-version {
  font-size: 12px;
  color: #8c8c8c;
}
.plugin-downloads {
  font-size: 12px;
  color: #8c8c8c;
  margin-left: auto;
}
.pagination {
  text-align: right;
  margin-top: 16px;
}
</style>

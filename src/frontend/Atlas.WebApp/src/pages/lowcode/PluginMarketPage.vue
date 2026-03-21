<template>
  <div class="plugin-market-page">
    <a-page-header title="插件市场" subtitle="浏览并安装扩展插件" />

    <!-- 筛选区 -->
    <a-card :bordered="false" class="filter-card">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          placeholder="搜索插件名称、描述或代码"
          allow-clear
          style="width: 280px"
          @search="handleSearch"
        />
        <a-select
          v-model:value="selectedCategory"
          placeholder="分类"
          allow-clear
          style="width: 160px"
          @change="handleSearch"
        >
          <a-select-option value="General">通用</a-select-option>
          <a-select-option value="FieldType">字段类型</a-select-option>
          <a-select-option value="Validator">验证器</a-select-option>
          <a-select-option value="DataSource">数据源</a-select-option>
          <a-select-option value="FlowNode">流程节点</a-select-option>
          <a-select-option value="GridRenderer">表格渲染</a-select-option>
          <a-select-option value="Theme">主题</a-select-option>
        </a-select>
      </a-space>
    </a-card>

    <!-- 插件列表 -->
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
            <span class="plugin-downloads">{{ entry.downloads }} 次安装</span>
          </div>
        </a-card>
      </div>

      <a-empty v-if="!loading && entries.length === 0" description="暂无插件" />
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

    <!-- 插件详情 Drawer -->
    <PluginDetailDrawer
      v-if="detailCode"
      :code="detailCode"
      @close="detailCode = null"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { AppstoreOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import { searchPluginMarket } from '@/services/api-plugin'
import type { PluginMarketEntry } from '@/types/plugin'
import PluginDetailDrawer from './PluginDetailDrawer.vue'

const loading = ref(false)
const keyword = ref('')
const selectedCategory = ref<string | undefined>()
const entries = ref<PluginMarketEntry[]>([])
const total = ref(0)
const pageIndex = ref(1)
const pageSize = 20
const detailCode = ref<string | null>(null)

async function fetchEntries() {
  loading.value = true
  try {
    const res = await searchPluginMarket({
      keyword: keyword.value || undefined,
      category: selectedCategory.value,
      pageIndex: pageIndex.value,
      pageSize,
    })
    if (res.success && res.data) {
      entries.value = res.data.items
      total.value = res.data.total
    }
  } catch {
    message.error('加载失败')
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  pageIndex.value = 1
  fetchEntries()
}

function openDetail(code: string) {
  detailCode.value = code
}

function categoryColor(cat: string) {
  const map: Record<string, string> = {
    General: 'default',
    FieldType: 'blue',
    Validator: 'green',
    DataSource: 'orange',
    FlowNode: 'purple',
    GridRenderer: 'cyan',
    Theme: 'magenta',
  }
  return map[cat] ?? 'default'
}

onMounted(fetchEntries)
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

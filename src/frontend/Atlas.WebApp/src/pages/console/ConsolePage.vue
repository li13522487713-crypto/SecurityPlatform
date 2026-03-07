<template>
  <div class="console-page">
    <a-row :gutter="16" class="quick-actions">
      <a-col :span="8">
        <a-card hoverable @click="go('/console/apps')">
          <a-statistic title="应用中心" :value="apps.length" suffix="个应用" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card hoverable @click="go('/console/datasources')">
          <a-statistic title="数据源管理" :value="'进入'" />
        </a-card>
      </a-col>
      <a-col :span="8">
        <a-card hoverable @click="go('/console/settings/system/configs')">
          <a-statistic title="系统设置" :value="'进入'" />
        </a-card>
      </a-col>
    </a-row>

    <a-card title="应用列表" :loading="loading">
      <template #extra>
        <a-space>
          <a-input-search
            v-model:value="keyword"
            placeholder="搜索应用名称/标识"
            style="width: 240px"
            allow-clear
            @search="loadApps"
          />
          <a-button type="primary" @click="createWizardVisible = true">新建应用</a-button>
        </a-space>
      </template>

      <a-row :gutter="[16, 16]">
        <a-col v-for="item in apps" :key="item.id" :xs="24" :sm="12" :md="8" :lg="6">
          <a-card hoverable @click="openApp(item.id)">
            <a-card-meta :title="item.name" :description="item.description || '暂无描述'" />
            <template #actions>
              <span>{{ item.appKey }}</span>
              <a-tag :color="item.status === 'Published' ? 'green' : 'default'">{{ item.status }}</a-tag>
            </template>
          </a-card>
        </a-col>
      </a-row>
    </a-card>

    <AppCreateWizard
      v-model:open="createWizardVisible"
      @created="loadApps"
    />
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import type { LowCodeAppListItem } from "@/types/lowcode";
import { getLowCodeAppsPaged } from "@/services/lowcode";
import AppCreateWizard from "@/pages/console/components/AppCreateWizard.vue";

const router = useRouter();
const loading = ref(false);
const keyword = ref("");
const apps = ref<LowCodeAppListItem[]>([]);
const createWizardVisible = ref(false);

async function loadApps() {
  loading.value = true;
  try {
    const result = await getLowCodeAppsPaged({
      pageIndex: 1,
      pageSize: 60,
      keyword: keyword.value || undefined
    });
    apps.value = result.items;
  } catch (error) {
    message.error((error as Error).message || "加载应用失败");
  } finally {
    loading.value = false;
  }
}

function openApp(id: string) {
  router.push(`/apps/${id}`);
}

function go(path: string) {
  router.push(path);
}

onMounted(() => {
  loadApps();
});
</script>

<style scoped>
.console-page {
  padding: 16px;
}

.quick-actions {
  margin-bottom: 16px;
}
</style>

<template>
  <a-card title="AI 应用管理" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          placeholder="搜索应用名称"
          style="width: 260px"
          @search="loadData"
        />
        <a-button @click="handleReset">重置</a-button>
        <a-button type="primary" @click="goCreate">新建应用</a-button>
      </a-space>
    </div>

    <a-table row-key="id" :columns="columns" :data-source="list" :loading="loading" :pagination="false">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="record.status === 1 ? 'green' : 'default'">
            {{ record.status === 1 ? "已发布" : "草稿" }}
          </a-tag>
        </template>
        <template v-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="goEdit(record.id)">编辑</a-button>
            <a-button type="link" @click="handlePublish(record.id)">发布</a-button>
            <a-button type="link" @click="showVersion(record.id)">版本</a-button>
            <a-button type="link" @click="openCopy(record.id)">资源复制</a-button>
            <a-popconfirm title="确认删除该应用？" @confirm="handleDelete(record.id)">
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
        @change="loadData"
      />
    </div>

    <a-modal
      v-model:open="versionModalOpen"
      title="版本检查"
      :footer="null"
    >
      <a-descriptions v-if="versionInfo" bordered :column="1" size="small">
        <a-descriptions-item label="当前发布版本号">{{ versionInfo.currentPublishVersion }}</a-descriptions-item>
        <a-descriptions-item label="最新版本">{{ versionInfo.latestVersion ?? "-" }}</a-descriptions-item>
        <a-descriptions-item label="最新发布时间">{{ versionInfo.latestPublishedAt ?? "-" }}</a-descriptions-item>
      </a-descriptions>
    </a-modal>

    <a-modal
      v-model:open="copyModalOpen"
      title="提交资源复制任务"
      :confirm-loading="copySubmitting"
      @ok="submitCopyTask"
      @cancel="copyModalOpen = false"
    >
      <a-form layout="vertical">
        <a-form-item label="源应用ID">
          <a-input-number v-model:value="copySourceAppId" :min="1" style="width: 100%" />
        </a-form-item>
        <a-form-item v-if="copyProgress">
          <a-alert
            type="info"
            show-icon
            :message="`最新任务 #${copyProgress.taskId} 状态：${copyStatusLabel(copyProgress.status)}`"
            :description="`总计 ${copyProgress.totalItems}，已复制 ${copyProgress.copiedItems}${copyProgress.errorMessage ? `，错误：${copyProgress.errorMessage}` : ''}`"
          />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  checkAiAppVersion,
  deleteAiApp,
  getAiAppLatestResourceCopyTask,
  getAiAppsPaged,
  publishAiApp,
  submitAiAppResourceCopy,
  type AiAppListItem,
  type AiAppResourceCopyTaskProgress,
  type AiAppVersionCheckResult
} from "@/services/api-ai-app";

const router = useRouter();
const keyword = ref("");
const list = ref<AiAppListItem[]>([]);
const loading = ref(false);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const columns = [
  { title: "名称", dataIndex: "name", key: "name", width: 220 },
  { title: "描述", dataIndex: "description", key: "description", ellipsis: true },
  { title: "状态", key: "status", width: 100 },
  { title: "发布版本", dataIndex: "publishVersion", key: "publishVersion", width: 100 },
  { title: "更新时间", dataIndex: "updatedAt", key: "updatedAt", width: 200 },
  { title: "操作", key: "action", width: 360 }
];

const versionModalOpen = ref(false);
const versionInfo = ref<AiAppVersionCheckResult | null>(null);

const copyModalOpen = ref(false);
const copyAppId = ref<number | null>(null);
const copySourceAppId = ref<number | undefined>(undefined);
const copySubmitting = ref(false);
const copyProgress = ref<AiAppResourceCopyTaskProgress | null>(null);

async function loadData() {
  loading.value = true;
  try {
    const result = await getAiAppsPaged(
      { pageIndex: pageIndex.value, pageSize: pageSize.value },
      keyword.value || undefined
    );
    list.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || "加载应用列表失败");
  } finally {
    loading.value = false;
  }
}

function handleReset() {
  keyword.value = "";
  pageIndex.value = 1;
  void loadData();
}

function goCreate() {
  void router.push("/ai/apps/0/edit");
}

function goEdit(id: number) {
  void router.push(`/ai/apps/${id}/edit`);
}

async function handlePublish(id: number) {
  try {
    await publishAiApp(id);
    message.success("发布成功");
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || "发布失败");
  }
}

async function showVersion(id: number) {
  try {
    versionInfo.value = await checkAiAppVersion(id);
    versionModalOpen.value = true;
  } catch (error: unknown) {
    message.error((error as Error).message || "查询版本失败");
  }
}

async function openCopy(id: number) {
  copyAppId.value = id;
  copySourceAppId.value = undefined;
  copyProgress.value = null;
  copyModalOpen.value = true;
  try {
    copyProgress.value = await getAiAppLatestResourceCopyTask(id);
  } catch {
    copyProgress.value = null;
  }
}

async function submitCopyTask() {
  if (!copyAppId.value || !copySourceAppId.value) {
    message.warning("请输入源应用ID");
    return;
  }

  copySubmitting.value = true;
  try {
    await submitAiAppResourceCopy(copyAppId.value, copySourceAppId.value);
    message.success("资源复制任务已提交");
    copyProgress.value = await getAiAppLatestResourceCopyTask(copyAppId.value);
  } catch (error: unknown) {
    message.error((error as Error).message || "提交复制任务失败");
  } finally {
    copySubmitting.value = false;
  }
}

async function handleDelete(id: number) {
  try {
    await deleteAiApp(id);
    message.success("删除成功");
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || "删除失败");
  }
}

function copyStatusLabel(status: number) {
  if (status === 1) return "进行中";
  if (status === 2) return "已完成";
  if (status === 3) return "失败";
  return "待执行";
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

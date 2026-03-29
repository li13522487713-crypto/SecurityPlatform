<template>
  <div class="app-db-migration-page">
    <a-page-header title="应用数据库迁移中心" sub-title="应用级独立数据库迁移、校验与切换">
      <template #extra>
        <a-space>
          <a-input-number v-model:value="newAppId" :min="1" :precision="0" placeholder="应用实例ID" />
          <a-button type="primary" :loading="creating" @click="handleCreate">新建迁移任务</a-button>
          <a-button :loading="loading" @click="loadTasks">刷新</a-button>
        </a-space>
      </template>
    </a-page-header>

    <a-card class="playbook-card" size="small" title="切库作业顺序（标准流程）">
      <a-typography-paragraph>
        1. SchemaInit（建表） -> 2. SchemaReady（结构确认） -> 3. DataSync（数据同步） ->
        4. IntegrityCheck（一致性校验） -> 5. Cutover（切到 AppOnly）
      </a-typography-paragraph>
      <a-typography-text type="secondary">
        说明：切换与回切都会自动触发租户+应用级连接缓存失效，避免旧策略残留。
      </a-typography-text>
      <a-divider style="margin: 12px 0" />
      <a-typography-paragraph style="margin-bottom: 8px">验收回归清单：</a-typography-paragraph>
      <a-space direction="vertical" size="small">
        <a-typography-text>场景A：同步进行中访问旧运行态链接，应重定向到安全入口且不出现 500。</a-typography-text>
        <a-typography-text>场景B：切换完成后访问旧链接，应正常进入运行页。</a-typography-text>
        <a-typography-text>场景C：执行回切后访问旧链接，应按主库策略稳定可用。</a-typography-text>
        <a-typography-text>场景D：多租户并发验证，不应出现跨租户误拦截。</a-typography-text>
      </a-space>
    </a-card>

    <a-table
      row-key="id"
      :columns="columns"
      :data-source="tasks"
      :loading="loading"
      :pagination="false"
      class="task-table"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'progress'">
          <a-progress :percent="Number(record.progressPercent || 0)" size="small" />
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button size="small" @click="runPrecheck(record.id)">预检查</a-button>
            <a-button size="small" type="primary" @click="runStart(record.id)">迁移</a-button>
            <a-button size="small" @click="runValidate(record.id)">校验</a-button>
            <a-button size="small" danger @click="runCutover(record.id)">切换</a-button>
            <a-button size="small" @click="runRollback(record.id)">回切</a-button>
          </a-space>
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { message } from "ant-design-vue";
import {
  createAppMigrationTask,
  cutoverAppMigrationTask,
  precheckAppMigrationTask,
  queryAppMigrationTasks,
  rollbackAppMigrationTask,
  startAppMigrationTask,
  validateAppMigrationTask,
  type AppMigrationTaskListItem
} from "@/services/api-app-migration";

const loading = ref(false);
const creating = ref(false);
const newAppId = ref<number>(0);
const tasks = ref<AppMigrationTaskListItem[]>([]);

const columns = [
  { title: "任务ID", dataIndex: "id", key: "id", width: 180 },
  { title: "应用实例", dataIndex: "appInstanceId", key: "appInstanceId", width: 120 },
  { title: "状态", dataIndex: "status", key: "status", width: 140 },
  { title: "阶段", dataIndex: "phase", key: "phase", width: 140 },
  { title: "进度", dataIndex: "progressPercent", key: "progress", width: 200 },
  { title: "错误摘要", dataIndex: "errorSummary", key: "errorSummary" },
  { title: "操作", key: "action", width: 420 }
];

async function loadTasks() {
  loading.value = true;
  try {
    const result = await queryAppMigrationTasks(1, 50);
    tasks.value = result.items;
  } catch (error) {
    message.error((error as Error).message || "加载迁移任务失败");
  } finally {
    loading.value = false;
  }
}

async function handleCreate() {
  if (!newAppId.value || newAppId.value <= 0) {
    message.warning("请先输入应用实例ID");
    return;
  }

  creating.value = true;
  try {
    await createAppMigrationTask(newAppId.value);
    message.success("迁移任务已创建");
    await loadTasks();
  } catch (error) {
    message.error((error as Error).message || "创建迁移任务失败");
  } finally {
    creating.value = false;
  }
}

async function runPrecheck(taskId: string) {
  try {
    const result = await precheckAppMigrationTask(taskId);
    message.success(result.canStart ? "预检查通过" : "预检查未通过");
    await loadTasks();
  } catch (error) {
    message.error((error as Error).message || "预检查失败");
  }
}

async function runStart(taskId: string) {
  try {
    await startAppMigrationTask(taskId);
    message.success("迁移已启动");
    await loadTasks();
  } catch (error) {
    message.error((error as Error).message || "迁移执行失败");
  }
}

async function runValidate(taskId: string) {
  try {
    const result = await validateAppMigrationTask(taskId);
    message.success(result.passed ? "校验通过" : "校验未通过");
    await loadTasks();
  } catch (error) {
    message.error((error as Error).message || "完整性校验失败");
  }
}

async function runCutover(taskId: string) {
  try {
    await cutoverAppMigrationTask(taskId, true, false);
    message.success("已切换到应用独立库");
    await loadTasks();
  } catch (error) {
    message.error((error as Error).message || "切换失败");
  }
}

async function runRollback(taskId: string) {
  try {
    await rollbackAppMigrationTask(taskId);
    message.success("已回切到主库");
    await loadTasks();
  } catch (error) {
    message.error((error as Error).message || "回切失败");
  }
}

onMounted(() => {
  void loadTasks();
});
</script>

<style scoped>
.app-db-migration-page {
  padding: 24px;
}

.task-table {
  margin-top: 16px;
}

.playbook-card {
  margin-top: 16px;
}
</style>

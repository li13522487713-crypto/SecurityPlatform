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
</style>

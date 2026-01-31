<template>
  <a-card title="系统总览" class="page-card">
    <a-skeleton :loading="loading" active>
      <a-row :gutter="16">
        <a-col :span="6">
          <a-statistic title="资产总量" :value="metrics?.assetsTotal ?? 0" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="今日告警" :value="metrics?.alertsToday ?? 0" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="今日审计" :value="metrics?.auditEventsToday ?? 0" />
        </a-col>
        <a-col :span="6">
          <a-statistic title="运行中实例" :value="metrics?.runningInstances ?? 0" />
        </a-col>
      </a-row>
    </a-skeleton>

    <a-divider>组织与权限</a-divider>
    <a-row :gutter="[16, 16]">
      <a-col v-for="entry in organizationEntries" :key="entry.title" :span="6">
        <a-card hoverable class="quick-card" :title="entry.title" @click="go(entry.path)">
          <p>{{ entry.description }}</p>
        </a-card>
      </a-col>
    </a-row>

    <a-divider>业务与应用</a-divider>
    <a-row :gutter="[16, 16]">
      <a-col v-for="entry in businessEntries" :key="entry.title" :span="6">
        <a-card hoverable class="quick-card" :title="entry.title" @click="go(entry.path)">
          <p>{{ entry.description }}</p>
        </a-card>
      </a-col>
    </a-row>

    <a-divider>安全运营</a-divider>
    <a-row :gutter="[16, 16]">
      <a-col v-for="entry in securityEntries" :key="entry.title" :span="6">
        <a-card hoverable class="quick-card" :title="entry.title" @click="go(entry.path)">
          <p>{{ entry.description }}</p>
        </a-card>
      </a-col>
    </a-row>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { getVisualizationMetrics } from "@/services/api";
import type { VisualizationMetricsResponse } from "@/types/api";

const router = useRouter();
const loading = ref(false);
const metrics = ref<VisualizationMetricsResponse | null>(null);

const organizationEntries = [
  { title: "员工管理", description: "人员信息 / 角色与权限 / 账号状态", path: "/system/users" },
  { title: "部门管理", description: "组织层级 / 负责人 / 可见范围", path: "/system/departments" },
  { title: "职位管理", description: "岗位序列 / 影响面提示 / 状态", path: "/system/positions" },
  { title: "角色管理", description: "成员 / 权限 / 数据范围", path: "/system/roles" }
];

const businessEntries = [
  { title: "权限管理", description: "功能权限 + 数据权限配置", path: "/system/permissions" },
  { title: "菜单管理", description: "菜单层级 / 权限绑定 / 隐藏", path: "/system/menus" },
  { title: "项目管理", description: "成员分配 / 数据隔离", path: "/system/projects" },
  { title: "应用配置", description: "可见范围 / 项目模式 / 状态", path: "/system/apps" }
];

const securityEntries = [
  { title: "资产中心", description: "资产盘点 / 分类 / 风险定位", path: "/assets" },
  { title: "审计中心", description: "操作留痕 / 风险回溯", path: "/audit" },
  { title: "告警中心", description: "告警聚合 / 处置跟踪", path: "/alert" },
  { title: "审批中心", description: "流程编排 / 审批任务", path: "/approval/flows" }
];

const go = (path: string) => router.push(path);

const loadMetrics = async () => {
  loading.value = true;
  try {
    metrics.value = await getVisualizationMetrics();
  } catch (error) {
    message.error((error as Error).message || "加载指标失败");
  } finally {
    loading.value = false;
  }
};

onMounted(loadMetrics);
</script>

<style scoped>
.quick-card {
  min-height: 110px;
}
</style>

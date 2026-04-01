<template>
  <div class="workspace">
    <a-card class="top-bar" :bordered="false">
      <div class="top-content">
        <div>
          <h3>Agent 团队编辑工作台</h3>
          <div class="sub">团队ID: {{ teamId }}</div>
        </div>
        <a-space>
          <a-button @click="goBack">返回</a-button>
          <a-button :loading="validating" @click="validate">校验</a-button>
          <a-button @click="goDebug">调试</a-button>
          <a-button @click="goRun">运行</a-button>
          <a-button type="primary" :loading="saving" @click="saveTeam">保存</a-button>
          <a-button type="primary" ghost :loading="publishing" @click="publishTeam">发布</a-button>
        </a-space>
      </div>
    </a-card>

    <div class="main-grid">
      <a-card class="left" title="结构树" size="small">
        <a-space direction="vertical" style="width: 100%">
          <a-button block @click="openSubAgentDrawer">新增子代理</a-button>
          <a-divider style="margin: 8px 0" />
          <div class="section-title">子代理列表</div>
          <a-list size="small" :data-source="subAgents">
            <template #renderItem="{ item }">
              <a-list-item>
                <a-space>
                  <span>{{ item.agentName }}</span>
                  <a-tag>{{ item.status }}</a-tag>
                </a-space>
              </a-list-item>
            </template>
          </a-list>
          <a-divider style="margin: 8px 0" />
          <div class="section-title">节点列表</div>
          <a-list size="small" :data-source="nodes">
            <template #renderItem="{ item }">
              <a-list-item>
                <a-space>
                  <span>{{ item.nodeName }}</span>
                  <a-tag>{{ item.executionMode }}</a-tag>
                </a-space>
              </a-list-item>
            </template>
          </a-list>
        </a-space>
      </a-card>

      <a-card class="center" title="编排视图（基础模式）" size="small">
        <a-empty v-if="nodes.length === 0" description="暂无节点，可先创建子代理后配置节点" />
        <a-steps v-else direction="vertical" size="small">
          <a-step v-for="node in nodes" :key="node.id" :title="node.nodeName" :description="node.executionMode" />
        </a-steps>
      </a-card>

      <a-card class="right" title="团队配置面板" size="small">
        <a-form layout="vertical">
          <a-form-item label="团队名称">
            <a-input v-model:value="teamForm.teamName" />
          </a-form-item>
          <a-form-item label="负责人">
            <a-input v-model:value="teamForm.owner" />
          </a-form-item>
          <a-form-item label="描述">
            <a-textarea v-model:value="teamForm.description" :rows="3" />
          </a-form-item>
          <a-form-item label="风险等级">
            <a-select v-model:value="teamForm.riskLevel">
              <a-select-option value="Low">Low</a-select-option>
              <a-select-option value="Medium">Medium</a-select-option>
              <a-select-option value="High">High</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="标签（逗号分隔）">
            <a-input v-model:value="tagsRaw" />
          </a-form-item>
        </a-form>
      </a-card>
    </div>

    <a-card class="bottom" title="校验 / 日志区" size="small">
      <a-alert v-if="validationMessage" type="info" :message="validationMessage" show-icon />
    </a-card>

    <SubAgentConfigDrawer
      :open="subAgentDrawerOpen"
      :team-id="teamId"
      @close="subAgentDrawerOpen = false"
      @saved="reloadSubAgents"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { message } from "ant-design-vue";
import { useRoute, useRouter } from "vue-router";
import {
  getAgentTeamDetail,
  getOrchestrationNodes,
  getSubAgents,
  publishAgentTeam,
  updateAgentTeam,
  validateOrchestration,
  type OrchestrationNodeItem,
  type SubAgentItem
} from "@/services/api-agent-team";
import SubAgentConfigDrawer from "@/components/ai/SubAgentConfigDrawer.vue";
import { resolveCurrentAppId } from "@/utils/app-context";

const route = useRoute();
const router = useRouter();
const teamId = computed(() => Number(route.params.id || 0));
const saving = ref(false);
const publishing = ref(false);
const validating = ref(false);
const validationMessage = ref("");
const subAgentDrawerOpen = ref(false);

const subAgents = ref<SubAgentItem[]>([]);
const nodes = ref<OrchestrationNodeItem[]>([]);

const teamForm = reactive({
  teamName: "",
  owner: "",
  description: "",
  riskLevel: "Low" as "Low" | "Medium" | "High",
  collaborators: [] as string[]
});
const tagsRaw = ref("");

function basePath() {
  const appId = resolveCurrentAppId(route);
  return appId ? `/apps/${appId}/multi-agent` : "/ai/multi-agent";
}

function goBack() {
  void router.push(basePath());
}

function goDebug() {
  void router.push(`${basePath()}/${teamId.value}/debug`);
}

function goRun() {
  void router.push(`${basePath()}/${teamId.value}/run`);
}

function openSubAgentDrawer() {
  subAgentDrawerOpen.value = true;
}

async function loadTeam() {
  if (!teamId.value) return;
  const detail = await getAgentTeamDetail(teamId.value);
  teamForm.teamName = detail.teamName;
  teamForm.owner = detail.owner;
  teamForm.description = detail.description || "";
  teamForm.riskLevel = detail.riskLevel;
  teamForm.collaborators = detail.collaborators || [];
  tagsRaw.value = (detail.tags || []).join(",");
}

async function reloadSubAgents() {
  if (!teamId.value) return;
  subAgents.value = await getSubAgents(teamId.value);
}

async function reloadNodes() {
  if (!teamId.value) return;
  nodes.value = await getOrchestrationNodes(teamId.value);
}

async function saveTeam() {
  if (!teamId.value) return;
  if (!teamForm.teamName.trim() || !teamForm.owner.trim()) {
    message.warning("请填写团队名称和负责人");
    return;
  }

  saving.value = true;
  try {
    await updateAgentTeam(teamId.value, {
      teamName: teamForm.teamName.trim(),
      owner: teamForm.owner.trim(),
      description: teamForm.description.trim() || undefined,
      collaborators: teamForm.collaborators,
      riskLevel: teamForm.riskLevel,
      tags: tagsRaw.value.split(",").map(x => x.trim()).filter(Boolean),
      defaultModelPolicyJson: "{}",
      budgetPolicyJson: "{}",
      permissionScopeJson: "{}"
    });
    message.success("保存成功");
    await loadTeam();
  } catch (err) {
    message.error((err as Error).message || "保存失败");
  } finally {
    saving.value = false;
  }
}

async function validate() {
  if (!teamId.value) return;
  validating.value = true;
  try {
    await validateOrchestration(teamId.value);
    validationMessage.value = "结构校验通过";
    message.success("编排校验通过");
  } catch (err) {
    validationMessage.value = (err as Error).message || "校验失败";
    message.error(validationMessage.value);
  } finally {
    validating.value = false;
  }
}

async function publishTeam() {
  if (!teamId.value) return;
  publishing.value = true;
  try {
    await publishAgentTeam(teamId.value, { releaseNote: "发布 Agent 团队版本" });
    message.success("发布成功");
  } catch (err) {
    message.error((err as Error).message || "发布失败");
  } finally {
    publishing.value = false;
  }
}

onMounted(async () => {
  try {
    await Promise.all([loadTeam(), reloadSubAgents(), reloadNodes()]);
  } catch (err) {
    message.error((err as Error).message || "加载失败");
  }
});
</script>

<style scoped>
.workspace {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.top-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.sub {
  color: #888;
  font-size: 12px;
}

.main-grid {
  display: grid;
  grid-template-columns: 280px 1fr 360px;
  gap: 12px;
  min-height: 520px;
}

.left,
.center,
.right,
.bottom,
.top-bar {
  height: 100%;
}

.section-title {
  font-size: 12px;
  color: #888;
}
</style>

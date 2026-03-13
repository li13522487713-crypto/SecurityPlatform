<template>
  <a-card :bordered="false">
    <template #title>
      <a-space>
        <a-button type="link" @click="goBack">返回列表</a-button>
        <span>Agent 编辑器</span>
      </a-space>
    </template>

    <a-row :gutter="16">
      <a-col :span="8">
        <a-card size="small" title="基础设置">
          <a-form layout="vertical">
            <a-form-item label="名称">
              <a-input v-model:value="form.name" />
            </a-form-item>
            <a-form-item label="描述">
              <a-textarea v-model:value="form.description" :rows="2" />
            </a-form-item>
            <a-form-item label="头像 URL">
              <a-input v-model:value="form.avatarUrl" />
            </a-form-item>
            <a-form-item label="模型配置">
              <a-select v-model:value="form.modelConfigId" allow-clear :options="modelOptions" />
            </a-form-item>
            <a-form-item label="模型覆盖">
              <a-input v-model:value="form.modelName" />
            </a-form-item>
            <a-form-item label="Temperature">
              <a-slider v-model:value="form.temperature" :min="0" :max="2" :step="0.1" />
            </a-form-item>
            <a-form-item label="MaxTokens">
              <a-input-number v-model:value="form.maxTokens" :min="1" :max="128000" style="width: 100%" />
            </a-form-item>
            <a-form-item label="知识库 ID（逗号分隔）">
              <a-input v-model:value="knowledgeBaseInput" placeholder="例如：1001,1002" />
            </a-form-item>
          </a-form>
        </a-card>
      </a-col>

      <a-col :span="8">
        <a-card size="small" title="System Prompt">
          <a-textarea v-model:value="form.systemPrompt" :rows="26" />
          <div class="counter">字符数：{{ form.systemPrompt.length }}</div>
        </a-card>
      </a-col>

      <a-col :span="8">
        <a-card size="small" title="预览面板">
          <a-alert
            message="MVP 预览"
            description="此阶段先提供配置编辑与保存，聊天预览将在对话阶段接入。"
            type="info"
            show-icon
          />
          <div class="preview-box">
            <p><strong>当前状态：</strong>{{ agent?.status || "-" }}</p>
            <p><strong>发布版本：</strong>v{{ agent?.publishVersion ?? 0 }}</p>
            <p><strong>最后更新时间：</strong>{{ agent?.updatedAt || "-" }}</p>
          </div>
        </a-card>
      </a-col>
    </a-row>

    <div class="actions">
      <a-space>
        <a-button @click="goBack">取消</a-button>
        <a-button :loading="publishing" @click="handlePublish">发布</a-button>
        <a-button type="primary" :loading="saving" @click="handleSave">保存</a-button>
      </a-space>
    </div>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  getAgentById,
  publishAgent,
  type AgentDetail,
  updateAgent
} from "@/services/api-agent";
import { getEnabledModelConfigs, type ModelConfigDto } from "@/services/api-model-config";

const route = useRoute();
const router = useRouter();
const agentId = Number(route.params.id);

const agent = ref<AgentDetail | null>(null);
const modelConfigs = ref<ModelConfigDto[]>([]);
const saving = ref(false);
const publishing = ref(false);
const knowledgeBaseInput = ref("");

const form = reactive({
  name: "",
  description: "",
  avatarUrl: "",
  systemPrompt: "",
  modelConfigId: undefined as number | undefined,
  modelName: "",
  temperature: 1,
  maxTokens: 2048
});

const modelOptions = computed(() =>
  modelConfigs.value.map((item) => ({
    label: `${item.name} (${item.providerType})`,
    value: item.id
  }))
);

function goBack() {
  void router.push("/ai/agents");
}

async function loadData() {
  try {
    const [detail, models] = await Promise.all([
      getAgentById(agentId),
      getEnabledModelConfigs()
    ]);
    agent.value = detail;
    modelConfigs.value = models;
    Object.assign(form, {
      name: detail.name,
      description: detail.description || "",
      avatarUrl: detail.avatarUrl || "",
      systemPrompt: detail.systemPrompt || "",
      modelConfigId: detail.modelConfigId,
      modelName: detail.modelName || "",
      temperature: detail.temperature ?? 1,
      maxTokens: detail.maxTokens ?? 2048
    });
    knowledgeBaseInput.value = (detail.knowledgeBaseIds || []).join(",");
  } catch (error: unknown) {
    message.error((error as Error).message || "加载 Agent 失败");
  }
}

function parseKnowledgeBaseIds() {
  return knowledgeBaseInput.value
    .split(",")
    .map((item) => Number(item.trim()))
    .filter((item) => Number.isFinite(item) && item > 0);
}

async function handleSave() {
  if (!form.name.trim()) {
    message.warning("名称不能为空");
    return;
  }

  saving.value = true;
  try {
    await updateAgent(agentId, {
      name: form.name,
      description: form.description || undefined,
      avatarUrl: form.avatarUrl || undefined,
      systemPrompt: form.systemPrompt || undefined,
      modelConfigId: form.modelConfigId,
      modelName: form.modelName || undefined,
      temperature: form.temperature,
      maxTokens: form.maxTokens,
      knowledgeBaseIds: parseKnowledgeBaseIds()
    });
    message.success("保存成功");
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || "保存失败");
  } finally {
    saving.value = false;
  }
}

async function handlePublish() {
  publishing.value = true;
  try {
    await publishAgent(agentId);
    message.success("发布成功");
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || "发布失败");
  } finally {
    publishing.value = false;
  }
}

onMounted(() => {
  void loadData();
});
</script>

<style scoped>
.counter {
  margin-top: 8px;
  text-align: right;
  color: rgba(0, 0, 0, 0.45);
}

.preview-box {
  margin-top: 12px;
}

.actions {
  margin-top: 16px;
  text-align: right;
}
</style>

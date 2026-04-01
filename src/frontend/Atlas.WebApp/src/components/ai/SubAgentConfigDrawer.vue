<template>
  <a-drawer :open="open" title="子代理配置" width="560px" @close="emit('close')">
    <a-form layout="vertical">
      <a-form-item label="子代理名称">
        <a-input v-model:value="form.agentName" />
      </a-form-item>
      <a-form-item label="角色">
        <a-input v-model:value="form.role" />
      </a-form-item>
      <a-form-item label="目标">
        <a-textarea v-model:value="form.goal" :rows="3" />
      </a-form-item>
      <a-form-item label="Prompt">
        <a-textarea v-model:value="form.promptTemplate" :rows="6" />
      </a-form-item>
      <a-form-item label="模型配置(JSON)">
        <a-textarea v-model:value="form.modelConfigJson" :rows="4" />
      </a-form-item>
      <a-form-item label="工具权限(JSON)">
        <a-textarea v-model:value="form.toolPermissionsJson" :rows="4" />
      </a-form-item>
      <a-form-item label="知识范围(JSON)">
        <a-textarea v-model:value="form.knowledgeScopesJson" :rows="4" />
      </a-form-item>
      <a-form-item label="输入 Schema(JSON)">
        <a-textarea v-model:value="form.inputSchemaJson" :rows="3" />
      </a-form-item>
      <a-form-item label="输出 Schema(JSON)">
        <a-textarea v-model:value="form.outputSchemaJson" :rows="3" />
      </a-form-item>
      <a-form-item label="超时策略(JSON)">
        <a-textarea v-model:value="form.timeoutPolicyJson" :rows="3" />
      </a-form-item>
      <a-space>
        <a-button @click="emit('close')">取消</a-button>
        <a-button type="primary" :loading="saving" @click="handleSave">保存</a-button>
      </a-space>
    </a-form>
  </a-drawer>
</template>

<script setup lang="ts">
import { reactive, ref, watch } from "vue";
import { createSubAgent, updateSubAgent } from "@/services/api-agent-team";
import { message } from "ant-design-vue";

interface Props {
  open: boolean;
  teamId: number;
  subAgentId?: number;
  initial?: {
    agentName: string;
    role: string;
    goal: string;
  };
}

const props = defineProps<Props>();
const emit = defineEmits<{
  close: [];
  saved: [];
}>();

const form = reactive({
  agentName: "",
  role: "",
  goal: "",
  boundaries: "",
  promptTemplate: "",
  modelConfigJson: "{}",
  toolPermissionsJson: "[]",
  knowledgeScopesJson: "[]",
  inputSchemaJson: "{}",
  outputSchemaJson: "{}",
  memoryPolicyJson: "{}",
  timeoutPolicyJson: "{\"maxSeconds\":120}",
  retryPolicyJson: "{\"maxRetries\":1}",
  fallbackPolicyJson: "{}",
  visibilityPolicyJson: "{}",
  status: "Configured"
});

const saving = ref(false);

watch(
  () => props.initial,
  value => {
    if (!value) {
      return;
    }

    form.agentName = value.agentName || "";
    form.role = value.role || "";
    form.goal = value.goal || "";
  },
  { immediate: true }
);

async function handleSave() {
  if (!form.agentName.trim() || !form.role.trim() || !form.goal.trim()) {
    message.warning("请补齐子代理名称/角色/目标");
    return;
  }

  saving.value = true;
  try {
    const payload = {
      agentName: form.agentName.trim(),
      role: form.role.trim(),
      goal: form.goal.trim(),
      boundaries: form.boundaries,
      promptTemplate: form.promptTemplate || "你是子代理",
      modelConfigJson: form.modelConfigJson,
      toolPermissionsJson: form.toolPermissionsJson,
      knowledgeScopesJson: form.knowledgeScopesJson,
      inputSchemaJson: form.inputSchemaJson,
      outputSchemaJson: form.outputSchemaJson,
      memoryPolicyJson: form.memoryPolicyJson,
      timeoutPolicyJson: form.timeoutPolicyJson,
      retryPolicyJson: form.retryPolicyJson,
      fallbackPolicyJson: form.fallbackPolicyJson,
      visibilityPolicyJson: form.visibilityPolicyJson,
      status: form.status
    };
    if (props.subAgentId) {
      await updateSubAgent(props.teamId, props.subAgentId, payload);
    } else {
      await createSubAgent(props.teamId, payload);
    }

    message.success("保存子代理成功");
    emit("saved");
    emit("close");
  } catch (err) {
    message.error((err as Error).message || "保存子代理失败");
  } finally {
    saving.value = false;
  }
}
</script>

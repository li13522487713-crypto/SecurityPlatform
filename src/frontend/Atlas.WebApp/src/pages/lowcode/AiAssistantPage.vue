<template>
  <div class="ai-assistant-page">
    <div class="ai-content">
      <div class="ai-sidebar">
        <h3>{{ t("lowcode.aiAssistant.sidebarTitle") }}</h3>
        <a-menu v-model:selected-keys="selectedFunction" mode="inline">
          <a-menu-item key="form"><template #icon><FormOutlined /></template>{{ t("lowcode.aiAssistant.menuForm") }}</a-menu-item>
          <a-menu-item key="sql"><template #icon><CodeOutlined /></template>{{ t("lowcode.aiAssistant.menuSql") }}</a-menu-item>
          <a-menu-item key="workflow"><template #icon><BranchesOutlined /></template>{{ t("lowcode.aiAssistant.menuWorkflow") }}</a-menu-item>
        </a-menu>
      </div>
      <div class="ai-main">
        <div class="ai-chat-area">
          <div ref="chatContainerRef" class="chat-messages">
            <div v-for="msg in messages" :key="msg.id" :class="['chat-message', msg.role]">
              <div class="message-bubble">
                <div v-if="msg.role === 'assistant' && msg.resultJson" class="result-section">
                  <a-button type="primary" size="small" style="margin-bottom: 8px" @click="handleApplyResult(msg.resultJson)">{{ t("lowcode.aiAssistant.applyResult") }}</a-button>
                  <pre class="result-json">{{ formatJson(msg.resultJson) }}</pre>
                </div>
                <div v-else class="message-text">{{ msg.content }}</div>
              </div>
            </div>
            <div v-if="generating" class="chat-message assistant">
              <div class="message-bubble"><a-spin size="small" /> {{ t("lowcode.aiAssistant.thinking") }}</div>
            </div>
          </div>
          <div class="chat-input">
            <a-textarea v-model:value="userInput" :placeholder="inputPlaceholder" :rows="3" @keydown.enter.ctrl="sendMessage" />
            <a-button type="primary" :loading="generating" style="margin-top: 8px; align-self: flex-end" @click="sendMessage">
              {{ t("lowcode.aiAssistant.send") }}
            </a-button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import { FormOutlined, CodeOutlined, BranchesOutlined } from "@ant-design/icons-vue";
import { message } from "ant-design-vue";
import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@/types/api";

const { t } = useI18n();

interface ChatMessage { id: string; role: "user" | "assistant"; content: string; resultJson?: string; }

const selectedFunction = ref<string[]>(["form"]);
const userInput = ref("");
const generating = ref(false);
const chatContainerRef = ref<HTMLElement>();

const messages = reactive<ChatMessage[]>([]);

function pushWelcome() {
  messages.push({
    id: crypto.randomUUID(),
    role: "assistant",
    content: t("lowcode.aiAssistant.welcome")
  });
}
pushWelcome();

const inputPlaceholder = computed(() => {
  const fn = selectedFunction.value[0];
  if (fn === "form") return t("lowcode.aiAssistant.phForm");
  if (fn === "sql") return t("lowcode.aiAssistant.phSql");
  return t("lowcode.aiAssistant.phWorkflow");
});

const formatJson = (json: string): string => {
  try { return JSON.stringify(JSON.parse(json), null, 2); }
  catch { return json; }
};

const scrollToBottom = () => { nextTick(() => { if (chatContainerRef.value) chatContainerRef.value.scrollTop = chatContainerRef.value.scrollHeight; }); };

const sendMessage = async () => {
  const text = userInput.value.trim();
  if (!text) return;

  messages.push({ id: crypto.randomUUID(), role: "user", content: text });
  userInput.value = "";
  generating.value = true;
  scrollToBottom();

  const fn = selectedFunction.value[0] || "form";
  const endpointMap: Record<string, string> = { form: "/ai/generate-form", sql: "/ai/generate-sql", workflow: "/ai/suggest-workflow" };

  try {
    const resp = await requestApi<ApiResponse<{ result: string; explanation: string }>>(endpointMap[fn], {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ description: text })
    });
    if (resp.data) {
      messages.push({ id: crypto.randomUUID(), role: "assistant", content: resp.data.explanation, resultJson: resp.data.result });
    } else {
      messages.push({ id: crypto.randomUUID(), role: "assistant", content: t("lowcode.aiAssistant.noResult") });
    }
  } catch (e) {
    messages.push({ id: crypto.randomUUID(), role: "assistant", content: t("lowcode.aiAssistant.errWithMsg", { msg: (e as Error).message }) });
  } finally {
    generating.value = false;
    scrollToBottom();
  }
};

const handleApplyResult = (json: string) => {
  navigator.clipboard.writeText(json).then(() => message.success(t("lowcode.aiAssistant.copyOk"))).catch(() => message.error(t("lowcode.aiAssistant.copyFail")));
};
</script>

<style scoped>
.ai-assistant-page { padding: 24px; height: calc(100vh - 112px); display: flex; flex-direction: column; }
.ai-content { display: flex; flex: 1; gap: 16px; min-height: 0; }
.ai-sidebar { width: 200px; flex-shrink: 0; }
.ai-sidebar h3 { margin: 0 0 12px; font-size: 16px; }
.ai-main { flex: 1; display: flex; flex-direction: column; min-height: 0; }
.ai-chat-area { flex: 1; display: flex; flex-direction: column; border: 1px solid #e8e8e8; border-radius: 8px; overflow: hidden; }
.chat-messages { flex: 1; overflow-y: auto; padding: 16px; display: flex; flex-direction: column; gap: 12px; }
.chat-message { display: flex; }
.chat-message.user { justify-content: flex-end; }
.chat-message.assistant { justify-content: flex-start; }
.message-bubble { max-width: 80%; padding: 10px 14px; border-radius: 12px; font-size: 14px; line-height: 1.6; }
.chat-message.user .message-bubble { background: #1677ff; color: #fff; border-bottom-right-radius: 4px; }
.chat-message.assistant .message-bubble { background: #f5f5f5; color: #333; border-bottom-left-radius: 4px; }
.result-json { background: #fafafa; border: 1px solid #e8e8e8; border-radius: 6px; padding: 12px; font-size: 12px; overflow-x: auto; max-height: 300px; overflow-y: auto; white-space: pre-wrap; word-break: break-all; }
.chat-input { padding: 12px 16px; border-top: 1px solid #e8e8e8; display: flex; flex-direction: column; }
</style>

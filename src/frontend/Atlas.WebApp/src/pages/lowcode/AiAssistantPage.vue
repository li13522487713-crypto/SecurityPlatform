<template>
  <div class="ai-assistant-page">
    <div class="ai-content">
      <div class="ai-sidebar">
        <h3>AI 助手功能</h3>
        <a-menu v-model:selectedKeys="selectedFunction" mode="inline">
          <a-menu-item key="form"><template #icon><FormOutlined /></template>表单生成</a-menu-item>
          <a-menu-item key="sql"><template #icon><CodeOutlined /></template>SQL 生成</a-menu-item>
          <a-menu-item key="workflow"><template #icon><BranchesOutlined /></template>工作流建议</a-menu-item>
        </a-menu>
      </div>
      <div class="ai-main">
        <div class="ai-chat-area">
          <div class="chat-messages" ref="chatContainerRef">
            <div v-for="(msg, idx) in messages" :key="idx" :class="['chat-message', msg.role]">
              <div class="message-bubble">
                <div v-if="msg.role === 'assistant' && msg.resultJson" class="result-section">
                  <a-button type="primary" size="small" @click="handleApplyResult(msg.resultJson)" style="margin-bottom: 8px">应用结果</a-button>
                  <pre class="result-json">{{ formatJson(msg.resultJson) }}</pre>
                </div>
                <div v-else class="message-text">{{ msg.content }}</div>
              </div>
            </div>
            <div v-if="generating" class="chat-message assistant">
              <div class="message-bubble"><a-spin size="small" /> AI 正在思考中...</div>
            </div>
          </div>
          <div class="chat-input">
            <a-textarea v-model:value="userInput" :placeholder="inputPlaceholder" :rows="3" @keydown.enter.ctrl="sendMessage" />
            <a-button type="primary" :loading="generating" @click="sendMessage" style="margin-top: 8px; align-self: flex-end">
              发送 (Ctrl+Enter)
            </a-button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, reactive, ref } from "vue";
import { FormOutlined, CodeOutlined, BranchesOutlined } from "@ant-design/icons-vue";
import { message } from "ant-design-vue";
import { requestApi } from "@/services/api";
import type { ApiResponse } from "@/types/api";

interface ChatMessage { role: "user" | "assistant"; content: string; resultJson?: string; }

const selectedFunction = ref<string[]>(["form"]);
const userInput = ref("");
const generating = ref(false);
const chatContainerRef = ref<HTMLElement>();
const messages = reactive<ChatMessage[]>([
  { role: "assistant", content: "你好！我是 AI 助手，可以帮你生成表单、SQL 或工作流建议。请选择左侧功能并描述你的需求。" }
]);

const inputPlaceholder = computed(() => {
  const fn = selectedFunction.value[0];
  if (fn === "form") return "描述你想要的表单，例如：请帮我创建一个员工请假申请表单，包含姓名、部门、请假类型、开始日期、结束日期、原因...";
  if (fn === "sql") return "描述你需要的 SQL 查询，例如：查询每个部门本月请假超过 3 天的员工...";
  return "描述你需要的工作流程，例如：一个三级审批的采购审批流程...";
});

const formatJson = (json: string): string => {
  try { return JSON.stringify(JSON.parse(json), null, 2); }
  catch { return json; }
};

const scrollToBottom = () => { nextTick(() => { if (chatContainerRef.value) chatContainerRef.value.scrollTop = chatContainerRef.value.scrollHeight; }); };

const sendMessage = async () => {
  const text = userInput.value.trim();
  if (!text) return;

  messages.push({ role: "user", content: text });
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
      messages.push({ role: "assistant", content: resp.data.explanation, resultJson: resp.data.result });
    } else {
      messages.push({ role: "assistant", content: "抱歉，未能生成结果，请重试。" });
    }
  } catch (e) {
    messages.push({ role: "assistant", content: `出错了：${(e as Error).message}` });
  } finally {
    generating.value = false;
    scrollToBottom();
  }
};

const handleApplyResult = (json: string) => {
  navigator.clipboard.writeText(json).then(() => message.success("已复制到剪贴板")).catch(() => message.error("复制失败"));
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

<template>
  <div class="agent-chat-layout">
    <!-- Sidebar: conversation list -->
    <div class="chat-sidebar">
      <div class="sidebar-header">
        <span class="sidebar-title">对话列表</span>
        <a-button type="primary" size="small" @click="handleNewConversation">
          + 新建
        </a-button>
      </div>
      <a-spin :spinning="loadingConversations">
        <div class="conversation-list">
          <div
            v-for="conv in conversations"
            :key="conv.id"
            :class="['conversation-item', { active: currentConvId === conv.id }]"
            @click="selectConversation(conv)"
          >
            <div class="conv-title">{{ conv.title || "新对话" }}</div>
            <div class="conv-meta">{{ formatDate(conv.lastMessageAt || conv.createdAt) }}</div>
            <a-popconfirm
              title="确认删除该对话？"
              placement="right"
              @confirm.stop="handleDeleteConversation(conv.id)"
            >
              <a-button
                class="conv-delete-btn"
                type="text"
                size="small"
                danger
                @click.stop
              >
                <template #icon><span>×</span></template>
              </a-button>
            </a-popconfirm>
          </div>
          <div v-if="conversations.length === 0 && !loadingConversations" class="empty-conv">
            暂无对话，点击「新建」开始
          </div>
        </div>
      </a-spin>
    </div>

    <!-- Main chat area -->
    <div class="chat-main">
      <div class="chat-header">
        <span class="agent-name">{{ agentName }}</span>
        <a-space v-if="currentConvId">
          <a-tooltip title="清除上下文（保留历史，但新消息不使用旧上下文）">
            <a-button size="small" @click="handleClearContext">清除上下文</a-button>
          </a-tooltip>
          <a-tooltip title="清除全部历史消息">
            <a-button size="small" danger @click="handleClearHistory">清除历史</a-button>
          </a-tooltip>
        </a-space>
      </div>

      <div ref="messagesContainer" class="chat-messages">
        <div v-if="!currentConvId" class="chat-empty">
          <a-empty description="选择或新建一个对话开始聊天" />
        </div>
        <template v-else>
          <a-spin v-if="loadingMessages" />
          <ChatMessage
            v-for="msg in chatMessages"
            :key="msg.id"
            :message="msg"
            :user-initial="userInitial"
          />
          <div v-if="chatError" class="chat-error">
            <a-alert type="error" :message="chatError" show-icon />
          </div>
        </template>
      </div>

      <div class="chat-input-area">
        <div class="rag-toggle">
          <a-checkbox v-model:checked="enableRag">启用知识库 (RAG)</a-checkbox>
        </div>
        <div class="input-row">
          <a-textarea
            v-model:value="inputText"
            :rows="3"
            :placeholder="isStreaming ? '正在回复中…' : '输入消息，Ctrl+Enter 发送'"
            :disabled="!currentConvId || isStreaming"
            @keydown="handleKeyDown"
          />
          <div class="input-actions">
            <a-button
              v-if="isStreaming"
              type="default"
              danger
              @click="handleCancel"
            >
              停止
            </a-button>
            <a-button
              v-else
              type="primary"
              :disabled="!currentConvId || !inputText.trim()"
              @click="handleSend"
            >
              发送
            </a-button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import ChatMessage from "@/components/ai/ChatMessage.vue";
import { useStreamChat } from "@/composables/useStreamChat";
import {
  getConversationsPaged,
  createConversation,
  deleteConversation,
  clearConversationContext,
  clearConversationHistory,
  getMessages,
  type ConversationDto
} from "@/services/api-conversation";
import { getAgentById } from "@/services/api-agent";

const route = useRoute();
const agentId = computed(() => Number(route.params["agentId"]));

const agentName = ref("Agent 对话");
const conversations = ref<ConversationDto[]>([]);
const currentConvId = ref<number | null>(null);
const loadingConversations = ref(false);
const loadingMessages = ref(false);
const inputText = ref("");
const enableRag = ref(false);
const messagesContainer = ref<HTMLElement | null>(null);
const userInitial = ref("U");

const chatStore = useStreamChat({
  agentId: agentId.value,
  enableRag: enableRag.value
});

const isStreaming = computed(() => chatStore.isStreaming.value);
const chatMessages = computed(() => chatStore.messages.value);
const chatError = computed(() => chatStore.error.value);

function formatDate(iso: string) {
  try {
    const d = new Date(iso);
    const now = new Date();
    if (d.toDateString() === now.toDateString()) {
      return d.toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit" });
    }
    return d.toLocaleDateString("zh-CN", { month: "2-digit", day: "2-digit" });
  } catch {
    return "";
  }
}

async function loadConversations() {
  loadingConversations.value = true;
  try {
    const result = await getConversationsPaged(
      { pageIndex: 1, pageSize: 50 },
      agentId.value
    );
    conversations.value = result.items.sort(
      (a, b) => new Date(b.lastMessageAt || b.createdAt).getTime() -
                 new Date(a.lastMessageAt || a.createdAt).getTime()
    );
  } catch (err: unknown) {
    message.error((err as Error).message || "加载对话列表失败");
  } finally {
    loadingConversations.value = false;
  }
}

async function selectConversation(conv: ConversationDto) {
  if (currentConvId.value === conv.id) return;
  currentConvId.value = conv.id;
  chatStore.currentConversationId.value = conv.id;
  await loadMessages(conv.id);
}

async function loadMessages(convId: number) {
  loadingMessages.value = true;
  try {
    const msgs = await getMessages(convId, { limit: 50 });
    chatStore.loadHistory(msgs);
    await scrollToBottom();
  } catch (err: unknown) {
    message.error((err as Error).message || "加载消息失败");
  } finally {
    loadingMessages.value = false;
  }
}

async function handleNewConversation() {
  try {
    const id = await createConversation(agentId.value, "新对话");
    await loadConversations();
    const conv = conversations.value.find((c) => c.id === id);
    if (conv) {
      await selectConversation(conv);
    } else {
      currentConvId.value = id;
      chatStore.clearMessages();
    }
  } catch (err: unknown) {
    message.error((err as Error).message || "创建对话失败");
  }
}

async function handleDeleteConversation(id: number) {
  try {
    await deleteConversation(id);
    if (currentConvId.value === id) {
      currentConvId.value = null;
      chatStore.clearMessages();
    }
    await loadConversations();
    message.success("删除成功");
  } catch (err: unknown) {
    message.error((err as Error).message || "删除失败");
  }
}

async function handleClearContext() {
  if (!currentConvId.value) return;
  try {
    await clearConversationContext(currentConvId.value);
    message.success("上下文已清除");
  } catch (err: unknown) {
    message.error((err as Error).message || "操作失败");
  }
}

async function handleClearHistory() {
  if (!currentConvId.value) return;
  try {
    await clearConversationHistory(currentConvId.value);
    chatStore.clearMessages();
    message.success("历史已清除");
  } catch (err: unknown) {
    message.error((err as Error).message || "操作失败");
  }
}

async function handleSend() {
  if (!inputText.value.trim() || !currentConvId.value || isStreaming.value) return;
  const text = inputText.value;
  inputText.value = "";
  await chatStore.sendMessage(text);
  await scrollToBottom();
  await loadConversations();
}

function handleCancel() {
  chatStore.cancelStream();
}

function handleKeyDown(e: KeyboardEvent) {
  if (e.key === "Enter" && e.ctrlKey) {
    e.preventDefault();
    void handleSend();
  }
}

async function scrollToBottom() {
  await nextTick();
  if (messagesContainer.value) {
    messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight;
  }
}

watch(
  () => chatStore.messages.value.length,
  async () => {
    await scrollToBottom();
  }
);

onMounted(async () => {
  try {
    const agent = await getAgentById(agentId.value);
    agentName.value = agent.name;
  } catch {
    // ignore
  }
  await loadConversations();
  if (conversations.value.length > 0) {
    await selectConversation(conversations.value[0]);
  }
});
</script>

<style scoped>
.agent-chat-layout {
  display: flex;
  height: calc(100vh - 120px);
  gap: 0;
  background: #fff;
  border-radius: 8px;
  overflow: hidden;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
}

.chat-sidebar {
  width: 240px;
  min-width: 200px;
  border-right: 1px solid #f0f0f0;
  display: flex;
  flex-direction: column;
  background: #fafafa;
}

.sidebar-header {
  padding: 12px 12px 8px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid #f0f0f0;
}

.sidebar-title {
  font-weight: 600;
  font-size: 14px;
}

.conversation-list {
  flex: 1;
  overflow-y: auto;
  padding: 4px 0;
}

.conversation-item {
  padding: 8px 12px;
  cursor: pointer;
  border-radius: 4px;
  margin: 2px 6px;
  position: relative;
  transition: background 0.15s;
}

.conversation-item:hover {
  background: #e6f4ff;
}

.conversation-item.active {
  background: #e6f4ff;
  color: #1677ff;
}

.conv-title {
  font-size: 13px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  padding-right: 20px;
}

.conv-meta {
  font-size: 11px;
  color: rgba(0, 0, 0, 0.4);
  margin-top: 2px;
}

.conv-delete-btn {
  position: absolute;
  right: 4px;
  top: 50%;
  transform: translateY(-50%);
  opacity: 0;
  transition: opacity 0.15s;
}

.conversation-item:hover .conv-delete-btn {
  opacity: 1;
}

.empty-conv {
  padding: 20px 12px;
  color: rgba(0, 0, 0, 0.4);
  font-size: 12px;
  text-align: center;
}

.chat-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.chat-header {
  padding: 12px 16px;
  border-bottom: 1px solid #f0f0f0;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.agent-name {
  font-size: 15px;
  font-weight: 600;
}

.chat-messages {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: flex;
  flex-direction: column;
}

.chat-empty {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

.chat-error {
  margin-top: 8px;
}

.chat-input-area {
  border-top: 1px solid #f0f0f0;
  padding: 12px 16px;
  background: #fff;
}

.rag-toggle {
  margin-bottom: 8px;
}

.input-row {
  display: flex;
  gap: 8px;
  align-items: flex-end;
}

.input-row :deep(.ant-input) {
  flex: 1;
  resize: none;
}

.input-actions {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding-bottom: 2px;
}
</style>

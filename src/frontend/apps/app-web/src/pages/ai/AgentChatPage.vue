<template>
  <div v-if="!agentId" class="agent-chat-missing">
    <a-card class="agent-selector-card" :bordered="false">
      <a-space direction="vertical" size="middle" class="agent-selector-body">
        <div class="agent-selector-header">
          <h3 class="agent-selector-title">{{ t("ai.chat.selectAgentTitle") }}</h3>
          <p class="agent-selector-desc">{{ t("ai.chat.selectAgentDesc") }}</p>
        </div>

        <a-select
          v-model:value="selectedAgentId"
          show-search
          :loading="loadingAgents"
          :placeholder="t('ai.chat.selectAgentPlaceholder')"
          :options="availableAgents.map((item) => ({ value: item.id, label: item.name }))"
          :filter-option="false"
          @focus="availableAgents.length === 0 ? loadAgents() : undefined"
        />

        <a-button type="primary" :loading="loadingAgents" @click="enterChatWithSelectedAgent">
          {{ t("ai.chat.enterChat") }}
        </a-button>

        <a-empty
          v-if="!loadingAgents && availableAgents.length === 0"
          :description="t('ai.chat.noAgents')"
        />
      </a-space>
    </a-card>
  </div>
  <div v-else class="immersive-chat">
    <div class="chat-bg-gradient" />

    <div class="chat-topbar">
      <div class="topbar-left">
        <span class="topbar-agent-name">{{ agentName }}</span>
      </div>
      <div class="topbar-right">
        <a-button size="small" type="text" class="conv-drawer-trigger" @click="convDrawerVisible = true">
          <template #icon><HistoryOutlined /></template>
          {{ t("ai.chat.convList") }}
        </a-button>
        <a-button size="small" type="text" @click="handleNewConversation">
          <template #icon><PlusOutlined /></template>
          {{ t("ai.chat.newConv") }}
        </a-button>
        <a-dropdown v-if="currentConvId" :trigger="['click']">
          <a-button size="small" type="text">
            <template #icon><MoreOutlined /></template>
          </a-button>
          <template #overlay>
            <a-menu>
              <a-menu-item key="clear-ctx" @click="handleClearContext">
                {{ t("ai.chat.clearContext") }}
              </a-menu-item>
              <a-menu-item key="clear-hist" danger @click="handleClearHistory">
                {{ t("ai.chat.clearHistory") }}
              </a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
      </div>
    </div>

    <a-drawer
      v-model:open="convDrawerVisible"
      :title="t('ai.chat.convList')"
      placement="right"
      :width="320"
      :body-style="{ padding: '0' }"
    >
      <template #extra>
        <a-button type="primary" size="small" @click="handleNewConversation">
          {{ t("ai.chat.newConv") }}
        </a-button>
      </template>
      <a-spin :spinning="loadingConversations">
        <div class="conv-drawer-list">
          <div
            v-for="conv in conversations"
            :key="conv.id"
            :class="['conv-drawer-item', { 'conv-drawer-item--active': currentConvId === conv.id }]"
            @click="onDrawerConvClick(conv)"
          >
            <div class="conv-drawer-item-body">
              <div class="conv-drawer-item-title">{{ conv.title || t("ai.chat.newConvTitle") }}</div>
              <div class="conv-drawer-item-meta">
                <span>{{ conv.messageCount }} {{ t("ai.chat.messagesUnit") }}</span>
                <span>{{ formatDate(conv.lastMessageAt || conv.createdAt) }}</span>
              </div>
            </div>
            <a-popconfirm
              :title="t('ai.chat.deleteConvConfirm')"
              placement="left"
              @confirm.stop="handleDeleteConversation(conv.id)"
            >
              <a-button
                class="conv-drawer-item-delete"
                type="text"
                size="small"
                danger
                @click.stop
              >
                <template #icon><DeleteOutlined /></template>
              </a-button>
            </a-popconfirm>
          </div>
          <div v-if="conversations.length === 0 && !loadingConversations" class="conv-drawer-empty">
            {{ t("ai.chat.emptyConv") }}
          </div>
        </div>
      </a-spin>
    </a-drawer>

    <div ref="messagesContainer" class="chat-messages-area">
      <div class="chat-messages-center">
        <div v-if="!currentConvId && !loadingConversations" class="chat-welcome">
          <div class="welcome-icon">
            <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
              <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
            </svg>
          </div>
          <h2 class="welcome-title">{{ agentName || t("ai.chat.defaultAgentName") }}</h2>
          <p class="welcome-desc">{{ t("ai.chat.emptySelect") }}</p>
        </div>

        <template v-if="currentConvId">
          <a-spin v-if="loadingMessages" class="messages-spinner" />
          <ChatMessage
            v-for="(msg, index) in chatMessages"
            :key="msg.id"
            :message="msg"
            :react-steps="index === chatMessages.length - 1 && msg.role === 'assistant' ? reactSteps : undefined"
          />
          <div v-if="chatError" class="chat-error">
            <a-alert type="error" :message="chatError" show-icon />
          </div>
        </template>
      </div>
    </div>

    <div class="chat-input-wrapper">
      <div class="chat-input-fade" />
      <div class="chat-input-card">
        <div v-if="pendingAttachments.length > 0" class="attachment-strip">
          <a-tag
            v-for="(attachment, index) in pendingAttachments"
            :key="`${attachment.type}-${index}`"
            closable
            @close.prevent="removeAttachment(index)"
          >
            {{ attachment.type }}{{ attachment.name ? `: ${attachment.name}` : "" }}
          </a-tag>
        </div>
        <audio
          v-if="audioRecorder.audioUrl.value"
          :src="audioRecorder.audioUrl.value"
          class="audio-preview"
          controls
        />
        <div v-if="audioRecorder.error.value" class="recorder-error">
          <a-alert type="warning" show-icon :message="audioRecorder.error.value" />
        </div>

        <div class="input-main">
          <a-textarea
            v-model:value="inputText"
            :auto-size="{ minRows: 1, maxRows: 6 }"
            :placeholder="isStreaming ? t('ai.chat.placeholderStreaming') : t('ai.chat.immersivePlaceholder')"
            :disabled="!currentConvId || isStreaming"
            :bordered="false"
            class="chat-textarea"
            @keydown="handleKeyDown"
          />
        </div>

        <div class="input-bottom-bar">
          <div class="input-bottom-left">
            <input
              ref="imageInputRef"
              type="file"
              accept="image/*"
              style="display: none"
              @change="handleImageSelected"
            />
            <a-tooltip :title="t('ai.chat.attachImage')">
              <a-button type="text" size="small" :disabled="isStreaming" @click="triggerImageUpload">
                <template #icon><PaperClipOutlined /></template>
              </a-button>
            </a-tooltip>
            <div class="bar-divider" />
            <a-button
              :class="['feature-btn', { 'feature-btn--active': deepThinkEnabled }]"
              size="small"
              @click="deepThinkEnabled = !deepThinkEnabled"
            >
              {{ t("ai.chat.deepThink") }}
            </a-button>
            <a-button
              :class="['feature-btn', { 'feature-btn--active': webSearchEnabled }]"
              size="small"
              @click="webSearchEnabled = !webSearchEnabled"
            >
              {{ t("ai.chat.webSearch") }}
            </a-button>
          </div>
          <div class="input-bottom-right">
            <a-button
              v-if="isStreaming"
              shape="circle"
              size="small"
              danger
              class="send-btn"
              @click="handleCancel"
            >
              <template #icon><StopOutlined /></template>
            </a-button>
            <a-button
              v-else
              shape="circle"
              size="small"
              class="send-btn"
              :class="{ 'send-btn--ready': canSend }"
              :disabled="!canSend"
              @click="handleSend"
            >
              <template #icon><SendOutlined /></template>
            </a-button>
          </div>
        </div>
      </div>
      <div class="chat-disclaimer">
        {{ t("ai.chat.disclaimer") }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted, watch, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";
import {
  PaperClipOutlined,
  SendOutlined,
  StopOutlined,
  PlusOutlined,
  HistoryOutlined,
  MoreOutlined,
  DeleteOutlined
} from "@ant-design/icons-vue";

const { t, locale } = useI18n();

const isMounted = ref(false);
onUnmounted(() => {
  isMounted.value = false;
  clearAttachments();
});

import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import ChatMessage from "@/components/ai/ChatMessage.vue";
import { useStreamChat } from "@/composables/useStreamChat";
import { useAudioRecorder } from "@/composables/useAudioRecorder";
import {
  getConversationsPaged,
  createConversation,
  deleteConversation,
  clearConversationContext,
  clearConversationHistory,
  getMessages,
  type AgentChatAttachment,
  type ConversationDto
} from "@/services/api-conversation";
import { getAgentById, getAgentsPaged, type AgentListItem } from "@/services/api-agent";

const route = useRoute();
const router = useRouter();
const appKey = computed(() => String(route.params.appKey ?? ""));
const agentId = computed(() => String(route.params["agentId"] ?? ""));

const agentName = ref("");
const availableAgents = ref<AgentListItem[]>([]);
const loadingAgents = ref(false);
const selectedAgentId = ref("");
const conversations = ref<ConversationDto[]>([]);
const currentConvId = ref<string | null>(null);
const loadingConversations = ref(false);
const loadingMessages = ref(false);
const inputText = ref("");
const enableRag = ref(false);
const deepThinkEnabled = ref(true);
const webSearchEnabled = ref(false);
const convDrawerVisible = ref(false);
const messagesContainer = ref<HTMLElement | null>(null);
const imageInputRef = ref<HTMLInputElement | null>(null);
const pendingAttachments = ref<AgentChatAttachment[]>([]);
const audioRecorder = useAudioRecorder();

const chatStore = useStreamChat({
  appKey: () => appKey.value,
  agentId: () => agentId.value,
  enableRag: () => enableRag.value
});

const isStreaming = computed(() => chatStore.isStreaming.value);
const chatMessages = computed(() => chatStore.messages.value);
const chatError = computed(() => chatStore.error.value);
const reactSteps = computed(() => chatStore.reactSteps.value);

const canSend = computed(
  () => currentConvId.value && (inputText.value.trim() || pendingAttachments.value.length > 0) && !isStreaming.value
);

async function loadAgents() {
  loadingAgents.value = true;
  try {
    const result = await getAgentsPaged({ pageIndex: 1, pageSize: 20 });
    if (!isMounted.value) return;
    availableAgents.value = result.items;
    if (!selectedAgentId.value && result.items.length > 0) {
      selectedAgentId.value = result.items[0].id;
    }
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.chat.loadAgentsFailed"));
  } finally {
    loadingAgents.value = false;
  }
}

async function enterChatWithSelectedAgent() {
  if (!selectedAgentId.value) {
    message.warning(t("ai.chat.selectAgentRequired"));
    return;
  }
  await router.push({
    name: "app-ai-chat",
    params: {
      appKey: appKey.value,
      agentId: selectedAgentId.value
    }
  });
}

function formatDate(iso: string) {
  try {
    const d = new Date(iso);
    const now = new Date();
    const loc = locale.value === "en-US" ? "en-US" : "zh-CN";
    if (d.toDateString() === now.toDateString()) {
      return d.toLocaleTimeString(loc, { hour: "2-digit", minute: "2-digit" });
    }
    return d.toLocaleDateString(loc, { month: "2-digit", day: "2-digit" });
  } catch {
    return "";
  }
}

async function loadConversations() {
  loadingConversations.value = true;
  try {
    const result = await getConversationsPaged(
      appKey.value,
      { pageIndex: 1, pageSize: 50 },
      agentId.value
    );

    if (!isMounted.value) return;
    conversations.value = result.items.sort(
      (a, b) => new Date(b.lastMessageAt || b.createdAt).getTime() -
                 new Date(a.lastMessageAt || a.createdAt).getTime()
    );
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.chat.loadConvFailed"));
  } finally {
    loadingConversations.value = false;
  }
}

async function selectConversation(conv: ConversationDto) {
  if (currentConvId.value === conv.id) return;
  currentConvId.value = conv.id;
  chatStore.currentConversationId.value = conv.id;
  await loadMessages(conv.id);
  if (!isMounted.value) return;
}

async function loadMessages(convId: string) {
  loadingMessages.value = true;
  try {
    const msgs = await getMessages(appKey.value, convId, { limit: 50 });
    if (!isMounted.value) return;
    chatStore.loadHistory(msgs);
    await scrollToBottom();
    if (!isMounted.value) return;
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.chat.loadMsgFailed"));
  } finally {
    loadingMessages.value = false;
  }
}

function onDrawerConvClick(conv: ConversationDto) {
  convDrawerVisible.value = false;
  void selectConversation(conv);
}

async function handleNewConversation() {
  try {
    const id = await createConversation(appKey.value, agentId.value, t("ai.chat.newConversationTitle"));
    if (!isMounted.value) return;
    await loadConversations();
    if (!isMounted.value) return;
    const conv = conversations.value.find((c) => c.id === id);
    if (conv) {
      await selectConversation(conv);
      if (!isMounted.value) return;
    } else {
      currentConvId.value = id;
      chatStore.currentConversationId.value = id;
      chatStore.clearMessages();
    }
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.chat.createConvFailed"));
  }
}

async function handleDeleteConversation(id: string) {
  try {
    await deleteConversation(appKey.value, id);
    if (!isMounted.value) return;
    if (currentConvId.value === id) {
      currentConvId.value = null;
      chatStore.clearMessages();
    }
    await loadConversations();
    if (!isMounted.value) return;
    message.success(t("crud.deleteSuccess"));
  } catch (err: unknown) {
    message.error((err as Error).message || t("crud.deleteFailed"));
  }
}

async function handleClearContext() {
  if (!currentConvId.value) return;
  try {
    await clearConversationContext(appKey.value, currentConvId.value);
    if (!isMounted.value) return;
    message.success(t("ai.chat.clearContextOk"));
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.chat.opFailed"));
  }
}

async function handleClearHistory() {
  if (!currentConvId.value) return;
  try {
    await clearConversationHistory(appKey.value, currentConvId.value);
    if (!isMounted.value) return;
    chatStore.clearMessages();
    message.success(t("ai.chat.clearHistoryOk"));
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.chat.opFailed"));
  }
}

async function handleSend() {
  if ((!inputText.value.trim() && pendingAttachments.value.length === 0) || !currentConvId.value || isStreaming.value) return;
  const text = inputText.value;
  const attachments = pendingAttachments.value.map((item) => ({ ...item }));
  inputText.value = "";
  pendingAttachments.value = [];
  audioRecorder.clear();
  await chatStore.sendMessage(text, attachments);
  if (!isMounted.value) return;
  await scrollToBottom();
  if (!isMounted.value) return;
  await loadConversations();
  if (!isMounted.value) return;
}

function handleCancel() {
  chatStore.cancelStream();
}

function triggerImageUpload() {
  imageInputRef.value?.click();
}

function handleImageSelected(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) return;
  const url = URL.createObjectURL(file);
  pendingAttachments.value.push({
    type: "image",
    url,
    mimeType: file.type || "image/*",
    name: file.name
  });
  input.value = "";
}

function removeAttachment(index: number) {
  const [removed] = pendingAttachments.value.splice(index, 1);
  if (removed?.url?.startsWith("blob:")) {
    URL.revokeObjectURL(removed.url);
  }
}

function clearAttachments() {
  pendingAttachments.value.forEach((item) => {
    if (item.url?.startsWith("blob:")) {
      URL.revokeObjectURL(item.url);
    }
  });
  pendingAttachments.value = [];
  audioRecorder.clear();
}

function handleKeyDown(e: KeyboardEvent) {
  if (e.key === "Enter" && !e.shiftKey) {
    e.preventDefault();
    void handleSend();
  }
}

async function scrollToBottom() {
  await nextTick();
  if (!isMounted.value) return;
  if (messagesContainer.value) {
    messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight;
  }
}

function resetAgentChatState() {
  agentName.value = "";
  conversations.value = [];
  currentConvId.value = null;
  chatStore.currentConversationId.value = null;
  chatStore.clearMessages();
  inputText.value = "";
  clearAttachments();
}

async function initializeAgentChatPage() {
  if (!agentId.value) {
    resetAgentChatState();
    await loadAgents();
    return;
  }

  const [agentResult] = await Promise.allSettled([
    getAgentById(agentId.value),
    loadConversations()
  ]);

  if (!isMounted.value) return;
  if (agentResult.status === "fulfilled") {
    agentName.value = agentResult.value.name;
  } else {
    agentName.value = t("ai.chat.defaultAgentName");
  }
  if (conversations.value.length > 0) {
    await selectConversation(conversations.value[0]);
    if (!isMounted.value) return;
  }
}

watch(
  () => chatStore.messages.value.length,
  async () => {
    await scrollToBottom();
    if (!isMounted.value) return;
  }
);

watch(agentId, async (newValue, oldValue) => {
  if (!isMounted.value || newValue === oldValue) return;
  await initializeAgentChatPage();
});

onMounted(async () => {
  isMounted.value = true;
  await initializeAgentChatPage();
});
</script>

<style scoped>
.immersive-chat {
  position: relative;
  display: flex;
  flex-direction: column;
  height: calc(100vh - 64px);
  background: #fbfcfd;
  overflow: hidden;
}

.chat-bg-gradient {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 260px;
  background: linear-gradient(180deg, #eef2ff 0%, rgba(238, 242, 255, 0) 100%);
  pointer-events: none;
  z-index: 0;
}

.chat-topbar {
  position: relative;
  z-index: 10;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 24px;
  flex-shrink: 0;
}

.topbar-agent-name {
  font-size: 15px;
  font-weight: 600;
  color: #1e2939;
}

.topbar-right {
  display: flex;
  align-items: center;
  gap: 4px;
}

.conv-drawer-trigger {
  color: #6a7282;
}

.conv-drawer-list {
  display: flex;
  flex-direction: column;
}

.conv-drawer-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  cursor: pointer;
  transition: background 0.15s;
  border-bottom: 1px solid #f3f4f6;
}

.conv-drawer-item:hover {
  background: #f9fafb;
}

.conv-drawer-item--active {
  background: #eef2ff;
}

.conv-drawer-item-body {
  flex: 1;
  min-width: 0;
}

.conv-drawer-item-title {
  font-size: 13px;
  font-weight: 500;
  color: #1e2939;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.conv-drawer-item-meta {
  display: flex;
  gap: 8px;
  font-size: 11px;
  color: #9ca3af;
  margin-top: 2px;
}

.conv-drawer-item-delete {
  opacity: 0;
  flex-shrink: 0;
  transition: opacity 0.15s;
}

.conv-drawer-item:hover .conv-drawer-item-delete {
  opacity: 1;
}

.conv-drawer-empty {
  padding: 40px 16px;
  color: #9ca3af;
  font-size: 13px;
  text-align: center;
}

.chat-messages-area {
  position: relative;
  z-index: 1;
  flex: 1;
  overflow-y: auto;
  padding: 0 24px 140px;
  display: flex;
  flex-direction: column;
  align-items: center;
}

.chat-messages-center {
  width: 100%;
  max-width: 768px;
  display: flex;
  flex-direction: column;
}

.chat-welcome {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 80px 0 40px;
  text-align: center;
}

.welcome-icon {
  width: 56px;
  height: 56px;
  border-radius: 16px;
  background: #4f39f6;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 16px;
}

.welcome-title {
  font-size: 20px;
  font-weight: 600;
  color: #1e2939;
  margin: 0 0 8px;
}

.welcome-desc {
  font-size: 14px;
  color: #6a7282;
  margin: 0;
}

.messages-spinner {
  display: flex;
  justify-content: center;
  padding: 24px 0;
}

.chat-error {
  margin-top: 8px;
  width: 100%;
}

.chat-input-wrapper {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  z-index: 20;
  display: flex;
  flex-direction: column;
  align-items: center;
  pointer-events: none;
}

.chat-input-fade {
  width: 100%;
  height: 40px;
  background: linear-gradient(180deg, transparent, #fbfcfd);
}

.chat-input-card {
  width: 100%;
  max-width: 768px;
  background: #fff;
  border-radius: 24px;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.08);
  border: 1px solid #e5e7eb;
  padding: 12px 16px;
  pointer-events: auto;
  display: flex;
  flex-direction: column;
  margin: 0 24px;
}

.input-main {
  display: flex;
  align-items: flex-end;
}

.chat-textarea {
  flex: 1;
  padding: 0;
  resize: none;
  font-size: 14px;
  line-height: 1.6;
  box-shadow: none !important;
}

.chat-textarea:focus {
  box-shadow: none !important;
}

.input-bottom-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 8px;
}

.input-bottom-left {
  display: flex;
  align-items: center;
  gap: 6px;
}

.bar-divider {
  width: 1px;
  height: 16px;
  background: #e5e7eb;
}

.feature-btn {
  border-radius: 16px;
  font-size: 12px;
  padding: 2px 12px;
  height: 28px;
  border: 1px solid #e5e7eb;
  color: #6a7282;
  background: transparent;
}

.feature-btn--active {
  background: #eef2ff;
  color: #4f39f6;
  border-color: #c7d2fe;
}

.input-bottom-right {
  display: flex;
  align-items: center;
}

.send-btn {
  width: 32px;
  height: 32px;
  background: #d1d5db;
  border: none;
  color: #fff;
}

.send-btn--ready {
  background: #4f39f6;
}

.send-btn--ready:hover {
  background: #432dd7 !important;
}

.chat-disclaimer {
  text-align: center;
  font-size: 12px;
  color: #9ca3af;
  padding: 8px 0 12px;
  pointer-events: auto;
  background: #fbfcfd;
  width: 100%;
}

.attachment-strip {
  margin-bottom: 8px;
}

.audio-preview {
  width: 100%;
  margin-bottom: 8px;
  height: 32px;
}

.recorder-error {
  margin-bottom: 8px;
}

.agent-chat-missing {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: calc(100vh - 64px);
  padding: 24px;
}

.agent-selector-card {
  width: 100%;
  max-width: 560px;
  border-radius: 16px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.08);
}

.agent-selector-body {
  width: 100%;
}

.agent-selector-header {
  margin-bottom: 8px;
}

.agent-selector-title {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
  color: #111827;
}

.agent-selector-desc {
  margin: 8px 0 0;
  color: #6b7280;
  font-size: 14px;
}
</style>

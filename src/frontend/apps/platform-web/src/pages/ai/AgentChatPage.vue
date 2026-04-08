<template>
  <div class="agent-chat-layout">
    <div class="chat-sidebar">
      <div class="sidebar-header">
        <span class="sidebar-title">{{ t("ai.chat.convList") }}</span>
        <a-button type="primary" size="small" @click="handleNewConversation">
          {{ t("ai.chat.newConv") }}
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
            <div class="conv-title">{{ conv.title || t("ai.chat.newConvTitle") }}</div>
            <div class="conv-meta">{{ formatDate(conv.lastMessageAt || conv.createdAt) }}</div>
            <a-popconfirm
              :title="t('ai.chat.deleteConvConfirm')"
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
            {{ t("ai.chat.emptyConv") }}
          </div>
        </div>
      </a-spin>
    </div>

    <div class="chat-main">
      <div class="chat-header">
        <span class="agent-name">{{ agentName }}</span>
        <a-space v-if="currentConvId">
          <a-tooltip :title="t('ai.chat.clearContextTip')">
            <a-button size="small" @click="handleClearContext">{{ t("ai.chat.clearContext") }}</a-button>
          </a-tooltip>
          <a-tooltip :title="t('ai.chat.clearHistoryTip')">
            <a-button size="small" danger @click="handleClearHistory">{{ t("ai.chat.clearHistory") }}</a-button>
          </a-tooltip>
        </a-space>
      </div>

      <div ref="messagesContainer" class="chat-messages" @scroll="handleMessagesScroll">
        <div class="chat-messages-inner">
          <div v-if="!currentConvId" class="chat-empty">
            <a-empty :description="t('ai.chat.emptySelect')" />
          </div>
          <template v-else>
            <a-spin v-if="loadingMessages" />
            <ChatMessage
              v-for="(msg, index) in chatMessages"
              :key="msg.id"
              :message="msg"
              :react-steps="msg.reactSteps"
              :show-typing-cursor="index === chatMessages.length - 1 && msg.role === 'assistant' && isStreaming"
            />
            <div v-if="chatError" class="chat-error">
              <a-alert type="error" :message="chatError" show-icon />
            </div>
          </template>
        </div>
      </div>

      <div class="chat-input-area">
        <div class="chat-input-container">
          <div v-if="pendingAttachments.length > 0" class="attachment-list">
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

          <div class="input-row">
            <a-textarea
              v-model:value="inputText"
              :auto-size="{ minRows: 1, maxRows: 6 }"
              :placeholder="isStreaming ? t('ai.chat.placeholderStreaming') : t('ai.chat.placeholderInput')"
              :disabled="!currentConvId || isStreaming"
              :bordered="false"
              class="chat-textarea"
              @keydown="handleKeyDown"
            />
            <div class="input-actions">
              <a-button
                v-if="isStreaming"
                type="primary"
                danger
                shape="circle"
                @click="handleCancel"
              >
                <template #icon><StopOutlined /></template>
              </a-button>
              <a-button
                v-else
                type="primary"
                shape="circle"
                :disabled="!currentConvId || (!inputText.trim() && pendingAttachments.length === 0)"
                @click="handleSend"
              >
                <template #icon><SendOutlined /></template>
              </a-button>
            </div>
          </div>

          <div class="toolbar-row">
            <a-space :size="4">
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
              <a-tooltip :title="audioRecorder.isRecording.value ? t('ai.chat.stopRecord') : t('ai.chat.startRecord')">
                <a-button
                  type="text"
                  size="small"
                  :disabled="isStreaming || !audioRecorder.isSupported.value"
                  :danger="audioRecorder.isRecording.value"
                  @click="handleToggleRecord"
                >
                  <template #icon><AudioOutlined /></template>
                </a-button>
              </a-tooltip>
              <a-tooltip v-if="pendingAttachments.length > 0" :title="t('ai.chat.clearAttachments')">
                <a-button
                  type="text"
                  size="small"
                  :disabled="isStreaming"
                  @click="clearAttachments"
                >
                  <template #icon><DeleteOutlined /></template>
                </a-button>
              </a-tooltip>
            </a-space>
            <div class="rag-toggle">
              <a-checkbox v-model:checked="enableRag">{{ t("ai.chat.enableRag") }}</a-checkbox>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted, watch, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import { PaperClipOutlined, AudioOutlined, SendOutlined, StopOutlined, DeleteOutlined } from "@ant-design/icons-vue";
import ChatMessage from "@/components/ai/ChatMessage.vue";
import { useStreamChat } from "@/composables/useStreamChat";
import { useAudioRecorder } from "@/composables/useAudioRecorder";
import {
  getAgentById,
  getConversationsPaged,
  createConversation,
  deleteConversation,
  clearConversationContext,
  clearConversationHistory,
  getMessages,
  type AgentChatAttachment,
  type ConversationDto
} from "@/services/api-ai";

const { t, locale } = useI18n();
const route = useRoute();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => {
  isMounted.value = false;
  clearAttachments();
});

/** 路由中的雪花 ID 必须用字符串，Number() 会丢失超过 MAX_SAFE_INTEGER 的精度 */
const agentId = computed(() => String(route.params["agentId"] ?? ""));

const agentName = ref("");
const conversations = ref<ConversationDto[]>([]);
const currentConvId = ref<string | null>(null);
const loadingConversations = ref(false);
const loadingMessages = ref(false);
const inputText = ref("");
const enableRag = ref(false);
const messagesContainer = ref<HTMLElement | null>(null);
const shouldAutoScroll = ref(true);
const imageInputRef = ref<HTMLInputElement | null>(null);
const pendingAttachments = ref<AgentChatAttachment[]>([]);
const audioRecorder = useAudioRecorder();

const chatStore = useStreamChat({
  agentId: () => agentId.value,
  enableRag: () => enableRag.value
});

const isStreaming = computed(() => chatStore.isStreaming.value);
const chatMessages = computed(() => chatStore.messages.value);
const chatError = computed(() => chatStore.error.value);

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
    const msgs = await getMessages(convId, { limit: 50 });

    if (!isMounted.value) return;
    chatStore.loadHistory(msgs);
    await scrollToBottom(true);

    if (!isMounted.value) return;
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.chat.loadMsgFailed"));
  } finally {
    loadingMessages.value = false;
  }
}

async function handleNewConversation() {
  try {
    const id = await createConversation(agentId.value, t("ai.chat.newConversationTitle"));

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
    await deleteConversation(id);

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
    await clearConversationContext(currentConvId.value);

    if (!isMounted.value) return;
    message.success(t("ai.chat.clearContextOk"));
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.chat.opFailed"));
  }
}

async function handleClearHistory() {
  if (!currentConvId.value) return;
  try {
    await clearConversationHistory(currentConvId.value);

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
  await scrollToBottom(true);

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
  if (!file) {
    return;
  }

  const url = URL.createObjectURL(file);
  pendingAttachments.value.push({
    type: "image",
    url,
    mimeType: file.type || "image/*",
    name: file.name
  });
  input.value = "";
}

async function handleToggleRecord() {
  if (!audioRecorder.isSupported.value) {
    message.warning(t("ai.chat.recordUnsupported"));
    return;
  }

  if (!audioRecorder.isRecording.value) {
    await audioRecorder.startRecording();
    return;
  }

  await audioRecorder.stopRecording();
  if (!audioRecorder.audioBlob.value) {
    return;
  }

  pendingAttachments.value.push({
    type: "audio",
    url: audioRecorder.audioUrl.value || undefined,
    mimeType: audioRecorder.audioBlob.value.type || "audio/webm",
    name: `record-${Date.now()}.webm`,
    text: t("ai.chat.recordAttachmentHint")
  });
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
  if (e.key === "Enter" && e.ctrlKey) {
    e.preventDefault();
    void handleSend();
  }
}

function handleMessagesScroll() {
  const container = messagesContainer.value;
  if (!container) {
    return;
  }
  shouldAutoScroll.value = isNearBottom(container);
}

function isNearBottom(container: HTMLElement) {
  return container.scrollHeight - container.scrollTop - container.clientHeight < 120;
}

async function scrollToBottom(force = false) {
  await nextTick();

  if (!isMounted.value) return;
  if (messagesContainer.value) {
    if (!force && !shouldAutoScroll.value) {
      return;
    }
    messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight;
    shouldAutoScroll.value = true;
  }
}

watch(
  () => chatStore.messages.value.length,
  async () => {
    await scrollToBottom();

    if (!isMounted.value) return;
  }
);

watch(
  () => [
    chatStore.reasoningText.value,
    chatStore.answerText.value,
    chatStore.streamPhase.value
  ],
  async () => {
    await scrollToBottom();

    if (!isMounted.value) return;
  }
);

onMounted(async () => {
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
  background: #f9f9f9;
  position: relative;
}

.chat-header {
  padding: 12px 16px;
  border-bottom: 1px solid #f0f0f0;
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: #fff;
  z-index: 10;
}

.agent-name {
  font-size: 15px;
  font-weight: 600;
}

.chat-messages {
  flex: 1;
  overflow-y: auto;
  padding: 24px 16px 120px;
  display: flex;
  flex-direction: column;
  align-items: center;
}

.chat-messages-inner {
  width: 100%;
  max-width: 800px;
  display: flex;
  flex-direction: column;
}

.chat-empty {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 300px;
}

.chat-error {
  margin-top: 8px;
  width: 100%;
}

.chat-input-area {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  padding: 0 16px 24px;
  background: linear-gradient(180deg, transparent, #f9f9f9 20%);
  display: flex;
  justify-content: center;
  pointer-events: none;
}

.chat-input-container {
  width: 100%;
  max-width: 800px;
  background: #fff;
  border-radius: 16px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.08);
  padding: 12px 16px;
  border: 1px solid #f0f0f0;
  pointer-events: auto;
  display: flex;
  flex-direction: column;
}

.input-row {
  display: flex;
  gap: 8px;
  align-items: flex-end;
  margin-bottom: 8px;
}

.chat-textarea {
  flex: 1;
  padding: 0;
  resize: none;
  font-size: 14px;
  line-height: 1.5;
  box-shadow: none !important;
}

.chat-textarea:focus {
  box-shadow: none !important;
}

.input-actions {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding-bottom: 2px;
}

.toolbar-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-top: 1px solid #f5f5f5;
  padding-top: 8px;
}

.rag-toggle {
  margin-bottom: 0;
}

.attachment-list {
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
</style>

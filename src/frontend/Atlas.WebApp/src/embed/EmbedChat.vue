<template>
  <div class="atlas-embed-chat">
    <div class="atlas-embed-header">{{ titleText }}</div>
    <div class="atlas-embed-messages">
      <div
        v-for="item in messages"
        :key="item.id"
        class="atlas-embed-message"
        :class="item.role === 'user' ? 'atlas-embed-message-user' : 'atlas-embed-message-assistant'"
      >
        <div class="atlas-embed-bubble">{{ item.content }}</div>
      </div>
    </div>
    <div class="atlas-embed-input">
      <a-textarea
        v-model:value="inputText"
        :rows="2"
        :maxlength="1000"
        :placeholder="placeholderText"
        @press-enter="onPressEnter"
      />
      <a-button type="primary" :loading="sending" @click="sendMessage">
        {{ sendText }}
      </a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";

interface EmbedMessage {
  id: string;
  role: "user" | "assistant";
  content: string;
}

const props = withDefaults(defineProps<{
  apiBaseUrl?: string;
  tenantId?: string;
  embedToken: string;
  externalUserId?: string;
  title?: string;
  placeholder?: string;
  sendText?: string;
}>(), {
  apiBaseUrl: `${window.location.origin}/api/v1`,
  tenantId: "",
  externalUserId: "",
  title: "Atlas Embed Chat",
  placeholder: "请输入消息…",
  sendText: "发送"
});

const inputText = ref("");
const sending = ref(false);
const conversationId = ref<number | undefined>();
const messages = ref<EmbedMessage[]>([]);

const titleText = computed(() => props.title);
const placeholderText = computed(() => props.placeholder);
const sendText = computed(() => props.sendText);

function onPressEnter(event: KeyboardEvent) {
  if (event.shiftKey) {
    return;
  }

  event.preventDefault();
  void sendMessage();
}

async function sendMessage() {
  const message = inputText.value.trim();
  if (!message || sending.value) {
    return;
  }

  messages.value.push({
    id: `user-${Date.now()}`,
    role: "user",
    content: message
  });
  inputText.value = "";
  sending.value = true;
  try {
    const headers: Record<string, string> = {
      "Content-Type": "application/json"
    };
    if (props.tenantId) {
      headers["X-Tenant-Id"] = props.tenantId;
    }

    const response = await fetch(`${props.apiBaseUrl}/embed-chat/chat`, {
      method: "POST",
      headers,
      body: JSON.stringify({
        embedToken: props.embedToken,
        externalUserId: props.externalUserId || undefined,
        message,
        conversationId: conversationId.value,
        enableRag: false
      })
    });
    const payload = await response.json() as {
      success: boolean;
      message?: string;
      data?: { conversationId?: number; content?: string };
    };
    if (!response.ok || !payload.success || !payload.data?.content) {
      throw new Error(payload.message || "Embed chat request failed.");
    }

    conversationId.value = payload.data.conversationId;
    messages.value.push({
      id: `assistant-${Date.now()}`,
      role: "assistant",
      content: payload.data.content
    });
  } catch (error: unknown) {
    messages.value.push({
      id: `assistant-error-${Date.now()}`,
      role: "assistant",
      content: (error as Error).message || "Embed chat request failed."
    });
  } finally {
    sending.value = false;
  }
}
</script>

<style scoped>
.atlas-embed-chat {
  width: 100%;
  max-width: 420px;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  overflow: hidden;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  background: #fff;
}

.atlas-embed-header {
  padding: 10px 12px;
  font-size: 14px;
  font-weight: 600;
  border-bottom: 1px solid #f0f0f0;
}

.atlas-embed-messages {
  min-height: 280px;
  max-height: 380px;
  overflow-y: auto;
  padding: 12px;
  background: #fafafa;
}

.atlas-embed-message {
  display: flex;
  margin-bottom: 10px;
}

.atlas-embed-message-user {
  justify-content: flex-end;
}

.atlas-embed-message-assistant {
  justify-content: flex-start;
}

.atlas-embed-bubble {
  max-width: 86%;
  font-size: 13px;
  line-height: 1.5;
  padding: 8px 10px;
  border-radius: 10px;
  background: #fff;
  border: 1px solid #f0f0f0;
  white-space: pre-wrap;
}

.atlas-embed-message-user .atlas-embed-bubble {
  background: #1677ff;
  border-color: #1677ff;
  color: #fff;
}

.atlas-embed-input {
  border-top: 1px solid #f0f0f0;
  padding: 10px;
  display: flex;
  gap: 8px;
  align-items: flex-end;
}

.atlas-embed-input :deep(.ant-input-textarea) {
  flex: 1;
}
</style>

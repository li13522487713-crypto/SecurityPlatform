<template>
  <div class="communication-panel">
    <div ref="msgListRef" class="msg-list">
      <div v-if="messages.length === 0" class="empty-msg">{{ t('approvalDesigner.commEmpty') }}</div>
      <div v-for="msg in messages" :key="msg.id" class="msg-item" :class="{ 'is-me': msg.senderUserId === currentUserId }">
        <div class="msg-avatar">
          <a-avatar>{{ msg.senderName?.[0] || 'U' }}</a-avatar>
        </div>
        <div class="msg-content">
          <div class="msg-info">
            <span class="msg-name">{{ msg.senderName || t('approvalDesigner.commUserFallback') }}</span>
            <span class="msg-time">{{ formatTime(msg.createdAt) }}</span>
          </div>
          <div class="msg-text">{{ msg.content }}</div>
        </div>
      </div>
    </div>
    <div class="msg-input">
      <a-textarea
        v-model:value="inputText"
        :placeholder="t('approvalDesigner.commPlaceholder')"
        :auto-size="{ minRows: 2, maxRows: 4 }"
        @press-enter.prevent="handleSend"
      />
      <div class="msg-actions">
        <UserRolePicker v-model:value="recipientIds" mode="user" :placeholder="t('approvalDesigner.commPickRecipients')" style="width: 200px" />
        <a-button type="primary" :loading="sending" @click="handleSend">{{ t('approvalDesigner.commSend') }}</a-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, nextTick, onUnmounted } from 'vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { message } from 'ant-design-vue';
import { getCommunications, communicateTask, type ApprovalCommunicationMessage } from '@/services/api';
import UserRolePicker from '@/components/common/UserRolePicker.vue';
import dayjs from 'dayjs';

const props = defineProps<{
  taskId: string;
  currentUserId: string;
}>();

const messages = ref<ApprovalCommunicationMessage[]>([]);
const inputText = ref('');
const recipientIds = ref<string[]>([]);
const sending = ref(false);
const msgListRef = ref<HTMLElement | null>(null);

const fetchMessages = async () => {
  try {
    const res  = await getCommunications(props.taskId);

    if (!isMounted.value) return;
    messages.value = [...res].sort((a, b) => dayjs(a.createdAt).valueOf() - dayjs(b.createdAt).valueOf());
    scrollToBottom();
  } catch (error) {
    console.error(error);
  }
};

const handleSend = async () => {
  if (!inputText.value.trim()) return;
  if (recipientIds.value.length === 0) {
    message.warning(t('approvalDesigner.commWarnRecipients'));
    return;
  }

  sending.value = true;
  try {
    const recipientId = recipientIds.value[0];
    await communicateTask(props.taskId, recipientId, inputText.value);

    if (!isMounted.value) return;
    inputText.value = '';
    recipientIds.value = [];
    message.success(t('approvalDesigner.commSendOk'));
    fetchMessages();
  } catch (error) {
    message.error(t('approvalDesigner.commSendFailed'));
  } finally {
    sending.value = false;
  }
};

const scrollToBottom = () => {
  nextTick(() => {
    if (msgListRef.value) {
      msgListRef.value.scrollTop = msgListRef.value.scrollHeight;
    }
  });
};

const formatTime = (time: string) => {
  return dayjs(time).format('MM-DD HH:mm');
};

onMounted(() => {
  fetchMessages();
});
</script>

<style scoped>
.communication-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  border: 1px solid #f0f0f0;
  border-radius: 4px;
}
.msg-list {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  background: #fafafa;
}
.empty-msg {
  text-align: center;
  color: #999;
  margin-top: 20px;
}
.msg-item {
  display: flex;
  margin-bottom: 16px;
}
.msg-item.is-me {
  flex-direction: row-reverse;
}
.msg-avatar {
  margin: 0 12px;
}
.msg-content {
  max-width: 70%;
}
.msg-info {
  font-size: 12px;
  color: #999;
  margin-bottom: 4px;
}
.is-me .msg-info {
  text-align: right;
}
.msg-text {
  background: #fff;
  padding: 8px 12px;
  border-radius: 4px;
  box-shadow: 0 1px 2px rgba(0,0,0,0.05);
  white-space: pre-wrap;
}
.is-me .msg-text {
  background: #e6f7ff;
}
.msg-input {
  padding: 16px;
  border-top: 1px solid #f0f0f0;
  background: #fff;
}
.msg-actions {
  display: flex;
  justify-content: space-between;
  margin-top: 8px;
}
</style>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";

interface Action {
  key: string;
  label: string;
  type?: 'primary' | 'default' | 'dashed' | 'text' | 'link';
  danger?: boolean;
  icon?: any;
  requireConfirm?: boolean;
  confirmTitle?: string;
  action: (selectedKeys: any[]) => Promise<void>;
}

interface Props {
  selectedKeys: any[];
  actions: Action[];
  loading?: boolean;
}

const props = defineProps<Props>();
const emit = defineEmits<{
  (e: "clear"): void;
}>();

const { t } = useI18n();

const hasSelection = computed(() => props.selectedKeys.length > 0);

const handleAction = async (act: Action) => {
  if (act.requireConfirm) {
    // Confirm is handled by a-popconfirm outside
    return;
  }
  await executeAction(act);
};

const executeAction = async (act: Action) => {
  try {
    await act.action(props.selectedKeys);
    message.success(`${act.label} 成功`);
    emit("clear");
  } catch (err: any) {
    message.error(`${act.label} 失败: ${err.message || '未知错误'}`);
  }
};

</script>

<template>
  <div class="batch-action-bar" :class="{ 'is-active': hasSelection }">
    <div class="selection-info">
      <span>已选择 <span class="highlight">{{ selectedKeys.length }}</span> 项</span>
      <a-button type="link" size="small" @click="emit('clear')">清空选择</a-button>
    </div>
    
    <div class="action-buttons">
      <template v-for="act in actions" :key="act.key">
        <a-popconfirm
          v-if="act.requireConfirm"
          :title="act.confirmTitle || `确认要执行【${act.label}】操作吗？`"
          @confirm="executeAction(act)"
        >
          <a-button
            :type="act.type"
            :danger="act.danger"
            :loading="loading"
            class="action-btn"
          >
            {{ act.label }}
          </a-button>
        </a-popconfirm>
        
        <a-button
          v-else
          :type="act.type"
          :danger="act.danger"
          :loading="loading"
          class="action-btn"
          @click="handleAction(act)"
        >
          {{ act.label }}
        </a-button>
      </template>
    </div>
  </div>
</template>

<style scoped>
.batch-action-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 16px;
  background-color: var(--ant-primary-1, #e6f7ff);
  border: 1px solid var(--ant-primary-3, #91d5ff);
  border-radius: 4px;
  margin-bottom: 16px;
  transition: all 0.3s;
  opacity: 0;
  visibility: hidden;
  height: 0;
  overflow: hidden;
}

.batch-action-bar.is-active {
  opacity: 1;
  visibility: visible;
  height: auto;
}

.selection-info {
  display: flex;
  align-items: center;
  gap: 8px;
}

.selection-info .highlight {
  color: var(--ant-primary-color, #1890ff);
  font-weight: 600;
  margin: 0 4px;
}

.action-buttons {
  display: flex;
  gap: 8px;
}

.action-btn {
  /* 使得批量操作更明显 */
}
</style>

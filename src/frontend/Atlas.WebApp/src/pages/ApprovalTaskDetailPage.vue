<template>
  <div class="task-detail-page">
    <div class="page-header">
      <a-page-header
        :title="`审批任务: ${task?.title || ''}`"
        @back="$router.back()"
      >
        <template #tags>
          <a-tag :color="getStatusColor(task?.status)">{{ getStatusText(task?.status) }}</a-tag>
        </template>
        <template #extra>
          <template v-if="task?.status === 0">
            <a-button type="primary" @click="showApproveModal">同意</a-button>
            <a-button danger @click="showRejectModal">驳回</a-button>
            <a-dropdown>
              <template #overlay>
                <a-menu @click="handleMenuClick">
                  <a-menu-item key="transfer">转办</a-menu-item>
                  <a-menu-item key="delegate">委派</a-menu-item>
                  <a-menu-item key="jump">跳转</a-menu-item>
                  <a-menu-item key="communicate">沟通</a-menu-item>
                </a-menu>
              </template>
              <a-button>更多操作 <DownOutlined /></a-button>
            </a-dropdown>
          </template>
        </template>
      </a-page-header>
    </div>

    <a-spin :spinning="loading">
      <div class="content-layout">
        <div class="main-content">
          <!-- 表单区域 (可编辑/只读) -->
          <a-card title="业务表单" class="mb-4">
            <div v-if="instance?.dataJson">
              <!-- 表单渲染器 -->
              <pre>{{ JSON.stringify(JSON.parse(instance.dataJson), null, 2) }}</pre>
            </div>
          </a-card>
        </div>

        <div class="side-content">
          <!-- 沟通面板 -->
          <a-card title="沟通记录" class="mb-4">
            <CommunicationPanel 
              v-if="task" 
              :task-id="task.id" 
              :current-user-id="currentUserId" 
              style="height: 400px"
            />
          </a-card>
        </div>
      </div>
    </a-spin>

    <!-- 审批弹窗 -->
    <a-modal
      v-model:visible="approveVisible"
      title="审批同意"
      @ok="handleApprove"
      :confirm-loading="submitting"
    >
      <a-textarea v-model:value="comment" placeholder="请输入审批意见" :rows="4" />
    </a-modal>

    <!-- 驳回弹窗 -->
    <a-modal
      v-model:visible="rejectVisible"
      title="审批驳回"
      @ok="handleReject"
      :confirm-loading="submitting"
    >
      <a-textarea v-model:value="comment" placeholder="请输入驳回原因" :rows="4" />
    </a-modal>

    <!-- 跳转选择器 -->
    <JumpNodeSelector 
      v-model:visible="jumpVisible" 
      :flow-definition="flowDefinition" 
      @select="handleJump" 
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { message } from 'ant-design-vue';
import { 
  getApprovalInstanceById, 
  decideApprovalTask,
  delegateTask,
  jumpTask,
  getApprovalFlowById
} from '@/services/api'; // 假设有 getTaskById (目前 api.ts 里好像没有直接获取单个任务的，通常从列表进)
// 实际上我们可能需要加一个 getTaskDetail API
import { DownOutlined } from '@ant-design/icons-vue';
import CommunicationPanel from '@/components/approval/runtime/CommunicationPanel.vue';
import JumpNodeSelector from '@/components/approval/runtime/JumpNodeSelector.vue';
import { getCurrentUser } from '@/services/api';

const route = useRoute();
const router = useRouter();
const taskId = route.params.id as string;
const instanceId = route.query.instanceId as string;

const loading = ref(false);
const submitting = ref(false);
const task = ref<any>(null); // 需要获取任务详情
const instance = ref<any>(null);
const currentUserId = ref('');
const flowDefinition = ref<any>(null);

const approveVisible = ref(false);
const rejectVisible = ref(false);
const jumpVisible = ref(false);
const comment = ref('');

const fetchDetail = async () => {
  loading.value = true;
  try {
    const user = await getCurrentUser();
    currentUserId.value = user.id;

    // 获取实例详情
    if (instanceId) {
      const res = await getApprovalInstanceById(instanceId);
      instance.value = res;
      
      // 获取流程定义用于跳转选择
      const flowRes = await getApprovalFlowById(res.definitionId);
      flowDefinition.value = JSON.parse(flowRes.definitionJson);
    }
    
    // 获取任务详情 (模拟，实际需要 API)
    // task.value = await getTaskDetail(taskId);
    // 临时 mock
    task.value = { id: taskId, title: '测试任务', status: 0 }; 

  } catch (error) {
    message.error('获取详情失败');
  } finally {
    loading.value = false;
  }
};

const showApproveModal = () => {
  comment.value = '同意';
  approveVisible.value = true;
};

const showRejectModal = () => {
  comment.value = '';
  rejectVisible.value = true;
};

const handleApprove = async () => {
  submitting.value = true;
  try {
    await decideApprovalTask({
      taskId: taskId,
      approved: true,
      comment: comment.value
    });
    message.success('已同意');
    approveVisible.value = false;
    router.back();
  } catch (error) {
    message.error('操作失败');
  } finally {
    submitting.value = false;
  }
};

const handleReject = async () => {
  submitting.value = true;
  try {
    await decideApprovalTask({
      taskId: taskId,
      approved: false,
      comment: comment.value
    });
    message.success('已驳回');
    rejectVisible.value = false;
    router.back();
  } catch (error) {
    message.error('操作失败');
  } finally {
    submitting.value = false;
  }
};

const handleMenuClick = ({ key }: { key: string }) => {
  if (key === 'jump') {
    jumpVisible.value = true;
  } else if (key === 'delegate') {
    // 弹窗选择人
  } else if (key === 'transfer') {
    // 弹窗选择人
  }
};

const handleJump = async (targetNodeId: string) => {
  try {
    await jumpTask(instanceId, targetNodeId);
    message.success('跳转成功');
    router.back();
  } catch (error) {
    message.error('跳转失败');
  }
};

const getStatusColor = (status: number) => {
  return status === 0 ? 'blue' : 'default';
};

const getStatusText = (status: number) => {
  return status === 0 ? '待审批' : '已结束';
};

onMounted(() => {
  fetchDetail();
});
</script>

<style scoped>
.task-detail-page {
  padding: 24px;
  background: #f0f2f5;
  min-height: 100vh;
}
.page-header {
  background: #fff;
  padding: 16px 24px;
  margin-bottom: 24px;
}
.content-layout {
  display: flex;
  gap: 24px;
}
.main-content {
  flex: 1;
}
.side-content {
  width: 350px;
}
.mb-4 {
  margin-bottom: 24px;
}
</style>

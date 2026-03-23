<template>
  <div class="dd-add-btn-wrap">
    <a-popover
      v-model:open="visible"
      placement="rightTop"
      trigger="click"
      :get-popup-container="getContainer"
    >
      <template #content>
        <div class="dd-add-popover">
          <div class="dd-add-popover__section">
            <div class="dd-add-popover__item" @click="handleSelect('approve')">
              <div class="dd-add-popover__icon dd-add-popover__icon--approve">
                <UserOutlined />
              </div>
              <span>{{ t('approvalDesigner.palNodeApprove') }}</span>
            </div>
            <div class="dd-add-popover__item" @click="handleSelect('copy')">
              <div class="dd-add-popover__icon dd-add-popover__icon--copy">
                <SendOutlined />
              </div>
              <span>{{ t('approvalDesigner.palNodeCopy') }}</span>
            </div>
          </div>
          <div class="dd-add-popover__divider"></div>
          <div class="dd-add-popover__section">
            <div class="dd-add-popover__item" @click="handleSelect('condition')">
              <div class="dd-add-popover__icon dd-add-popover__icon--condition">
                <BranchesOutlined />
              </div>
              <span>{{ t('approvalDesigner.palNodeCondition') }}</span>
            </div>
            <div class="dd-add-popover__item" @click="handleSelect('parallel')">
              <div class="dd-add-popover__icon dd-add-popover__icon--parallel">
                <ApartmentOutlined />
              </div>
              <span>{{ t('approvalDesigner.palNodeParallel') }}</span>
            </div>
            <div class="dd-add-popover__item" @click="handleSelect('inclusive')">
              <div class="dd-add-popover__icon dd-add-popover__icon--inclusive">
                <NodeIndexOutlined />
              </div>
              <span>{{ t('approvalDesigner.palNodeInclusive') }}</span>
            </div>
            <div class="dd-add-popover__item" @click="handleSelect('route')">
              <div class="dd-add-popover__icon dd-add-popover__icon--route">
                <SwapOutlined />
              </div>
              <span>{{ t('approvalDesigner.nodeWidgetRoute') }}</span>
            </div>
          </div>
          <div class="dd-add-popover__divider"></div>
          <div class="dd-add-popover__section">
            <div class="dd-add-popover__item" @click="handleSelect('callProcess')">
              <div class="dd-add-popover__icon dd-add-popover__icon--callProcess">
                <SubnodeOutlined />
              </div>
              <span>{{ t('approvalDesigner.palNodeSubflow') }}</span>
            </div>
            <div class="dd-add-popover__item" @click="handleSelect('timer')">
              <div class="dd-add-popover__icon dd-add-popover__icon--timer">
                <ClockCircleOutlined />
              </div>
              <span>{{ t('approvalDesigner.palNodeTimer') }}</span>
            </div>
            <div class="dd-add-popover__item" @click="handleSelect('trigger')">
              <div class="dd-add-popover__icon dd-add-popover__icon--trigger">
                <ThunderboltOutlined />
              </div>
              <span>{{ t('approvalDesigner.palNodeTrigger') }}</span>
            </div>
          </div>
        </div>
      </template>
      <button class="dd-add-btn" @click.stop>
        <PlusOutlined />
      </button>
    </a-popover>
  </div>
</template>

<script setup lang="ts">
import { inject, ref, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';

const { t } = useI18n();
import {
  PlusOutlined,
  UserOutlined,
  SendOutlined,
  BranchesOutlined,
  ApartmentOutlined,
  NodeIndexOutlined,
  SwapOutlined,
  SubnodeOutlined,
  ClockCircleOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons-vue';
import type { Node } from '@antv/x6';

const getNode = inject<() => Node>('getNode')!;
const data = ref<Record<string, unknown>>({});
const visible = ref(false);

onMounted(() => {
  const node = getNode();
  data.value = node.getData() || {};
});

const handleSelect = (nodeType: string) => {
  visible.value = false;
  const node = getNode();
  node.trigger('addNode:select', {
    parentId: data.value.parentId,
    nodeType,
  });
};

const getContainer = () => {
  // Mount popover inside the X6 canvas wrapper so it scrolls with graph
  return document.querySelector('.dd-designer-canvas') || document.body;
};
</script>

<style scoped>
.dd-add-popover__section {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.dd-add-popover__divider {
  height: 1px;
  background: #f0f0f0;
  margin: 6px 0;
}

.dd-add-popover__icon--parallel {
  background: #ff943e;
}

.dd-add-popover__icon--inclusive {
  background: #15bc83;
}

.dd-add-popover__icon--route {
  background: #718dff;
}

.dd-add-popover__icon--callProcess {
  background: #faad14;
}

.dd-add-popover__icon--timer {
  background: #f5222d;
}

.dd-add-popover__icon--trigger {
  background: #722ed1;
}
</style>

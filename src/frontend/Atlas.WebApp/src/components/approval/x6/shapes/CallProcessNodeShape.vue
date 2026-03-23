<template>
  <div
    class="dd-node dd-node--call-process"
    :class="{ 'is-error': data.error }"
    @click="handleClick"
  >
    <div class="dd-node__header dd-node__header--call-process">
      <SubnodeOutlined class="dd-node__icon" />
      <span class="dd-node__title">{{ data.nodeName || t('approvalDesigner.shapeCallProcessDefault') }}</span>
      <CloseOutlined class="dd-node__delete" @click.stop="handleDelete" />
    </div>
    <div class="dd-node__body">
      <span v-if="data.callProcessId" class="dd-node__text">{{ t('approvalDesigner.shapeCallProcessId', { id: data.callProcessId }) }}</span>
      <span v-else class="dd-node__placeholder">{{ t('approvalDesigner.shapeConfigureCallProcess') }}</span>
      <div v-if="data.callProcessId" class="dd-node__sub-text">
        {{ data.callAsync ? t('approvalDesigner.shapeExecAsync') : t('approvalDesigner.shapeExecSync') }}
      </div>
      <RightOutlined class="dd-node__arrow" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { inject, ref, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { SubnodeOutlined, CloseOutlined, RightOutlined } from '@ant-design/icons-vue';

const { t } = useI18n();
import type { Node } from '@antv/x6';

const getNode = inject<() => Node>('getNode')!;
const data = ref<Record<string, unknown>>({});

onMounted(() => {
  const node = getNode();
  data.value = node.getData() || {};
  node.on('change:data', ({ current }: { current: Record<string, unknown> }) => {
    data.value = { ...current };
  });
});

const handleClick = () => {
  const node = getNode();
  node.trigger('node:select', { nodeData: data.value });
};

const handleDelete = () => {
  const node = getNode();
  node.trigger('node:delete', { nodeId: data.value.id });
};
</script>

<style scoped>
.dd-node__header--call-process {
  background: #faad14;
}
.dd-node__sub-text {
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 4px;
}
</style>

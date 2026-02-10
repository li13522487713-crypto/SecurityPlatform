<template>
  <div class="dd-palette">
    <div class="dd-palette__title">节点库</div>
    <div
      v-for="item in nodeTypes"
      :key="item.type"
      class="dd-palette__item"
      @click="emit('addNode', item.type)"
    >
      <div class="dd-palette__icon" :style="{ background: item.color }">
        <component :is="item.icon" />
      </div>
      <span class="dd-palette__label">{{ item.label }}</span>
    </div>
    <div class="dd-palette__divider" />
    <div
      class="dd-palette__item"
      @click="emit('addDefaultStartEnd')"
    >
      <div class="dd-palette__icon" style="background: #576a95">
        <PlayCircleOutlined />
      </div>
      <span class="dd-palette__label">初始化</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import {
  UserOutlined,
  SendOutlined,
  BranchesOutlined,
  ApartmentOutlined,
  PlayCircleOutlined,
} from '@ant-design/icons-vue';

const nodeTypes = [
  { type: 'approve', label: '审批人', icon: UserOutlined, color: '#ff943e' },
  { type: 'copy', label: '抄送', icon: SendOutlined, color: '#3296fa' },
  { type: 'condition', label: '条件', icon: BranchesOutlined, color: '#15bc83' },
  { type: 'parallel', label: '并行', icon: ApartmentOutlined, color: '#ff943e' },
];

const emit = defineEmits<{
  addNode: [nodeType: string];
  addDefaultStartEnd: [];
}>();
</script>

<style scoped>
.dd-palette {
  width: 72px;
  flex-shrink: 0;
  background: #fff;
  border-right: 1px solid #e8e8e8;
  padding: 8px 0;
  overflow-y: auto;
  user-select: none;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
}
.dd-palette__title {
  font-size: 10px;
  font-weight: 600;
  color: #8c8c8c;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-bottom: 4px;
}
.dd-palette__item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  padding: 6px 4px;
  border-radius: 6px;
  cursor: pointer;
  transition: background 0.15s;
  width: 60px;
}
.dd-palette__item:hover {
  background: #f0f0f0;
}
.dd-palette__icon {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 14px;
}
.dd-palette__label {
  font-size: 11px;
  color: #595959;
  line-height: 1;
}
.dd-palette__divider {
  width: 40px;
  height: 1px;
  background: #e8e8e8;
  margin: 4px 0;
}
</style>

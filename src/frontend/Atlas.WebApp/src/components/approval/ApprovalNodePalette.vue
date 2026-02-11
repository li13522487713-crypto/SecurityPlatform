<template>
  <transition name="dd-palette-slide">
    <div v-show="visible" class="dd-palette">
      <div class="dd-palette__header">
        <span>节点面板</span>
        <button class="dd-palette__close" @click="emit('update:visible', false)" title="关闭">
          <CloseOutlined />
        </button>
      </div>
      <div
        v-for="group in nodeGroups"
        :key="group.title"
        class="dd-palette__group"
      >
        <div class="dd-palette__group-title">{{ group.title }}</div>
        <div
          v-for="item in group.items"
          :key="item.type"
          class="dd-palette__item"
          @click="emit('addNode', item.type)"
        >
          <div class="dd-palette__icon" :style="{ background: item.color }">
            <component :is="item.icon" />
          </div>
          <div class="dd-palette__info">
            <span class="dd-palette__label">{{ item.label }}</span>
            <span class="dd-palette__desc">{{ item.desc }}</span>
          </div>
        </div>
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import {
  UserOutlined,
  SendOutlined,
  BranchesOutlined,
  ApartmentOutlined,
  NodeIndexOutlined,
  SwapOutlined,
  SubnodeOutlined,
  ClockCircleOutlined,
  ThunderboltOutlined,
  CloseOutlined,
} from '@ant-design/icons-vue';

defineProps<{
  visible: boolean;
}>();

const nodeGroups = [
  {
    title: '基础节点',
    items: [
      { type: 'approve', label: '审批人', desc: '指定人员进行审批', icon: UserOutlined, color: '#ff943e' },
      { type: 'copy', label: '抄送人', desc: '抄送通知相关人员', icon: SendOutlined, color: '#3296fa' },
    ]
  },
  {
    title: '流程控制',
    items: [
      { type: 'condition', label: '条件分支', desc: '根据条件分流', icon: BranchesOutlined, color: '#15bc83' },
      { type: 'parallel', label: '并行分支', desc: '多任务同时进行', icon: ApartmentOutlined, color: '#ff943e' },
      { type: 'inclusive', label: '包容分支', desc: '满足条件的分支都执行', icon: NodeIndexOutlined, color: '#15bc83' },
      { type: 'route', label: '路由分支', desc: '重定向到指定节点', icon: SwapOutlined, color: '#718dff' },
    ]
  },
  {
    title: '高级节点',
    items: [
      { type: 'callProcess', label: '子流程', desc: '调用外部流程', icon: SubnodeOutlined, color: '#faad14' },
      { type: 'timer', label: '定时器', desc: '延时或定时执行', icon: ClockCircleOutlined, color: '#f5222d' },
      { type: 'trigger', label: '触发器', desc: '触发外部动作', icon: ThunderboltOutlined, color: '#722ed1' },
    ]
  }
];

const emit = defineEmits<{
  addNode: [nodeType: string];
  'update:visible': [value: boolean];
}>();
</script>

<style scoped>
.dd-palette {
  width: 220px;
  flex-shrink: 0;
  background: #fff;
  border-right: 1px solid #e8e8e8;
  overflow-y: auto;
  user-select: none;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.dd-palette__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px 0;
  font-size: 14px;
  font-weight: 600;
  color: #1f1f1f;
}

.dd-palette__close {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border: none;
  background: transparent;
  cursor: pointer;
  border-radius: 4px;
  color: #8c8c8c;
  font-size: 12px;
  transition: all 0.2s;
}

.dd-palette__close:hover {
  background: #f0f0f0;
  color: #1677ff;
}

.dd-palette__group-title {
  padding: 0 16px;
  font-size: 12px;
  color: #8c8c8c;
  margin-bottom: 4px;
}

.dd-palette__item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 16px;
  cursor: pointer;
  transition: background 0.15s;
}

.dd-palette__item:hover {
  background: #f0f5ff;
}

.dd-palette__item:active {
  background: #e6f0ff;
}

.dd-palette__icon {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 16px;
  flex-shrink: 0;
  box-shadow: 0 2px 6px rgba(0,0,0,0.06);
}

.dd-palette__info {
  display: flex;
  flex-direction: column;
  gap: 2px;
  overflow: hidden;
}

.dd-palette__label {
  font-size: 13px;
  font-weight: 500;
  color: #1f1f1f;
  line-height: 1.2;
}

.dd-palette__desc {
  font-size: 11px;
  color: #8c8c8c;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* 侧边栏滑入/滑出动画 */
.dd-palette-slide-enter-active,
.dd-palette-slide-leave-active {
  transition: all 0.25s ease;
}
.dd-palette-slide-enter-from,
.dd-palette-slide-leave-to {
  transform: translateX(-100%);
  opacity: 0;
}
</style>

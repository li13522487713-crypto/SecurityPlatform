<template>
  <transition name="dd-palette-slide">
    <div v-show="visible" class="dd-palette">
      <div class="dd-palette__header">
        <span>{{ t('approvalDesigner.palTitle') }}</span>
        <button class="dd-palette__close" :title="t('approvalDesigner.palCloseTitle')" @click="emit('update:visible', false)">
          <CloseOutlined />
        </button>
      </div>
      <div class="dd-palette__search">
        <SearchOutlined />
        <input
          v-model.trim="keyword"
          class="dd-palette__search-input"
          type="text"
          :placeholder="t('approvalDesigner.palSearchPh')"
        />
      </div>
      <div
        v-for="group in filteredNodeGroups"
        :key="group.key"
        class="dd-palette__group"
      >
        <button class="dd-palette__group-title" type="button" @click="toggleGroup(group.key)">
          <component :is="expandedGroups.has(group.key) ? DownOutlined : RightOutlined" />
          <span>{{ group.title }}</span>
          <span class="dd-palette__group-count">{{ group.items.length }}</span>
        </button>
        <div v-show="expandedGroups.has(group.key)">
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
      <div v-if="filteredNodeGroups.length === 0" class="dd-palette__empty">
        {{ t('approvalDesigner.palEmpty') }}
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import type { Component } from 'vue';
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
  SearchOutlined,
  DownOutlined,
  RightOutlined,
} from '@ant-design/icons-vue';

const { t } = useI18n();

defineProps<{
  visible: boolean;
}>();

type PaletteItem = { type: string; label: string; desc: string; icon: Component; color: string };
type PaletteGroup = { key: string; title: string; items: PaletteItem[] };

const groupDefs: Array<{
  key: string;
  titleKey: string;
  items: Array<{ type: string; labelKey: string; descKey: string; icon: Component; color: string }>;
}> = [
  {
    key: 'basic',
    titleKey: 'approvalDesigner.palCatBasic',
    items: [
      { type: 'approve', labelKey: 'approvalDesigner.palNodeApprove', descKey: 'approvalDesigner.palNodeApproveDesc', icon: UserOutlined, color: '#ff943e' },
      { type: 'copy', labelKey: 'approvalDesigner.palNodeCopy', descKey: 'approvalDesigner.palNodeCopyDesc', icon: SendOutlined, color: '#3296fa' },
    ],
  },
  {
    key: 'control',
    titleKey: 'approvalDesigner.palCatControl',
    items: [
      { type: 'condition', labelKey: 'approvalDesigner.palNodeCondition', descKey: 'approvalDesigner.palNodeConditionDesc', icon: BranchesOutlined, color: '#15bc83' },
      { type: 'parallel', labelKey: 'approvalDesigner.palNodeParallel', descKey: 'approvalDesigner.palNodeParallelDesc', icon: ApartmentOutlined, color: '#ff943e' },
      { type: 'inclusive', labelKey: 'approvalDesigner.palNodeInclusive', descKey: 'approvalDesigner.palNodeInclusiveDesc', icon: NodeIndexOutlined, color: '#15bc83' },
      { type: 'route', labelKey: 'approvalDesigner.palNodeRoute', descKey: 'approvalDesigner.palNodeRouteDesc', icon: SwapOutlined, color: '#718dff' },
    ],
  },
  {
    key: 'advanced',
    titleKey: 'approvalDesigner.palCatAdvanced',
    items: [
      { type: 'callProcess', labelKey: 'approvalDesigner.palNodeSubflow', descKey: 'approvalDesigner.palNodeSubflowDesc', icon: SubnodeOutlined, color: '#faad14' },
      { type: 'timer', labelKey: 'approvalDesigner.palNodeTimer', descKey: 'approvalDesigner.palNodeTimerDesc', icon: ClockCircleOutlined, color: '#f5222d' },
      { type: 'trigger', labelKey: 'approvalDesigner.palNodeTrigger', descKey: 'approvalDesigner.palNodeTriggerDesc', icon: ThunderboltOutlined, color: '#722ed1' },
    ],
  },
];

const nodeGroups = computed((): PaletteGroup[] =>
  groupDefs.map((g) => ({
    key: g.key,
    title: t(g.titleKey),
    items: g.items.map((it) => ({
      type: it.type,
      label: t(it.labelKey),
      desc: t(it.descKey),
      icon: it.icon,
      color: it.color,
    })),
  })),
);

const keyword = ref('');
const expandedGroups = ref(new Set(groupDefs.map((g) => g.key)));

const filteredNodeGroups = computed(() => {
  const groups = nodeGroups.value;
  const search = keyword.value.toLowerCase();
  if (!search) {
    return groups;
  }

  return groups
    .map((group) => ({
      ...group,
      items: group.items.filter((item) =>
        item.label.toLowerCase().includes(search) || item.desc.toLowerCase().includes(search),
      ),
    }))
    .filter((group) => group.items.length > 0);
});

const toggleGroup = (key: string) => {
  if (expandedGroups.value.has(key)) {
    expandedGroups.value.delete(key);
  } else {
    expandedGroups.value.add(key);
  }
};

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

.dd-palette__search {
  margin: 0 12px;
  padding: 0 10px;
  height: 32px;
  display: flex;
  align-items: center;
  gap: 8px;
  border: 1px solid #e8e8e8;
  border-radius: 6px;
  color: #8c8c8c;
}

.dd-palette__search-input {
  border: none;
  outline: none;
  flex: 1;
  font-size: 12px;
  color: #1f1f1f;
  background: transparent;
}

.dd-palette__group {
  display: flex;
  flex-direction: column;
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
  display: flex;
  align-items: center;
  gap: 6px;
  width: 100%;
  padding: 8px 16px;
  border: none;
  background: transparent;
  cursor: pointer;
  font-size: 12px;
  font-weight: 600;
  color: #595959;
  text-align: left;
}

.dd-palette__group-title:hover {
  background: #fafafa;
}

.dd-palette__group-count {
  margin-left: auto;
  font-size: 11px;
  color: #8c8c8c;
  font-weight: 400;
}

.dd-palette__item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 16px 8px 28px;
  cursor: pointer;
  transition: background 0.2s;
}

.dd-palette__item:hover {
  background: #f5f5f5;
}

.dd-palette__icon {
  width: 32px;
  height: 32px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 16px;
  flex-shrink: 0;
}

.dd-palette__info {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.dd-palette__label {
  font-size: 13px;
  font-weight: 500;
  color: #1f1f1f;
}

.dd-palette__desc {
  font-size: 11px;
  color: #8c8c8c;
  line-height: 1.4;
}

.dd-palette__empty {
  padding: 16px;
  text-align: center;
  font-size: 12px;
  color: #8c8c8c;
}

.dd-palette-slide-enter-active,
.dd-palette-slide-leave-active {
  transition: transform 0.2s ease, opacity 0.2s ease;
}

.dd-palette-slide-enter-from,
.dd-palette-slide-leave-to {
  transform: translateX(-12px);
  opacity: 0;
}
</style>

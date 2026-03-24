<template>
  <div class="right-panel">
    <div v-if="selectedComponent && activeMeta" class="panel-header">
      <strong>{{ activeMeta.name }}</strong> 配置
      <div class="component-id">ID: {{ selectedComponent.id }}</div>
    </div>
    <div v-else class="panel-header empty">
      请在画布中选中组件
    </div>

    <div v-if="selectedComponent && activeMeta" class="panel-content">
      <a-tabs v-model:active-key="activeTab" :tab-bar-style="{ padding: '0 16px', margin: 0 }" centered>
        <a-tab-pane v-if="hasGroup('basic') || hasGroup('data')" key="basic" tab="属性">
          <div class="settings-form">
            <template v-for="prop in (activeMeta.propsGroup.basic || [])" :key="prop.name">
              <div class="form-item">
                <div class="form-label">{{ prop.label }}</div>
                <div class="form-control">
                  <a-input v-if="prop.type === 'string'" v-model:value="selectedComponent.props[prop.name]" size="small" @change="store.commit" />
                  <a-switch v-else-if="prop.type === 'boolean'" v-model:checked="selectedComponent.props[prop.name]" size="small" @change="store.commit" />
                  <a-select v-else-if="prop.type === 'select'" v-model:value="selectedComponent.props[prop.name]" :options="prop.options" size="small" style="width: 100%" @change="store.commit" />
                </div>
              </div>
            </template>
            <template v-if="hasGroup('data')">
              <a-divider style="margin: 16px 0 12px; font-size: 13px;">数据与事件配置</a-divider>
              <template v-for="prop in activeMeta.propsGroup.data" :key="prop.name">
                <div class="form-item">
                  <div class="form-label">{{ prop.label }}</div>
                  <div class="form-control">
                    <a-button type="dashed" size="small" block @click="editJsonData(prop.name)">配置 JSON</a-button>
                  </div>
                </div>
              </template>
            </template>
          </div>
        </a-tab-pane>

        <a-tab-pane v-if="hasGroup('style')" key="style" tab="样式">
          <div class="settings-form">
            <template v-for="prop in activeMeta.propsGroup.style" :key="prop.name">
              <div class="form-item">
                <div class="form-label">{{ prop.label }}</div>
                <div class="form-control">
                  <a-input v-if="prop.type === 'string'" v-model:value="selectedComponent.styles[prop.name]" size="small" @change="store.commit" />
                  <div v-else-if="prop.type === 'color'" style="display: flex; gap: 8px;">
                    <input v-model="selectedComponent.styles[prop.name]" type="color" style="width: 28px; height: 24px; padding: 0; border: 1px solid #d9d9d9; border-radius: 4px; cursor: pointer;" @change="store.commit" />
                    <a-input v-model:value="selectedComponent.styles[prop.name]" size="small" style="flex: 1;" @change="store.commit" />
                  </div>
                  <a-select v-else-if="prop.type === 'select'" v-model:value="selectedComponent.styles[prop.name]" :options="prop.options" size="small" style="width: 100%" @change="store.commit" />
                </div>
              </div>
            </template>
          </div>
        </a-tab-pane>

        <a-tab-pane v-if="hasGroup('event')" key="event" tab="事件">
           <div class="settings-form">
            <template v-for="prop in activeMeta.propsGroup.event" :key="prop.name">
              <div class="form-item">
                <div class="form-label">{{ prop.label }}</div>
                <div class="form-control">
                  <a-button type="dashed" size="small" block>配置动作</a-button>
                </div>
              </div>
            </template>
          </div>
        </a-tab-pane>
      </a-tabs>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useDesignerStore } from '../core/store';
import { COMPONENT_REGISTRY } from '../core/registry';
import { message } from 'ant-design-vue';

const store = useDesignerStore();
const activeTab = ref('basic');

const selectedComponent = computed(() => store.selectedComponent);

const activeMeta = computed(() => {
  if (!selectedComponent.value) return null;
  return COMPONENT_REGISTRY[selectedComponent.value.type];
});

const hasGroup = (groupName: 'basic' | 'style' | 'data' | 'event') => {
  return (activeMeta.value?.propsGroup[groupName]?.length ?? 0) > 0;
};

// Reset tab when selection changes
watch(selectedComponent, () => {
  if (selectedComponent.value && activeMeta.value) {
    if (activeTab.value === 'basic' && !hasGroup('basic') && !hasGroup('data')) activeTab.value = 'style';
  }
});

const editJsonData = (propName: string) => {
  message.info('JSON数据配置弹窗尚未实现');
};
</script>

<style scoped>
.right-panel {
  width: 300px;
  background-color: #ffffff;
  border-left: 1px solid #f0f0f0;
  display: flex;
  flex-direction: column;
  height: 100%;
}
.panel-header {
  padding: 16px;
  border-bottom: 1px solid #f0f0f0;
  font-size: 14px;
  color: #333;
}
.panel-header.empty {
  color: #999;
  text-align: center;
  padding: 30px 16px;
}
.component-id {
  font-size: 12px;
  color: #999;
  margin-top: 4px;
}
.panel-content {
  flex: 1;
  overflow-y: auto;
}
.settings-form {
  padding: 16px;
}
.form-item {
  margin-bottom: 16px;
}
.form-label {
  font-size: 13px;
  color: #666;
  margin-bottom: 8px;
}
.form-control {
  font-size: 13px;
}
</style>

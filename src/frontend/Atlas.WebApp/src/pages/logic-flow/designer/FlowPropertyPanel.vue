<template>
  <div class="flow-property-panel">
    <a-tabs v-model:active-key="activeTab" type="card" size="small">
      <a-tab-pane key="basic" :tab="t('logicFlow.designerUi.property.tabs.basic')">
        <a-form layout="vertical" class="prop-form">
          <template v-if="selection.kind === 'none'">
            <a-form-item :label="t('logicFlow.flowName')">
              <a-input v-model:value="flowLevel.name" />
            </a-form-item>
            <a-form-item :label="t('logicFlow.version')">
              <a-input v-model:value="flowLevel.version" />
            </a-form-item>
            <a-form-item :label="t('logicFlow.functionDesigner.description')">
              <a-textarea v-model:value="flowLevel.description" :rows="3" />
            </a-form-item>
          </template>
          <template v-else-if="selection.kind === 'node'">
            <a-form-item :label="t('logicFlow.designerUi.property.nodeId')">
              <a-input :value="selection.nodeId" disabled />
            </a-form-item>
            <a-form-item :label="t('logicFlow.designerUi.property.nodeType')">
              <a-select v-model:value="nodeForm.typeKey" style="width: 100%">
                <a-select-option value="trigger.manual">trigger.manual</a-select-option>
                <a-select-option value="data.query">data.query</a-select-option>
              </a-select>
            </a-form-item>
            <a-form-item :label="t('logicFlow.designerUi.property.displayName')">
              <a-input v-model:value="nodeForm.displayName" />
            </a-form-item>
          </template>
          <template v-else>
            <FlowConnectionStyle
              :line-style="edgeForm.lineStyle"
              :color="edgeForm.color"
              :label="edgeForm.label"
              :animated="edgeForm.animated"
              @update:line-style="edgeForm.lineStyle = $event"
              @update:color="edgeForm.color = $event"
              @update:label="edgeForm.label = $event"
              @update:animated="edgeForm.animated = $event"
            />
          </template>
        </a-form>
      </a-tab-pane>
      <a-tab-pane key="binding" :tab="t('logicFlow.designerUi.property.tabs.binding')" :disabled="selection.kind === 'none'">
        <a-form layout="vertical">
          <a-form-item :label="t('logicFlow.designerUi.property.bindingKey')">
            <a-input v-model:value="bindingJson" />
          </a-form-item>
        </a-form>
      </a-tab-pane>
      <a-tab-pane key="advanced" :tab="t('logicFlow.designerUi.property.tabs.advanced')">
        <a-form layout="vertical">
          <a-form-item :label="t('logicFlow.designerUi.property.timeoutSeconds')">
            <a-input-number v-model:value="advanced.timeoutSeconds" :min="0" style="width: 100%" />
          </a-form-item>
          <a-form-item :label="t('logicFlow.designerUi.property.retry')">
            <a-switch v-model:checked="advanced.retry" />
          </a-form-item>
        </a-form>
      </a-tab-pane>
      <a-tab-pane key="error" :tab="t('logicFlow.designerUi.property.tabs.error')">
        <a-form layout="vertical">
          <a-form-item :label="t('logicFlow.designerUi.property.onError')">
            <a-select v-model:value="errorPolicy" style="width: 100%">
              <a-select-option value="fail">{{ t("logicFlow.designerUi.property.policyFail") }}</a-select-option>
              <a-select-option value="continue">{{ t("logicFlow.designerUi.property.policyContinue") }}</a-select-option>
            </a-select>
          </a-form-item>
        </a-form>
      </a-tab-pane>
      <a-tab-pane key="debug" :tab="t('logicFlow.designerUi.property.tabs.debug')">
        <a-form layout="vertical">
          <a-form-item :label="t('logicFlow.designerUi.property.debugLogging')">
            <a-switch v-model:checked="debugLogging" />
          </a-form-item>
        </a-form>
      </a-tab-pane>
    </a-tabs>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import FlowConnectionStyle from "./FlowConnectionStyle.vue";

export type FlowSelection =
  | { kind: "none" }
  | { kind: "node"; nodeId: string }
  | { kind: "edge"; edgeId: string };

const props = withDefaults(
  defineProps<{
    selection?: FlowSelection;
  }>(),
  {
    selection: () => ({ kind: "none" })
  }
);

const { t } = useI18n();
const activeTab = ref("basic");
const bindingJson = ref("{}");
const errorPolicy = ref<"fail" | "continue">("fail");
const debugLogging = ref(false);

const flowLevel = reactive({
  name: "",
  version: "1.0.0",
  description: ""
});

const nodeForm = reactive({
  typeKey: "trigger.manual",
  displayName: ""
});

const edgeForm = reactive({
  lineStyle: "solid" as "solid" | "dashed" | "dotted",
  color: "#8c8c8c",
  label: "",
  animated: false
});

const advanced = reactive({
  timeoutSeconds: 30,
  retry: true
});

watch(
  () => props.selection,
  (sel) => {
    if (sel.kind === "node") {
      nodeForm.displayName = sel.nodeId;
    }
    activeTab.value = "basic";
  },
  { deep: true }
);
</script>

<style scoped>
.flow-property-panel {
  min-width: 260px;
  max-width: 400px;
  background: #fff;
  border-left: 1px solid #f0f0f0;
}

.prop-form {
  padding-top: 8px;
}
</style>

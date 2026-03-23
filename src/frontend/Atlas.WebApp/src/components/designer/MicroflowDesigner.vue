<template>
  <div class="microflow-designer">
    <div class="microflow-header">
      <h3>{{ t("designer.microflow.title") }}</h3>
      <a-space>
        <a-button type="primary" @click="addStep">
          {{ t("designer.microflow.addStep") }}
        </a-button>
        <a-button :disabled="steps.length === 0" @click="executeFlow">
          {{ t("designer.microflow.execute") }}
        </a-button>
      </a-space>
    </div>

    <div v-if="steps.length === 0" class="microflow-empty">
      {{ t("designer.microflow.noSteps") }}
    </div>

    <div class="microflow-steps">
      <div
        v-for="(step, idx) in steps"
        :key="step.id"
        class="microflow-step-card"
      >
        <div class="step-header">
          <span class="step-index">{{ idx + 1 }}</span>
          <a-input
            v-model:value="step.name"
            :placeholder="t('designer.microflow.stepName')"
            size="small"
            style="width: 160px"
          />
          <a-select
            v-model:value="step.type"
            :placeholder="t('designer.microflow.stepType')"
            size="small"
            style="width: 140px"
          >
            <a-select-option value="api_call">{{ t("designer.microflow.apiCall") }}</a-select-option>
            <a-select-option value="condition">{{ t("designer.microflow.condition") }}</a-select-option>
            <a-select-option value="set_variable">{{ t("designer.microflow.setVariable") }}</a-select-option>
            <a-select-option value="notification">{{ t("designer.microflow.notification") }}</a-select-option>
          </a-select>
          <a-button danger size="small" @click="removeStep(idx)">
            {{ t("designer.microflow.removeStep") }}
          </a-button>
        </div>

        <div class="step-config">
          <template v-if="step.type === 'api_call'">
            <a-form-item :label="t('designer.microflow.url')">
              <a-input v-model:value="step.config.url" placeholder="https://..." />
            </a-form-item>
            <a-form-item :label="t('designer.microflow.method')">
              <a-select v-model:value="step.config.method" style="width: 120px">
                <a-select-option value="GET">GET</a-select-option>
                <a-select-option value="POST">POST</a-select-option>
                <a-select-option value="PUT">PUT</a-select-option>
                <a-select-option value="DELETE">DELETE</a-select-option>
              </a-select>
            </a-form-item>
          </template>

          <template v-if="step.type === 'condition'">
            <a-form-item :label="t('designer.microflow.field')">
              <a-input v-model:value="step.config.field" />
            </a-form-item>
            <a-form-item :label="t('designer.microflow.value')">
              <a-input v-model:value="step.config.value" />
            </a-form-item>
          </template>

          <template v-if="step.type === 'set_variable'">
            <a-form-item :label="t('designer.microflow.variableName')">
              <a-input v-model:value="step.config.variableName" />
            </a-form-item>
            <a-form-item :label="t('designer.microflow.variableValue')">
              <a-input v-model:value="step.config.variableValue" />
            </a-form-item>
          </template>

          <template v-if="step.type === 'notification'">
            <a-form-item :label="t('designer.microflow.message')">
              <a-textarea v-model:value="step.config.message" :rows="2" />
            </a-form-item>
          </template>
        </div>

        <div v-if="idx < steps.length - 1" class="step-connector">
          <div class="connector-line" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import { executeMicroflow, type MicroflowStep } from "@/services/lowcode";

const { t } = useI18n();

interface DesignerStep {
  id: string;
  type: MicroflowStep["type"];
  name: string;
  config: Record<string, string>;
}

const steps = ref<DesignerStep[]>([]);

function addStep() {
  steps.value.push({
    id: crypto.randomUUID(),
    type: "api_call",
    name: "",
    config: { url: "", method: "GET" },
  });
}

function removeStep(idx: number) {
  steps.value.splice(idx, 1);
}

async function executeFlow() {
  if (steps.value.length === 0) {
    message.warning(t("designer.microflow.noSteps"));
    return;
  }
  try {
    const payload: MicroflowStep[] = steps.value.map((s) => ({
      type: s.type,
      name: s.name,
      config: { ...s.config },
    }));
    await executeMicroflow(payload);
    message.success(t("designer.microflow.executeSuccess"));
  } catch {
    message.error(t("designer.microflow.executeFailed"));
  }
}

defineExpose({ steps });
</script>

<style scoped>
.microflow-designer {
  padding: 16px;
}
.microflow-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
.microflow-header h3 {
  margin: 0;
}
.microflow-empty {
  text-align: center;
  padding: 48px;
  color: #999;
}
.microflow-step-card {
  border: 1px solid #e8e8e8;
  border-radius: 8px;
  padding: 12px 16px;
  margin-bottom: 8px;
  background: #fafafa;
}
.step-header {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 8px;
}
.step-index {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: #1677ff;
  color: #fff;
  font-size: 12px;
  font-weight: 600;
  flex-shrink: 0;
}
.step-config {
  padding-left: 32px;
}
.step-connector {
  display: flex;
  justify-content: center;
  padding: 4px 0;
}
.connector-line {
  width: 2px;
  height: 16px;
  background: #d9d9d9;
}
</style>

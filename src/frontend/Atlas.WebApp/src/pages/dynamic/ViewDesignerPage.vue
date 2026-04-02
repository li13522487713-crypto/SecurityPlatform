<template>
  <div class="view-designer-page">
    <a-page-header :title="t('dynamic.viewDesignerWizard.title')" @back="$router.back()">
      <template #extra>
        <a-button type="primary" :disabled="currentStep < lastStep" @click="saveDraft">{{ t("fieldDesign.saveDraft") }}</a-button>
      </template>
    </a-page-header>

    <a-card :bordered="false" class="wizard-card">
      <a-steps v-model:current="currentStep" type="navigation" size="small" class="wizard-steps">
        <a-step :title="t('dynamic.viewDesignerWizard.stepSource')" />
        <a-step :title="t('dynamic.viewDesignerWizard.stepJoin')" />
        <a-step :title="t('dynamic.viewDesignerWizard.stepColumns')" />
        <a-step :title="t('dynamic.viewDesignerWizard.stepAggregate')" />
        <a-step :title="t('dynamic.viewDesignerWizard.stepPreview')" />
      </a-steps>

      <div class="step-body">
        <div v-show="currentStep === 0" class="step-pane">
          <a-form layout="vertical">
            <a-form-item :label="t('dynamic.viewDesignerWizard.sourceTable')">
              <a-select
                v-model:value="sourceTable"
                show-search
                :filter-option="filterOption"
                :options="tableOptions"
                style="width: 100%"
              />
            </a-form-item>
          </a-form>
        </div>
        <div v-show="currentStep === 1" class="step-pane">
          <a-button type="dashed" block class="add-join" @click="addJoin">{{ t("dynamic.viewDesignerWizard.addJoin") }}</a-button>
          <a-table :columns="joinColumns" :data-source="joins" row-key="id" size="small" :pagination="false">
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'type'">
                <a-select v-model:value="record.joinType" style="width: 100%">
                  <a-select-option value="LEFT">{{ t("dynamic.viewDesignerWizard.joinLeft") }}</a-select-option>
                  <a-select-option value="INNER">{{ t("dynamic.viewDesignerWizard.joinInner") }}</a-select-option>
                  <a-select-option value="RIGHT">{{ t("dynamic.viewDesignerWizard.joinRight") }}</a-select-option>
                </a-select>
              </template>
              <template v-if="column.key === 'actions'">
                <a-button type="link" danger size="small" @click="removeJoin(record.id)">{{ t("common.delete") }}</a-button>
              </template>
            </template>
          </a-table>
        </div>
        <div v-show="currentStep === 2" class="step-pane">
          <a-transfer
            v-model:target-keys="selectedColumns"
            :data-source="columnOptions"
            :titles="[t('dynamic.viewDesignerWizard.available'), t('dynamic.viewDesignerWizard.selected')]"
            show-search
            :render="transferRender"
          />
        </div>
        <div v-show="currentStep === 3" class="step-pane">
          <a-form layout="vertical">
            <a-form-item :label="t('dynamic.viewDesignerWizard.groupBy')">
              <a-select v-model:value="groupBy" mode="tags" style="width: 100%" :placeholder="t('dynamic.viewDesignerWizard.groupByPlaceholder')" />
            </a-form-item>
            <a-form-item :label="t('dynamic.viewDesignerWizard.aggregations')">
              <a-select v-model:value="aggregations" mode="multiple" style="width: 100%" :placeholder="t('dynamic.viewDesignerWizard.aggPlaceholder')">
                <a-select-option value="COUNT">COUNT</a-select-option>
                <a-select-option value="SUM">SUM</a-select-option>
                <a-select-option value="AVG">AVG</a-select-option>
                <a-select-option value="MAX">MAX</a-select-option>
                <a-select-option value="MIN">MIN</a-select-option>
              </a-select>
            </a-form-item>
          </a-form>
        </div>
        <div v-show="currentStep === 4" class="step-pane">
          <div class="sql-label">{{ t("dynamic.viewDesignerWizard.sqlPreview") }}</div>
          <pre class="sql-preview">{{ sqlPreview }}</pre>
        </div>
      </div>

      <div class="step-actions">
        <a-button :disabled="currentStep <= 0" @click="currentStep -= 1">{{ t("dynamic.viewDesignerWizard.prev") }}</a-button>
        <a-button type="primary" :disabled="currentStep >= lastStep" @click="currentStep += 1">{{ t("dynamic.viewDesignerWizard.next") }}</a-button>
      </div>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import type { TableColumnType } from "ant-design-vue";
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const currentStep = ref(0);
const lastStep = 4;

const sourceTable = ref<string | undefined>(undefined);
const tableOptions = ref<{ label: string; value: string }[]>([
  { label: "orders", value: "orders" },
  { label: "order_items", value: "order_items" }
]);

interface JoinRow {
  id: string;
  table: string;
  on: string;
  joinType: "LEFT" | "INNER" | "RIGHT";
}

let joinSeq = 0;
const joins = ref<JoinRow[]>([]);

const joinColumns = computed<TableColumnType[]>(() => [
  { title: t("dynamic.viewDesignerWizard.joinTable"), dataIndex: "table", key: "table" },
  { title: t("dynamic.viewDesignerWizard.joinOn"), dataIndex: "on", key: "on" },
  { title: t("dynamic.viewDesignerWizard.joinType"), key: "type", width: 140 },
  { title: t("dynamic.schemaSnapshotPanel.colActions"), key: "actions", width: 80 }
]);

type TransferItem = { key: string; title: string };

const columnOptions = ref<TransferItem[]>([
  { key: "id", title: "id" },
  { key: "name", title: "name" },
  { key: "amount", title: "amount" }
]);

function transferRender(item: TransferItem): string {
  return item.title;
}

const selectedColumns = ref<string[]>(["id", "name"]);
const groupBy = ref<string[]>([]);
const aggregations = ref<string[]>([]);

const sqlPreview = computed(() => {
  const cols = selectedColumns.value.length ? selectedColumns.value.join(", ") : "*";
  const grp = groupBy.value.length ? `\nGROUP BY ${groupBy.value.join(", ")}` : "";
  const agg = aggregations.value.length ? `\n-- AGG: ${aggregations.value.join(", ")}` : "";
  return `SELECT ${cols}\nFROM ${sourceTable.value ?? "<table>"}${agg}${grp};`;
});

function filterOption(input: string, option: { label?: string; value?: string } | undefined): boolean {
  const q = input.toLowerCase();
  const label = (option?.label ?? option?.value ?? "").toLowerCase();
  return label.includes(q);
}

function addJoin(): void {
  joinSeq += 1;
  joins.value = [
    ...joins.value,
    { id: `j${joinSeq}`, table: "order_items", on: "orders.id = order_items.order_id", joinType: "LEFT" }
  ];
}

function removeJoin(id: string): void {
  joins.value = joins.value.filter((j) => j.id !== id);
}

function saveDraft(): void {
  // 占位：接入保存 API
}
</script>

<style scoped>
.view-designer-page {
  padding: 0 0 24px;
}

.wizard-card {
  margin: 16px 24px;
}

.wizard-steps {
  margin-bottom: 24px;
}

.step-body {
  min-height: 280px;
}

.step-pane {
  padding: 8px 0;
}

.add-join {
  margin-bottom: 12px;
}

.sql-label {
  font-weight: 600;
  margin-bottom: 8px;
}

.sql-preview {
  margin: 0;
  padding: 12px;
  background: #0d1117;
  color: #c9d1d9;
  border-radius: 4px;
  font-size: 12px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
}

.step-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 24px;
  padding-top: 16px;
  border-top: 1px solid #f0f0f0;
}
</style>

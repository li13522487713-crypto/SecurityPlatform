<template>
  <div class="migration-preview-panel">
    <a-steps v-model:current="currentPhase" size="small" class="phase-steps">
      <a-step :title="t('dynamic.migrationPreviewPanel.phaseExpand')" />
      <a-step :title="t('dynamic.migrationPreviewPanel.phaseMigrate')" />
      <a-step :title="t('dynamic.migrationPreviewPanel.phaseContract')" />
    </a-steps>
    <a-alert
      v-if="hasRisk"
      type="warning"
      show-icon
      class="risk-alert"
      :message="t('dynamic.migrationPreviewPanel.riskTitle')"
      :description="t('dynamic.migrationPreviewPanel.riskDesc')"
    />
    <a-collapse v-model:active-key="openKeys" :bordered="false">
      <a-collapse-panel key="expand" :header="t('dynamic.migrationPreviewPanel.ddlExpand')">
        <pre class="sql-block">{{ ddlExpandText }}</pre>
      </a-collapse-panel>
      <a-collapse-panel key="migrate" :header="t('dynamic.migrationPreviewPanel.ddlMigrate')">
        <pre class="sql-block">{{ ddlMigrateText }}</pre>
      </a-collapse-panel>
      <a-collapse-panel key="contract" :header="t('dynamic.migrationPreviewPanel.ddlContract')">
        <pre class="sql-block">{{ ddlContractText }}</pre>
      </a-collapse-panel>
    </a-collapse>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";

const props = withDefaults(
  defineProps<{
    hasRisk?: boolean;
    ddlExpand?: string;
    ddlMigrate?: string;
    ddlContract?: string;
  }>(),
  {
    hasRisk: true,
    ddlExpand: "-- EXPAND: ADD COLUMN example VARCHAR(64) NULL;",
    ddlMigrate: "-- MIGRATE: BACKFILL / INDEX BUILD (batch)",
    ddlContract: "-- CONTRACT: ALTER COLUMN ... NOT NULL;"
  }
);

const { t } = useI18n();
const currentPhase = ref(0);
const openKeys = ref(["expand", "migrate", "contract"]);

const ddlExpandText = computed(() => props.ddlExpand);
const ddlMigrateText = computed(() => props.ddlMigrate);
const ddlContractText = computed(() => props.ddlContract);
</script>

<style scoped>
.migration-preview-panel {
  padding: 8px 0;
}

.phase-steps {
  margin-bottom: 16px;
}

.risk-alert {
  margin-bottom: 12px;
}

.sql-block {
  margin: 0;
  padding: 12px;
  font-size: 12px;
  line-height: 1.5;
  background: #0d1117;
  color: #c9d1d9;
  border-radius: 4px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
}
</style>

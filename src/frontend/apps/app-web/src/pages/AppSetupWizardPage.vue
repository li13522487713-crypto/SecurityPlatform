<template>
  <div class="app-setup-container">
    <div class="app-setup-card">
      <h1 class="setup-title">{{ t("setup.appSetupTitle") }}</h1>
      <p class="setup-subtitle">{{ t("setup.appSetupSubtitle") }}</p>

      <a-steps :current="currentStep" class="setup-steps">
        <a-step :title="t('setup.stepDatabase')" />
        <a-step :title="t('setup.stepAppInfo')" />
        <a-step :title="t('setup.stepComplete')" />
      </a-steps>

      <div v-if="currentStep === 0" class="step-database">
        <a-form :label-col="{ span: 6 }" :wrapper-col="{ span: 16 }">
          <a-form-item :label="t('setup.databaseDriver')">
            <a-select v-model:value="dbForm.driverCode" data-testid="app-setup-driver" @change="onDriverChange">
              <a-select-option v-for="driver in drivers" :key="driver.code" :value="driver.code">
                {{ driver.displayName }}
              </a-select-option>
            </a-select>
          </a-form-item>

          <a-form-item :label="t('setup.connectionMode')">
            <a-radio-group v-model:value="dbForm.mode" data-testid="app-setup-mode">
              <a-radio-button value="raw">{{ t("setup.modeRaw") }}</a-radio-button>
              <a-radio-button v-if="selectedDriver?.supportsVisual" value="visual">
                {{ t("setup.modeVisual") }}
              </a-radio-button>
            </a-radio-group>
          </a-form-item>

          <template v-if="dbForm.mode === 'raw'">
            <a-form-item :label="t('setup.connectionString')">
              <a-input
                v-model:value="dbForm.connectionString"
                data-testid="app-setup-connection-string"
                :placeholder="selectedDriver?.connectionStringExample ?? ''"
              />
            </a-form-item>
          </template>

          <template v-if="dbForm.mode === 'visual' && selectedDriver">
            <a-form-item
              v-for="field in selectedDriver.fields"
              :key="field.code"
              :label="field.label"
              :required="field.required"
            >
              <a-input-password
                v-if="field.secret"
                v-model:value="dbForm.visualConfig[field.code]"
                :data-testid="`app-setup-visual-${field.code}`"
                :placeholder="field.placeholder ?? ''"
              />
              <a-textarea
                v-else-if="field.multiline"
                v-model:value="dbForm.visualConfig[field.code]"
                :data-testid="`app-setup-visual-${field.code}`"
                :placeholder="field.placeholder ?? ''"
                :rows="3"
              />
              <a-input
                v-else
                v-model:value="dbForm.visualConfig[field.code]"
                :data-testid="`app-setup-visual-${field.code}`"
                :placeholder="field.placeholder ?? field.defaultValue ?? ''"
              />
            </a-form-item>
          </template>

          <a-form-item :wrapper-col="{ offset: 6, span: 16 }">
            <a-space>
              <a-button data-testid="app-setup-test-connection" :loading="testingConnection" @click="handleTestConnection">
                {{ testingConnection ? t("setup.testing") : t("setup.testConnection") }}
              </a-button>
              <a-tag
                v-if="connectionTestResult !== null"
                data-testid="app-setup-test-result"
                :color="connectionTestResult ? 'success' : 'error'"
              >
                {{ connectionTestResult ? t("setup.testSuccess") : connectionTestMessage }}
              </a-tag>
            </a-space>
          </a-form-item>
        </a-form>

        <div class="step-actions">
          <span />
          <a-button
            data-testid="app-setup-next-step"
            type="primary"
            :disabled="!connectionTestResult"
            @click="currentStep = 1"
          >
            {{ t("setup.next") }}
          </a-button>
        </div>
      </div>

      <div v-if="currentStep === 1 && !completed && !setupError" class="step-app-info">
        <a-form :label-col="{ span: 6 }" :wrapper-col="{ span: 16 }">
          <a-form-item :label="t('setup.appName')" required>
            <a-input
              v-model:value="form.appName"
              data-testid="app-setup-name"
              :placeholder="t('setup.appNamePlaceholder')"
            />
          </a-form-item>
          <a-form-item :label="t('setup.adminUsername')" required>
            <a-input
              v-model:value="form.adminUsername"
              data-testid="app-setup-admin-username"
              :placeholder="t('setup.adminUsernamePlaceholder')"
            />
          </a-form-item>
        </a-form>

        <div class="step-actions">
          <a-button data-testid="app-setup-prev-step" @click="currentStep = 0">{{ t("setup.prev") }}</a-button>
          <a-button
            data-testid="app-setup-initialize"
            type="primary"
            size="large"
            :loading="initializing"
            :disabled="!formValid"
            @click="handleInitialize"
          >
            {{ initializing ? t("setup.initializing") : t("setup.startSetup") }}
          </a-button>
        </div>
      </div>

      <a-result
        v-if="completed"
        data-testid="app-setup-success"
        status="success"
        :title="t('setup.appSetupComplete')"
        :sub-title="t('setup.appSetupCompleteDesc')"
      >
        <template #extra>
          <div class="setup-report">
            <a-descriptions v-if="initReport" bordered :column="1" size="small" style="margin-bottom: 16px">
              <a-descriptions-item :label="t('setup.platformStatus')">
                <span data-testid="app-setup-report-platform-status">{{ initReport.platformStatus }}</span>
              </a-descriptions-item>
              <a-descriptions-item :label="t('setup.appStatus')">
                <span data-testid="app-setup-report-app-status">{{ initReport.appStatus }}</span>
              </a-descriptions-item>
              <a-descriptions-item :label="t('setup.appSetupCompleted')">
                <span data-testid="app-setup-report-app-completed">
                  {{ formatBooleanFlag(initReport.appSetupCompleted) }}
                </span>
              </a-descriptions-item>
              <a-descriptions-item :label="t('setup.dbConnected')">
                <a-tag :color="initReport.databaseConnected ? 'green' : 'red'">
                  {{ initReport.databaseConnected ? t('common.success') : t('common.error') }}
                </a-tag>
                <span class="report-flag" data-testid="app-setup-report-db-connected">
                  {{ formatBooleanFlag(initReport.databaseConnected) }}
                </span>
              </a-descriptions-item>
              <a-descriptions-item :label="t('setup.coreTablesVerified')">
                <a-tag :color="initReport.coreTablesVerified ? 'green' : 'red'">
                  {{ initReport.coreTablesVerified ? t('common.success') : t('common.error') }}
                </a-tag>
                <span class="report-flag" data-testid="app-setup-report-core-tables">
                  {{ formatBooleanFlag(initReport.coreTablesVerified) }}
                </span>
              </a-descriptions-item>
            </a-descriptions>
            <a-alert
              :message="t('setup.restartRequired')"
              :description="t('setup.restartRequiredDesc')"
              type="warning"
              show-icon
              style="margin-bottom: 16px"
            />
            <a-button data-testid="app-setup-enter-workspace" type="primary" size="large" @click="enterWorkspace">
              {{ t("setup.enterWorkspace") }}
            </a-button>
          </div>
        </template>
      </a-result>

      <a-result
        v-if="setupError"
        data-testid="app-setup-failed"
        status="error"
        :title="t('setup.appSetupFailed')"
        :sub-title="setupError"
      >
        <template #extra>
          <a-button type="primary" @click="retrySetup">{{ t("setup.retry") }}</a-button>
        </template>
      </a-result>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import {
  getDrivers,
  initializeApp,
  testConnection,
  type AppSetupInitializeResponse,
  type DriverDefinition
} from "@/services/api-setup";

const { t } = useI18n();

type SetupDbForm = {
  driverCode: string;
  mode: string;
  connectionString: string;
  visualConfig: Record<string, string>;
};

const currentStep = ref(0);
const drivers = ref<DriverDefinition[]>([]);
const testingConnection = ref(false);
const connectionTestResult = ref<boolean | null>(null);
const connectionTestMessage = ref("");

const dbForm = ref<SetupDbForm>({
  driverCode: "SQLite",
  mode: "raw",
  connectionString: "Data Source=atlas.db",
  visualConfig: {}
});

const form = ref({
  appName: "",
  adminUsername: "admin"
});

const initializing = ref(false);
const completed = ref(false);
const setupError = ref<string | null>(null);
const initReport = ref<AppSetupInitializeResponse | null>(null);

const selectedDriver = computed(() => drivers.value.find((driver) => driver.code === dbForm.value.driverCode));

const formValid = computed(
  () => form.value.appName.trim() !== "" && form.value.adminUsername.trim() !== "" && !initializing.value
);

onMounted(async () => {
  try {
    const resp = await getDrivers();
    if (resp.success && resp.data) {
      drivers.value = resp.data;
    }
  } catch (error) {
    console.error("Failed to load drivers", error);
  }
});

function onDriverChange() {
  connectionTestResult.value = null;
  connectionTestMessage.value = "";
  dbForm.value.visualConfig = {};
  const driver = selectedDriver.value;
  if (!driver) {
    return;
  }

  dbForm.value.connectionString = driver.connectionStringExample;
  if (driver.supportsVisual) {
    for (const field of driver.fields) {
      if (field.defaultValue) {
        dbForm.value.visualConfig = {
          ...dbForm.value.visualConfig,
          [field.code]: field.defaultValue
        };
      }
    }
  } else {
    dbForm.value.mode = "raw";
  }
}

async function handleTestConnection() {
  testingConnection.value = true;
  connectionTestResult.value = null;
  try {
    const resp = await testConnection({
      driverCode: dbForm.value.driverCode,
      mode: dbForm.value.mode,
      connectionString: dbForm.value.mode === "raw" ? dbForm.value.connectionString : undefined,
      visualConfig: dbForm.value.mode === "visual" ? dbForm.value.visualConfig : undefined
    });
    if (resp.success && resp.data) {
      connectionTestResult.value = resp.data.connected;
      connectionTestMessage.value = resp.data.message;
    }
  } catch (error: unknown) {
    connectionTestResult.value = false;
    connectionTestMessage.value = error instanceof Error ? error.message : String(error);
  } finally {
    testingConnection.value = false;
  }
}

async function handleInitialize() {
  initializing.value = true;
  setupError.value = null;
  try {
    const resp = await initializeApp({
      database: {
        driverCode: dbForm.value.driverCode,
        mode: dbForm.value.mode,
        connectionString: dbForm.value.mode === "raw" ? dbForm.value.connectionString : undefined,
        visualConfig: dbForm.value.mode === "visual" ? dbForm.value.visualConfig : undefined
      },
      appName: form.value.appName,
      adminUsername: form.value.adminUsername
    });
    if (resp.success) {
      initReport.value = resp.data ?? null;
      completed.value = true;
      currentStep.value = 2;
    } else {
      setupError.value = resp.message || t("setup.appSetupFailed");
      currentStep.value = 2;
    }
  } catch (error: unknown) {
    setupError.value = error instanceof Error ? error.message : String(error);
    currentStep.value = 2;
  } finally {
    initializing.value = false;
  }
}

function retrySetup() {
  setupError.value = null;
  completed.value = false;
  currentStep.value = 0;
}

function enterWorkspace() {
  window.location.href = "/";
}

function formatBooleanFlag(value: boolean): string {
  return value ? "true" : "false";
}
</script>

<style scoped>
.app-setup-container {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #43e97b 0%, #38f9d7 100%);
  padding: 24px;
}

.app-setup-card {
  background: #fff;
  border-radius: 12px;
  padding: 48px;
  max-width: 760px;
  width: 100%;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.12);
}

.setup-title {
  text-align: center;
  font-size: 24px;
  font-weight: 600;
  margin-bottom: 8px;
  color: #1a1a2e;
}

.setup-subtitle {
  text-align: center;
  color: #666;
  margin-bottom: 32px;
}

.setup-steps {
  margin-bottom: 32px;
}

.step-actions {
  display: flex;
  justify-content: space-between;
  margin-top: 24px;
  padding-top: 16px;
  border-top: 1px solid #f0f0f0;
}

.setup-report {
  text-align: left;
}

.report-flag {
  margin-left: 8px;
  color: #666;
  font-size: 13px;
}
</style>

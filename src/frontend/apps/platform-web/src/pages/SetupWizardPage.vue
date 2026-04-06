<template>
  <div class="setup-wizard-container">
    <div class="setup-wizard-card">
      <h1 class="setup-title">{{ t("setup.title") }}</h1>
      <p class="setup-subtitle">{{ t("setup.subtitle") }}</p>

      <a-steps :current="currentStep" class="setup-steps">
        <a-step :title="t('setup.stepWelcome')" />
        <a-step :title="t('setup.stepDatabase')" />
        <a-step :title="t('setup.stepAdmin')" />
        <a-step :title="t('setup.stepComplete')" />
      </a-steps>

      <div class="step-content">
        <!-- Step 0: Welcome -->
        <div v-if="currentStep === 0" class="step-welcome">
          <a-result status="info" :title="t('setup.welcomeTitle')" :sub-title="t('setup.welcomeDesc')">
            <template #extra>
              <a-button type="primary" size="large" @click="currentStep = 1">
                {{ t("setup.startSetup") }}
              </a-button>
            </template>
          </a-result>
        </div>

        <!-- Step 1: Database -->
        <div v-if="currentStep === 1" class="step-database">
          <a-form :label-col="{ span: 6 }" :wrapper-col="{ span: 16 }">
            <a-form-item :label="t('setup.databaseDriver')">
              <a-select v-model:value="dbForm.driverCode" @change="onDriverChange">
                <a-select-option v-for="d in drivers" :key="d.code" :value="d.code">
                  {{ d.displayName }}
                </a-select-option>
              </a-select>
            </a-form-item>

            <a-form-item :label="t('setup.connectionMode')">
              <a-radio-group v-model:value="dbForm.mode">
                <a-radio-button value="raw">{{ t("setup.modeRaw") }}</a-radio-button>
                <a-radio-button v-if="selectedDriver?.supportsVisual" value="visual">
                  {{ t("setup.modeVisual") }}
                </a-radio-button>
              </a-radio-group>
            </a-form-item>

            <template v-if="dbForm.mode === 'raw'">
              <a-form-item :label="t('setup.connectionString')">
                <a-input v-model:value="dbForm.connectionString"
                  :placeholder="selectedDriver?.connectionStringExample ?? ''" />
              </a-form-item>
            </template>

            <template v-if="dbForm.mode === 'visual' && selectedDriver">
              <a-form-item v-for="field in selectedDriver.fields" :key="field.code" :label="field.label"
                :required="field.required">
                <a-input-password v-if="field.secret" v-model:value="dbForm.visualConfig[field.code]"
                  :placeholder="field.placeholder ?? ''" />
                <a-textarea v-else-if="field.multiline" v-model:value="dbForm.visualConfig[field.code]"
                  :placeholder="field.placeholder ?? ''" :rows="3" />
                <a-input v-else v-model:value="dbForm.visualConfig[field.code]"
                  :placeholder="field.placeholder ?? field.defaultValue ?? ''" />
              </a-form-item>
            </template>

            <a-form-item :wrapper-col="{ offset: 6, span: 16 }">
              <a-space>
                <a-button :loading="testingConnection" @click="handleTestConnection">
                  {{ testingConnection ? t("setup.testing") : t("setup.testConnection") }}
                </a-button>
                <a-tag v-if="connectionTestResult !== null" :color="connectionTestResult ? 'success' : 'error'">
                  {{ connectionTestResult ? t("setup.testSuccess") : connectionTestMessage }}
                </a-tag>
              </a-space>
            </a-form-item>
          </a-form>

          <div class="step-actions">
            <a-button @click="currentStep = 0">{{ t("setup.prev") }}</a-button>
            <a-button type="primary" :disabled="!connectionTestResult" @click="currentStep = 2">
              {{ t("setup.next") }}
            </a-button>
          </div>
        </div>

        <!-- Step 2: Admin Account -->
        <div v-if="currentStep === 2" class="step-admin">
          <a-form :label-col="{ span: 6 }" :wrapper-col="{ span: 16 }">
            <a-form-item :label="t('setup.tenantId')">
              <a-input v-model:value="adminForm.tenantId" />
              <div class="field-hint">{{ t("setup.tenantIdHint") }}</div>
            </a-form-item>
            <a-form-item :label="t('setup.adminUsername')" required>
              <a-input v-model:value="adminForm.username" />
            </a-form-item>
            <a-form-item :label="t('setup.adminPassword')" required>
              <a-input-password v-model:value="adminForm.password" />
            </a-form-item>
            <a-form-item :label="t('setup.adminPasswordConfirm')" required>
              <a-input-password v-model:value="adminForm.passwordConfirm" />
              <div v-if="passwordMismatch" class="field-error">{{ t("setup.passwordMismatch") }}</div>
            </a-form-item>
          </a-form>

          <div class="step-actions">
            <a-button @click="currentStep = 1">{{ t("setup.prev") }}</a-button>
            <a-button type="primary" :disabled="!adminFormValid" :loading="initializing"
              @click="handleInitialize">
              {{ initializing ? t("setup.initializing") : t("setup.next") }}
            </a-button>
          </div>
        </div>

        <!-- Step 3: Complete -->
        <div v-if="currentStep === 3" class="step-complete">
          <a-result v-if="!initError" status="success" :title="t('setup.completeTitle')"
            :sub-title="t('setup.completeDesc')">
            <template #extra>
              <div v-if="bootstrapReport" class="bootstrap-report">
                <a-descriptions bordered :column="1" size="small" class="report-descriptions">
                  <a-descriptions-item :label="t('setup.reportSchema')">
                    <a-tag :color="bootstrapReport.schemaInitialized ? 'success' : 'default'">
                      {{ bootstrapReport.schemaInitialized ? t('setup.reportDone') : t('setup.reportSkipped') }}
                    </a-tag>
                  </a-descriptions-item>
                  <a-descriptions-item :label="t('setup.reportMigrations')">
                    <a-tag :color="bootstrapReport.migrationsApplied ? 'success' : 'default'">
                      {{ bootstrapReport.migrationsApplied ? t('setup.reportDone') : t('setup.reportSkipped') }}
                    </a-tag>
                  </a-descriptions-item>
                  <a-descriptions-item :label="t('setup.reportSeed')">
                    <a-tag :color="bootstrapReport.seedCompleted ? 'success' : 'default'">
                      {{ bootstrapReport.seedCompleted ? t('setup.reportDone') : t('setup.reportSkipped') }}
                    </a-tag>
                    <span v-if="bootstrapReport.seedSummary" class="seed-summary">
                      {{ bootstrapReport.seedSummary }}
                    </span>
                  </a-descriptions-item>
                  <a-descriptions-item :label="t('setup.reportAdmin')">
                    <a-tag :color="bootstrapReport.adminCreated ? 'success' : 'warning'">
                      {{ bootstrapReport.adminCreated ? t('setup.reportDone') : t('setup.reportSkipped') }}
                    </a-tag>
                    <span v-if="bootstrapReport.adminUsername" class="admin-name">
                      {{ bootstrapReport.adminUsername }}
                    </span>
                  </a-descriptions-item>
                </a-descriptions>
                <div v-if="bootstrapReport.errors.length > 0" class="report-errors">
                  <a-alert type="warning" :message="t('setup.reportErrors')">
                    <template #description>
                      <ul>
                        <li v-for="(err, idx) in bootstrapReport.errors" :key="idx">{{ err }}</li>
                      </ul>
                    </template>
                  </a-alert>
                </div>
              </div>
              <a-button type="primary" size="large" style="margin-top: 16px" @click="goToLogin">
                {{ t("setup.goToLogin") }}
              </a-button>
            </template>
          </a-result>
          <a-result v-else status="error" :title="t('setup.initFailed')" :sub-title="initError">
            <template #extra>
              <a-button type="primary" @click="currentStep = 1">{{ t("setup.prev") }}</a-button>
            </template>
          </a-result>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import {
  getDrivers,
  testConnection,
  initializePlatform,
  type DriverDefinition,
  type InitializeResponse
} from "@/services/api-setup";
import { markSetupComplete } from "@/router";

const { t } = useI18n();
const router = useRouter();

const currentStep = ref(0);
const drivers = ref<DriverDefinition[]>([]);
const testingConnection = ref(false);
const connectionTestResult = ref<boolean | null>(null);
const connectionTestMessage = ref("");
const initializing = ref(false);
const initError = ref<string | null>(null);
const bootstrapReport = ref<InitializeResponse | null>(null);

const dbForm = ref({
  driverCode: "SQLite",
  mode: "raw",
  connectionString: "Data Source=atlas.db",
  visualConfig: {} as Record<string, string>
});

const adminForm = ref({
  tenantId: "00000000-0000-0000-0000-000000000001",
  username: "admin",
  password: "",
  passwordConfirm: ""
});

const selectedDriver = computed(() =>
  drivers.value.find((d) => d.code === dbForm.value.driverCode)
);

const passwordMismatch = computed(
  () => adminForm.value.password !== "" &&
    adminForm.value.passwordConfirm !== "" &&
    adminForm.value.password !== adminForm.value.passwordConfirm
);

const adminFormValid = computed(
  () => adminForm.value.username.trim() !== "" &&
    adminForm.value.password.trim() !== "" &&
    adminForm.value.password === adminForm.value.passwordConfirm &&
    !initializing.value
);

onMounted(async () => {
  try {
    const resp = await getDrivers();
    if (resp.success && resp.data) {
      drivers.value = resp.data;
    }
  } catch (e) {
    console.error("Failed to load drivers", e);
  }
});

function onDriverChange() {
  connectionTestResult.value = null;
  connectionTestMessage.value = "";
  dbForm.value.visualConfig = {};
  const driver = selectedDriver.value;
  if (driver) {
    dbForm.value.connectionString = driver.connectionStringExample;
    if (driver.supportsVisual) {
      for (const field of driver.fields) {
        if (field.defaultValue) {
          dbForm.value.visualConfig[field.code] = field.defaultValue;
        }
      }
    } else {
      dbForm.value.mode = "raw";
    }
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
  } catch (e: unknown) {
    connectionTestResult.value = false;
    connectionTestMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    testingConnection.value = false;
  }
}

async function handleInitialize() {
  initializing.value = true;
  initError.value = null;
  try {
    const resp = await initializePlatform({
      database: {
        driverCode: dbForm.value.driverCode,
        mode: dbForm.value.mode,
        connectionString: dbForm.value.mode === "raw" ? dbForm.value.connectionString : undefined,
        visualConfig: dbForm.value.mode === "visual" ? dbForm.value.visualConfig : undefined
      },
      admin: {
        tenantId: adminForm.value.tenantId || undefined,
        username: adminForm.value.username,
        password: adminForm.value.password
      }
    });
    if (resp.success && resp.data) {
      bootstrapReport.value = resp.data;
      currentStep.value = 3;
    } else {
      initError.value = resp.message || t("setup.initFailed");
      currentStep.value = 3;
    }
  } catch (e: unknown) {
    initError.value = e instanceof Error ? e.message : String(e);
    currentStep.value = 3;
  } finally {
    initializing.value = false;
  }
}

function goToLogin() {
  markSetupComplete();
  router.push("/login");
}
</script>

<style scoped>
.setup-wizard-container {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  padding: 24px;
}

.setup-wizard-card {
  background: #fff;
  border-radius: 12px;
  padding: 48px;
  max-width: 800px;
  width: 100%;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.12);
}

.setup-title {
  text-align: center;
  font-size: 28px;
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
  margin-bottom: 40px;
}

.step-content {
  min-height: 300px;
}

.step-actions {
  display: flex;
  justify-content: space-between;
  margin-top: 24px;
  padding-top: 16px;
  border-top: 1px solid #f0f0f0;
}

.field-hint {
  color: #999;
  font-size: 12px;
  margin-top: 4px;
}

.field-error {
  color: #ff4d4f;
  font-size: 12px;
  margin-top: 4px;
}

.bootstrap-report {
  text-align: left;
  margin-bottom: 8px;
}

.report-descriptions {
  margin-bottom: 12px;
}

.seed-summary,
.admin-name {
  margin-left: 8px;
  color: #666;
  font-size: 13px;
}

.report-errors {
  margin-top: 12px;
}

.report-errors ul {
  margin: 0;
  padding-left: 20px;
}
</style>

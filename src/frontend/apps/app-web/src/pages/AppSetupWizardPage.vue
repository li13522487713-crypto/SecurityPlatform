<template>
  <div class="app-setup-container">
    <div class="locale-switch-wrapper">
      <LocaleSwitch />
    </div>
    <div class="app-setup-card">
      <h1 class="setup-title">{{ t("setup.appSetupTitle") }}</h1>
      <p class="setup-subtitle">{{ t("setup.appSetupSubtitle") }}</p>

      <a-steps :current="currentStep" class="setup-steps">
        <a-step :title="t('setup.stepDatabase')" />
        <a-step :title="t('setup.stepAdmin')" />
        <a-step :title="t('setup.stepRoles')" />
        <a-step :title="t('setup.stepOrganization')" />
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

      <div v-if="currentStep === 1" class="step-admin">
        <a-form :label-col="{ span: 6 }" :wrapper-col="{ span: 16 }">
          <a-form-item :label="t('setup.appName')" required>
            <a-input
              v-model:value="adminForm.appName"
              data-testid="app-setup-name"
              :placeholder="t('setup.appNamePlaceholder')"
            />
          </a-form-item>
          <a-form-item :label="t('setup.adminUsername')" required>
            <a-input
              v-model:value="adminForm.adminUsername"
              data-testid="app-setup-admin-username"
              :placeholder="t('setup.adminUsernamePlaceholder')"
            />
          </a-form-item>
          <a-form-item :label="t('setup.appKey')" required>
            <a-input
              v-model:value="adminForm.appKey"
              data-testid="app-setup-app-key"
              :placeholder="t('setup.appKeyPlaceholder')"
            />
          </a-form-item>
        </a-form>

        <div class="step-actions">
          <a-button data-testid="app-setup-prev-step" @click="currentStep = 0">{{ t("setup.prev") }}</a-button>
          <a-button
            data-testid="app-setup-next-to-roles"
            type="primary"
            :disabled="!adminFormValid"
            @click="currentStep = 2"
          >
            {{ t("setup.next") }}
          </a-button>
        </div>
      </div>

      <div v-if="currentStep === 2" class="step-roles">
        <a-alert type="info" show-icon :message="t('setup.requiredRolesHint')" style="margin-bottom: 16px" />
        <div class="required-role-list">
          <a-tag data-testid="app-setup-role-required-app-admin" color="processing">AppAdmin</a-tag>
          <a-tag data-testid="app-setup-role-required-app-member" color="processing">AppMember</a-tag>
        </div>

        <div class="optional-role-block">
          <div class="section-title">{{ t("setup.optionalRolesTitle") }}</div>
          <div class="field-hint">{{ t("setup.optionalRolesDesc") }}</div>
          <a-checkbox-group v-model:value="rolesForm.selectedRoleCodes">
            <div class="role-grid">
              <label v-for="role in optionalRoleTemplates" :key="role.code" class="role-card">
                <a-checkbox :value="role.code" :data-testid="`app-setup-role-${role.code}`">
                  {{ t(role.labelKey) }}
                </a-checkbox>
                <div class="field-hint">{{ t(role.descKey) }}</div>
              </label>
            </div>
          </a-checkbox-group>
        </div>

        <div class="step-actions">
          <a-button data-testid="app-setup-back-to-admin" @click="currentStep = 1">{{ t("setup.prev") }}</a-button>
          <a-button data-testid="app-setup-next-to-org" type="primary" @click="currentStep = 3">
            {{ t("setup.next") }}
          </a-button>
        </div>
      </div>

      <div v-if="currentStep === 3" class="step-organization">
        <div class="org-section">
          <div class="section-header">
            <div>
              <div class="section-title">{{ t("setup.departmentSectionTitle") }}</div>
              <div class="field-hint">{{ t("setup.departmentSectionDesc") }}</div>
            </div>
            <a-button data-testid="app-setup-add-department" @click="addDepartment">
              {{ t("setup.addDepartment") }}
            </a-button>
          </div>

          <div v-for="(department, index) in organizationForm.departments" :key="`department-${index}`" class="config-row">
            <a-input
              v-model:value="department.name"
              :data-testid="`app-setup-department-name-${index}`"
              :placeholder="t('setup.departmentNamePlaceholder')"
            />
            <a-input
              v-model:value="department.code"
              :data-testid="`app-setup-department-code-${index}`"
              :placeholder="t('setup.departmentCodePlaceholder')"
            />
            <a-input
              v-model:value="department.parentCode"
              :data-testid="`app-setup-department-parent-${index}`"
              :placeholder="t('setup.departmentParentPlaceholder')"
            />
            <a-input-number
              v-model:value="department.sortOrder"
              :min="0"
              :controls="false"
              :data-testid="`app-setup-department-sort-${index}`"
            />
            <a-button
              v-if="organizationForm.departments.length > 1"
              danger
              :data-testid="`app-setup-remove-department-${index}`"
              @click="removeDepartment(index)"
            >
              {{ t("setup.removeRow") }}
            </a-button>
          </div>
        </div>

        <div class="org-section">
          <div class="section-header">
            <div>
              <div class="section-title">{{ t("setup.positionSectionTitle") }}</div>
              <div class="field-hint">{{ t("setup.positionSectionDesc") }}</div>
            </div>
            <a-button data-testid="app-setup-add-position" @click="addPosition">
              {{ t("setup.addPosition") }}
            </a-button>
          </div>

          <div v-for="(position, index) in organizationForm.positions" :key="`position-${index}`" class="config-row">
            <a-input
              v-model:value="position.name"
              :data-testid="`app-setup-position-name-${index}`"
              :placeholder="t('setup.positionNamePlaceholder')"
            />
            <a-input
              v-model:value="position.code"
              :data-testid="`app-setup-position-code-${index}`"
              :placeholder="t('setup.positionCodePlaceholder')"
            />
            <a-input
              v-model:value="position.description"
              :data-testid="`app-setup-position-description-${index}`"
              :placeholder="t('setup.positionDescriptionPlaceholder')"
            />
            <a-input-number
              v-model:value="position.sortOrder"
              :min="0"
              :controls="false"
              :data-testid="`app-setup-position-sort-${index}`"
            />
            <a-button
              v-if="organizationForm.positions.length > 1"
              danger
              :data-testid="`app-setup-remove-position-${index}`"
              @click="removePosition(index)"
            >
              {{ t("setup.removeRow") }}
            </a-button>
          </div>
        </div>

        <div class="step-actions">
          <a-button data-testid="app-setup-back-to-roles" @click="currentStep = 2">{{ t("setup.prev") }}</a-button>
          <a-button
            data-testid="app-setup-initialize"
            type="primary"
            :disabled="!organizationFormValid"
            :loading="initializing"
            @click="handleInitialize"
          >
            {{ initializing ? t("setup.initializing") : t("setup.startInitialization") }}
          </a-button>
        </div>
      </div>

      <a-result
        v-if="currentStep === 4 && completed"
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
                <span data-testid="app-setup-report-app-completed">{{ formatBooleanFlag(initReport.appSetupCompleted) }}</span>
              </a-descriptions-item>
              <a-descriptions-item :label="t('setup.dbConnected')">
                <span data-testid="app-setup-report-db-connected">{{ formatBooleanFlag(initReport.databaseConnected) }}</span>
              </a-descriptions-item>
              <a-descriptions-item :label="t('setup.coreTablesVerified')">
                <span data-testid="app-setup-report-core-tables">{{ formatBooleanFlag(initReport.coreTablesVerified) }}</span>
              </a-descriptions-item>
              <a-descriptions-item :label="t('setup.reportRoles')">
                <span data-testid="app-setup-report-roles-created">{{ initReport.rolesCreated }}</span>
              </a-descriptions-item>
              <a-descriptions-item :label="t('setup.reportDepartments')">
                <span data-testid="app-setup-report-departments-created">{{ initReport.departmentsCreated }}</span>
              </a-descriptions-item>
              <a-descriptions-item :label="t('setup.reportPositions')">
                <span data-testid="app-setup-report-positions-created">{{ initReport.positionsCreated }}</span>
              </a-descriptions-item>
              <a-descriptions-item :label="t('setup.reportAdmin')">
                <span data-testid="app-setup-report-admin-bound">{{ formatBooleanFlag(initReport.adminBound) }}</span>
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
              {{ t("setup.goToLogin") }}
            </a-button>
          </div>
        </template>
      </a-result>

      <a-result
        v-if="currentStep === 4 && setupError"
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
import { useRouter } from "vue-router";
import {
  getDrivers,
  getSetupState,
  initializeApp,
  testConnection,
  type AppSetupDepartmentConfig,
  type AppSetupInitializeResponse,
  type AppSetupPositionConfig,
  type DriverDefinition
} from "@/services/api-setup";
import { markAppSetupComplete } from "@/router";
import LocaleSwitch from "@/components/layout/LocaleSwitch.vue";

const router = useRouter();

type OptionalRoleTemplate = {
  code: string;
  labelKey: string;
  descKey: string;
};

const optionalRoleTemplates: OptionalRoleTemplate[] = [
  { code: "SecurityAdmin", labelKey: "setup.roleSecurityAdmin", descKey: "setup.roleSecurityAdminDesc" },
  { code: "AuditAdmin", labelKey: "setup.roleAuditAdmin", descKey: "setup.roleAuditAdminDesc" },
  { code: "AssetAdmin", labelKey: "setup.roleAssetAdmin", descKey: "setup.roleAssetAdminDesc" },
  { code: "ApprovalAdmin", labelKey: "setup.roleApprovalAdmin", descKey: "setup.roleApprovalAdminDesc" }
];

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
const configuredAppKey = ref("");

const dbForm = ref<SetupDbForm>({
  driverCode: "SQLite",
  mode: "raw",
  connectionString: "Data Source=atlas.db",
  visualConfig: {}
});

const adminForm = ref({
  appName: "",
  adminUsername: "admin",
  appKey: ""
});

const rolesForm = ref({
  selectedRoleCodes: [] as string[]
});

const organizationForm = ref({
  departments: buildDefaultDepartments(),
  positions: buildDefaultPositions()
});

const initializing = ref(false);
const completed = ref(false);
const setupError = ref<string | null>(null);
const initReport = ref<AppSetupInitializeResponse | null>(null);

const selectedDriver = computed(() => drivers.value.find((driver) => driver.code === dbForm.value.driverCode));
const adminFormValid = computed(
  () =>
    adminForm.value.appName.trim() !== "" &&
    adminForm.value.adminUsername.trim() !== "" &&
    adminForm.value.appKey.trim() !== "" &&
    !initializing.value
);
const organizationFormValid = computed(() => {
  const departmentsValid =
    organizationForm.value.departments.length > 0 &&
    organizationForm.value.departments.every(
      (department) => department.name.trim() !== "" && (department.code?.trim() ?? "") !== ""
    );
  const positionsValid =
    organizationForm.value.positions.length > 0 &&
    organizationForm.value.positions.every((position) => position.name.trim() !== "" && position.code.trim() !== "");

  return departmentsValid && positionsValid && !initializing.value;
});

onMounted(async () => {
  try {
    const [driversResp, stateResp] = await Promise.all([getDrivers(), getSetupState()]);

    if (driversResp.success && driversResp.data) {
      drivers.value = driversResp.data;
    }

    const resolvedConfiguredAppKey = stateResp.data?.configuredAppKey?.trim() ?? "";
    configuredAppKey.value = resolvedConfiguredAppKey;

    if (!adminForm.value.appKey.trim()) {
      adminForm.value.appKey = resolvedConfiguredAppKey;
    }
  } catch (error) {
    console.error("Failed to load setup bootstrap data", error);
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
        dbForm.value.visualConfig[field.code] = field.defaultValue;
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
      admin: {
        appName: adminForm.value.appName.trim(),
        adminUsername: adminForm.value.adminUsername.trim(),
        appKey: adminForm.value.appKey.trim() || undefined
      },
      roles: {
        selectedRoleCodes: rolesForm.value.selectedRoleCodes
      },
      organization: {
        departments: organizationForm.value.departments.map((department) => ({
          name: department.name.trim(),
          code: department.code?.trim() || undefined,
          parentCode: department.parentCode?.trim() || undefined,
          sortOrder: department.sortOrder ?? 0
        })),
        positions: organizationForm.value.positions.map((position) => ({
          name: position.name.trim(),
          code: position.code.trim(),
          description: position.description?.trim() || undefined,
          sortOrder: position.sortOrder ?? 0
        }))
      }
    });
    if (resp.success) {
      initReport.value = resp.data ?? null;
      completed.value = true;
      currentStep.value = 4;
    } else {
      setupError.value = resp.message || t("setup.appSetupFailed");
      currentStep.value = 4;
    }
  } catch (error: unknown) {
    setupError.value = error instanceof Error ? error.message : String(error);
    currentStep.value = 4;
  } finally {
    initializing.value = false;
  }
}

function addDepartment() {
  organizationForm.value.departments.push({
    name: "",
    code: "",
    parentCode: "",
    sortOrder: organizationForm.value.departments.length * 10
  });
}

function buildDefaultDepartments(): AppSetupDepartmentConfig[] {
  return [
    { name: t("setup.defaultDepartmentHq"), code: "HQ", parentCode: "", sortOrder: 0 },
    { name: t("setup.defaultDepartmentRd"), code: "RD", parentCode: "HQ", sortOrder: 10 },
    { name: t("setup.defaultDepartmentSecOps"), code: "SECOPS", parentCode: "HQ", sortOrder: 20 }
  ];
}

function buildDefaultPositions(): AppSetupPositionConfig[] {
  return [
    {
      name: t("setup.defaultPositionSysAdmin"),
      code: "SYS_ADMIN",
      description: t("setup.defaultPositionSysAdminDesc"),
      sortOrder: 10
    },
    {
      name: t("setup.defaultPositionSecLead"),
      code: "SEC_LEAD",
      description: t("setup.defaultPositionSecLeadDesc"),
      sortOrder: 20
    }
  ];
}

function removeDepartment(index: number) {
  organizationForm.value.departments.splice(index, 1);
}

function addPosition() {
  organizationForm.value.positions.push({
    name: "",
    code: "",
    description: "",
    sortOrder: organizationForm.value.positions.length * 10
  });
}

function removePosition(index: number) {
  organizationForm.value.positions.splice(index, 1);
}

function retrySetup() {
  setupError.value = null;
  completed.value = false;
  currentStep.value = 0;
}

function enterWorkspace() {
  const resolvedAppKey =
    initReport.value?.appKey || adminForm.value.appKey.trim() || configuredAppKey.value || "app-default";
  localStorage.setItem("atlas_app_last_appkey", resolvedAppKey);
  markAppSetupComplete();
  void router.push(`/apps/${encodeURIComponent(resolvedAppKey)}/login`);
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
  position: relative;
}

.locale-switch-wrapper {
  position: absolute;
  top: 20px;
  right: 20px;
  z-index: 10;
}

.app-setup-card {
  background: #fff;
  border-radius: 12px;
  padding: 48px;
  max-width: 960px;
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

.field-hint {
  color: #999;
  font-size: 12px;
  margin-top: 4px;
}

.required-role-list {
  display: flex;
  gap: 8px;
  margin-bottom: 16px;
}

.optional-role-block {
  border: 1px solid #f0f0f0;
  border-radius: 12px;
  padding: 20px;
}

.role-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 16px;
  margin-top: 16px;
}

.role-card {
  display: block;
  padding: 16px;
  border: 1px solid #f0f0f0;
  border-radius: 10px;
}

.org-section {
  margin-bottom: 24px;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
  margin-bottom: 12px;
}

.section-title {
  font-size: 16px;
  font-weight: 600;
  color: #1a1a2e;
}

.config-row {
  display: grid;
  grid-template-columns: 1.1fr 1fr 1fr 120px auto;
  gap: 12px;
  margin-bottom: 12px;
}

.setup-report {
  text-align: left;
}

@media (max-width: 900px) {
  .app-setup-card {
    padding: 28px 20px;
  }

  .config-row {
    grid-template-columns: 1fr;
  }

  .section-header {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>

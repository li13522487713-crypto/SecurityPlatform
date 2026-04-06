<template>
  <div class="setup-wizard-container">
    <div class="setup-wizard-card">
      <h1 class="setup-title">{{ t("setup.title") }}</h1>
      <p class="setup-subtitle">{{ t("setup.subtitle") }}</p>

      <a-steps :current="currentStep" class="setup-steps">
        <a-step :title="t('setup.stepWelcome')" />
        <a-step :title="t('setup.stepDatabase')" />
        <a-step :title="t('setup.stepAdmin')" />
        <a-step :title="t('setup.stepRoles')" />
        <a-step :title="t('setup.stepOrganization')" />
        <a-step :title="t('setup.stepComplete')" />
      </a-steps>

      <div class="step-content">
        <div v-if="currentStep === 0" class="step-welcome">
          <a-result status="info" :title="t('setup.welcomeTitle')" :sub-title="t('setup.welcomeDesc')">
            <template #extra>
              <a-button type="primary" size="large" data-testid="platform-setup-start" @click="currentStep = 1">
                {{ t("setup.startSetup") }}
              </a-button>
            </template>
          </a-result>
        </div>

        <div v-if="currentStep === 1" class="step-database">
          <a-form :label-col="{ span: 6 }" :wrapper-col="{ span: 16 }">
            <a-form-item :label="t('setup.databaseDriver')">
              <a-select v-model:value="dbForm.driverCode" data-testid="platform-setup-driver" @change="onDriverChange">
                <a-select-option v-for="driver in drivers" :key="driver.code" :value="driver.code">
                  {{ driver.displayName }}
                </a-select-option>
              </a-select>
            </a-form-item>

            <a-form-item :label="t('setup.connectionMode')">
              <a-radio-group v-model:value="dbForm.mode" data-testid="platform-setup-mode">
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
                  data-testid="platform-setup-connection-string"
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
                  :data-testid="`platform-setup-visual-${field.code}`"
                  :placeholder="field.placeholder ?? ''"
                />
                <a-textarea
                  v-else-if="field.multiline"
                  v-model:value="dbForm.visualConfig[field.code]"
                  :data-testid="`platform-setup-visual-${field.code}`"
                  :placeholder="field.placeholder ?? ''"
                  :rows="3"
                />
                <a-input
                  v-else
                  v-model:value="dbForm.visualConfig[field.code]"
                  :data-testid="`platform-setup-visual-${field.code}`"
                  :placeholder="field.placeholder ?? field.defaultValue ?? ''"
                />
              </a-form-item>
            </template>

            <a-form-item :wrapper-col="{ offset: 6, span: 16 }">
              <a-space>
                <a-button
                  data-testid="platform-setup-test-connection"
                  :loading="testingConnection"
                  @click="handleTestConnection"
                >
                  {{ testingConnection ? t("setup.testing") : t("setup.testConnection") }}
                </a-button>
                <a-tag
                  v-if="connectionTestResult !== null"
                  data-testid="platform-setup-test-result"
                  :color="connectionTestResult ? 'success' : 'error'"
                >
                  {{ connectionTestResult ? t("setup.testSuccess") : connectionTestMessage }}
                </a-tag>
              </a-space>
            </a-form-item>
          </a-form>

          <div class="step-actions">
            <a-button data-testid="platform-setup-prev-step" @click="currentStep = 0">{{ t("setup.prev") }}</a-button>
            <a-button
              data-testid="platform-setup-next-step"
              type="primary"
              :disabled="!connectionTestResult"
              @click="currentStep = 2"
            >
              {{ t("setup.next") }}
            </a-button>
          </div>
        </div>

        <div v-if="currentStep === 2" class="step-admin">
          <a-form :label-col="{ span: 6 }" :wrapper-col="{ span: 16 }">
            <a-form-item :label="t('setup.tenantId')">
              <a-input v-model:value="adminForm.tenantId" data-testid="platform-setup-tenant-id" />
              <div class="field-hint">{{ t("setup.tenantIdHint") }}</div>
            </a-form-item>
            <a-form-item :label="t('setup.adminUsername')" required>
              <a-input v-model:value="adminForm.username" data-testid="platform-setup-admin-username" />
            </a-form-item>
            <a-form-item :label="t('setup.adminPassword')" required>
              <a-input-password v-model:value="adminForm.password" data-testid="platform-setup-admin-password" />
            </a-form-item>
            <a-form-item :label="t('setup.adminPasswordConfirm')" required>
              <a-input-password
                v-model:value="adminForm.passwordConfirm"
                data-testid="platform-setup-admin-password-confirm"
              />
              <div v-if="passwordMismatch" class="field-error">{{ t("setup.passwordMismatch") }}</div>
            </a-form-item>
          </a-form>

          <div class="step-actions">
            <a-button data-testid="platform-setup-back-to-db" @click="currentStep = 1">{{ t("setup.prev") }}</a-button>
            <a-button
              data-testid="platform-setup-next-to-roles"
              type="primary"
              :disabled="!adminFormValid"
              @click="currentStep = 3"
            >
              {{ t("setup.next") }}
            </a-button>
          </div>
        </div>

        <div v-if="currentStep === 3" class="step-roles">
          <a-alert type="info" show-icon :message="t('setup.requiredRolesHint')" style="margin-bottom: 16px" />
          <div class="required-role-list">
            <a-tag data-testid="platform-setup-role-required-superadmin" color="processing">SuperAdmin</a-tag>
            <a-tag data-testid="platform-setup-role-required-admin" color="processing">Admin</a-tag>
          </div>

          <div class="optional-role-block">
            <div class="section-title">{{ t("setup.optionalRolesTitle") }}</div>
            <div class="field-hint">{{ t("setup.optionalRolesDesc") }}</div>
            <a-checkbox-group v-model:value="rolesForm.selectedRoleCodes">
              <div class="role-grid">
                <label v-for="role in optionalRoleTemplates" :key="role.code" class="role-card">
                  <a-checkbox :value="role.code" :data-testid="`platform-setup-role-${role.code}`">
                    {{ t(role.labelKey) }}
                  </a-checkbox>
                  <div class="field-hint">{{ t(role.descKey) }}</div>
                </label>
              </div>
            </a-checkbox-group>
          </div>

          <div class="step-actions">
            <a-button data-testid="platform-setup-back-to-admin" @click="currentStep = 2">{{ t("setup.prev") }}</a-button>
            <a-button data-testid="platform-setup-next-to-org" type="primary" @click="currentStep = 4">
              {{ t("setup.next") }}
            </a-button>
          </div>
        </div>

        <div v-if="currentStep === 4" class="step-organization">
          <div class="org-section">
            <div class="section-header">
              <div>
                <div class="section-title">{{ t("setup.departmentSectionTitle") }}</div>
                <div class="field-hint">{{ t("setup.departmentSectionDesc") }}</div>
              </div>
              <a-button data-testid="platform-setup-add-department" @click="addDepartment">
                {{ t("setup.addDepartment") }}
              </a-button>
            </div>

            <div v-for="(department, index) in organizationForm.departments" :key="`department-${index}`" class="config-row">
              <a-input
                v-model:value="department.name"
                :data-testid="`platform-setup-department-name-${index}`"
                :placeholder="t('setup.departmentNamePlaceholder')"
              />
              <a-input
                v-model:value="department.code"
                :data-testid="`platform-setup-department-code-${index}`"
                :placeholder="t('setup.departmentCodePlaceholder')"
              />
              <a-input
                v-model:value="department.parentCode"
                :data-testid="`platform-setup-department-parent-${index}`"
                :placeholder="t('setup.departmentParentPlaceholder')"
              />
              <a-input-number
                v-model:value="department.sortOrder"
                :min="0"
                :controls="false"
                :data-testid="`platform-setup-department-sort-${index}`"
              />
              <a-button
                v-if="organizationForm.departments.length > 1"
                danger
                :data-testid="`platform-setup-remove-department-${index}`"
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
              <a-button data-testid="platform-setup-add-position" @click="addPosition">
                {{ t("setup.addPosition") }}
              </a-button>
            </div>

            <div v-for="(position, index) in organizationForm.positions" :key="`position-${index}`" class="config-row">
              <a-input
                v-model:value="position.name"
                :data-testid="`platform-setup-position-name-${index}`"
                :placeholder="t('setup.positionNamePlaceholder')"
              />
              <a-input
                v-model:value="position.code"
                :data-testid="`platform-setup-position-code-${index}`"
                :placeholder="t('setup.positionCodePlaceholder')"
              />
              <a-input
                v-model:value="position.description"
                :data-testid="`platform-setup-position-description-${index}`"
                :placeholder="t('setup.positionDescriptionPlaceholder')"
              />
              <a-input-number
                v-model:value="position.sortOrder"
                :min="0"
                :controls="false"
                :data-testid="`platform-setup-position-sort-${index}`"
              />
              <a-button
                v-if="organizationForm.positions.length > 1"
                danger
                :data-testid="`platform-setup-remove-position-${index}`"
                @click="removePosition(index)"
              >
                {{ t("setup.removeRow") }}
              </a-button>
            </div>
          </div>

          <div class="step-actions">
            <a-button data-testid="platform-setup-back-to-roles" @click="currentStep = 3">{{ t("setup.prev") }}</a-button>
            <a-button
              data-testid="platform-setup-initialize"
              type="primary"
              :disabled="!organizationFormValid"
              :loading="initializing"
              @click="handleInitialize"
            >
              {{ initializing ? t("setup.initializing") : t("setup.startInitialization") }}
            </a-button>
          </div>
        </div>

        <div v-if="currentStep === 5" class="step-complete">
          <a-result
            v-if="!initError"
            data-testid="platform-setup-success"
            :status="setupCompleteResultStatus"
            :title="setupCompleteResultTitle"
            :sub-title="setupCompleteResultSubtitle"
          >
            <template #extra>
              <div v-if="bootstrapReport" class="bootstrap-report">
                <a-descriptions bordered :column="1" size="small" class="report-descriptions">
                  <a-descriptions-item :label="t('setup.reportStatus')">
                    <span data-testid="platform-setup-report-status">{{ bootstrapReport.status }}</span>
                  </a-descriptions-item>
                  <a-descriptions-item :label="t('setup.reportPlatformSetupCompleted')">
                    <span data-testid="platform-setup-report-platform-completed">
                      {{ formatBooleanFlag(bootstrapReport.platformSetupCompleted) }}
                    </span>
                  </a-descriptions-item>
                  <a-descriptions-item :label="t('setup.reportSchema')">
                    <a-tag :color="bootstrapReport.schemaInitialized ? 'success' : 'default'">
                      {{ bootstrapReport.schemaInitialized ? t('setup.reportDone') : t('setup.reportSkipped') }}
                    </a-tag>
                    <span class="report-flag" data-testid="platform-setup-report-schema">
                      {{ formatBooleanFlag(bootstrapReport.schemaInitialized) }}
                    </span>
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
                    <span class="report-flag" data-testid="platform-setup-report-seed">
                      {{ formatBooleanFlag(bootstrapReport.seedCompleted) }}
                    </span>
                    <span v-if="bootstrapReport.seedSummary" class="seed-summary">
                      {{ bootstrapReport.seedSummary }}
                    </span>
                  </a-descriptions-item>
                  <a-descriptions-item :label="t('setup.reportRoles')">
                    <span data-testid="platform-setup-report-roles-created">{{ bootstrapReport.rolesCreated }}</span>
                  </a-descriptions-item>
                  <a-descriptions-item :label="t('setup.reportDepartments')">
                    <span data-testid="platform-setup-report-departments-created">
                      {{ bootstrapReport.departmentsCreated }}
                    </span>
                  </a-descriptions-item>
                  <a-descriptions-item :label="t('setup.reportPositions')">
                    <span data-testid="platform-setup-report-positions-created">
                      {{ bootstrapReport.positionsCreated }}
                    </span>
                  </a-descriptions-item>
                  <a-descriptions-item :label="t('setup.reportAdmin')">
                    <a-tag :color="bootstrapReport.adminCreated ? 'success' : 'warning'">
                      {{ bootstrapReport.adminCreated ? t('setup.reportDone') : t('setup.reportSkipped') }}
                    </a-tag>
                    <span class="report-flag" data-testid="platform-setup-report-admin">
                      {{ formatBooleanFlag(bootstrapReport.adminCreated) }}
                    </span>
                    <span v-if="bootstrapReport.adminUsername" class="admin-name">
                      {{ bootstrapReport.adminUsername }}
                    </span>
                  </a-descriptions-item>
                  <a-descriptions-item :label="t('setup.reportEffectiveAdminRoles')">
                    <span data-testid="platform-setup-report-effective-admin-roles">
                      {{ formatRoleList(bootstrapReport.effectiveAdminRoles) }}
                    </span>
                  </a-descriptions-item>
                  <a-descriptions-item :label="t('setup.reportAdminPermissionCheck')">
                    <a-tag :color="bootstrapReport.adminPermissionCheckPassed ? 'success' : 'warning'">
                      {{ bootstrapReport.adminPermissionCheckPassed ? t('setup.reportDone') : t('setup.reportNeedsFix') }}
                    </a-tag>
                    <span class="report-flag" data-testid="platform-setup-report-admin-permission-check">
                      {{ formatBooleanFlag(bootstrapReport.adminPermissionCheckPassed) }}
                    </span>
                    <span class="seed-summary">
                      {{ bootstrapReport.adminPermissionCheckMessage }}
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
              <a-button
                type="primary"
                size="large"
                data-testid="platform-setup-go-login"
                style="margin-top: 16px"
                @click="goToLogin"
              >
                {{ t("setup.goToLogin") }}
              </a-button>
            </template>
          </a-result>
          <a-result
            v-else
            data-testid="platform-setup-failed"
            status="error"
            :title="t('setup.initFailed')"
            :sub-title="initError"
          >
            <template #extra>
              <a-button type="primary" @click="currentStep = 4">{{ t("setup.prev") }}</a-button>
            </template>
          </a-result>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import {
  getDrivers,
  initializePlatform,
  testConnection,
  type DriverDefinition,
  type InitializeResponse,
  type SetupDepartmentConfig,
  type SetupPositionConfig
} from "@/services/api-setup";
import { markSetupComplete } from "@/router";

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

const rolesForm = ref({
  selectedRoleCodes: [] as string[]
});

const organizationForm = ref({
  departments: buildDefaultDepartments(),
  positions: buildDefaultPositions()
});

const selectedDriver = computed(() => drivers.value.find((driver) => driver.code === dbForm.value.driverCode));

const passwordMismatch = computed(
  () =>
    adminForm.value.password !== "" &&
    adminForm.value.passwordConfirm !== "" &&
    adminForm.value.password !== adminForm.value.passwordConfirm
);

const adminFormValid = computed(
  () =>
    adminForm.value.username.trim() !== "" &&
    adminForm.value.password.trim() !== "" &&
    adminForm.value.password === adminForm.value.passwordConfirm &&
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

const setupCompleteResultStatus = computed(() => {
  if (bootstrapReport.value?.adminPermissionCheckPassed === false) {
    return "warning";
  }
  return "success";
});

const setupCompleteResultTitle = computed(() => {
  if (bootstrapReport.value?.adminPermissionCheckPassed === false) {
    return t("setup.completePartialTitle");
  }
  return t("setup.completeTitle");
});

const setupCompleteResultSubtitle = computed(() => {
  if (bootstrapReport.value?.adminPermissionCheckPassed === false) {
    return bootstrapReport.value.adminPermissionCheckMessage || t("setup.completePartialDesc");
  }
  return t("setup.completeDesc");
});

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

    if (resp.success && resp.data) {
      bootstrapReport.value = resp.data;
      currentStep.value = 5;
    } else {
      initError.value = resp.message || t("setup.initFailed");
      currentStep.value = 5;
    }
  } catch (error: unknown) {
    initError.value = error instanceof Error ? error.message : String(error);
    currentStep.value = 5;
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

function buildDefaultDepartments(): SetupDepartmentConfig[] {
  return [
    { name: t("setup.defaultDepartmentHq"), code: "HQ", parentCode: "", sortOrder: 0 },
    { name: t("setup.defaultDepartmentRd"), code: "RD", parentCode: "HQ", sortOrder: 10 },
    { name: t("setup.defaultDepartmentSecOps"), code: "SECOPS", parentCode: "HQ", sortOrder: 20 }
  ];
}

function buildDefaultPositions(): SetupPositionConfig[] {
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

function goToLogin() {
  markSetupComplete();
  router.push("/login");
}

function formatBooleanFlag(value: boolean): string {
  return value ? "true" : "false";
}

function formatRoleList(roles: string[]): string {
  if (roles.length === 0) {
    return t("common.none");
  }
  return roles.join(", ");
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
  max-width: 960px;
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

.report-flag {
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

@media (max-width: 900px) {
  .setup-wizard-card {
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

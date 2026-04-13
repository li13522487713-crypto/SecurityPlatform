import { useEffect, useMemo, useState, type ChangeEvent } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { appSignPath } from "../app-paths";
import { useAppI18n } from "../i18n";
import { useBootstrap } from "../bootstrap-context";
import { rememberConfiguredAppKey } from "@/services/api-core";
import {
  getDrivers,
  initializeApp,
  testConnection,
  type AppSetupDepartmentConfig,
  type AppSetupInitializeResponse,
  type AppSetupPositionConfig,
  type DriverDefinition
} from "@/services/api-setup";

type SetupStep = 0 | 1 | 2 | 3 | 4;

type OptionalRoleTemplate = {
  code: string;
  labelKey:
    | "setupRoleSecurityAdmin"
    | "setupRoleAuditAdmin"
    | "setupRoleAssetAdmin"
    | "setupRoleApprovalAdmin";
  descKey:
    | "setupRoleSecurityAdminDesc"
    | "setupRoleAuditAdminDesc"
    | "setupRoleAssetAdminDesc"
    | "setupRoleApprovalAdminDesc";
};

type SetupDbForm = {
  driverCode: string;
  mode: "raw" | "visual";
  connectionString: string;
  visualConfig: Record<string, string>;
};

const optionalRoleTemplates: OptionalRoleTemplate[] = [
  { code: "SecurityAdmin", labelKey: "setupRoleSecurityAdmin", descKey: "setupRoleSecurityAdminDesc" },
  { code: "AuditAdmin", labelKey: "setupRoleAuditAdmin", descKey: "setupRoleAuditAdminDesc" },
  { code: "AssetAdmin", labelKey: "setupRoleAssetAdmin", descKey: "setupRoleAssetAdminDesc" },
  { code: "ApprovalAdmin", labelKey: "setupRoleApprovalAdmin", descKey: "setupRoleApprovalAdminDesc" }
];

function LocaleSwitchButton() {
  const { locale, setLocale, t } = useAppI18n();

  return (
    <button
      type="button"
      className="atlas-locale-switch"
      onClick={() => setLocale(locale === "zh-CN" ? "en-US" : "zh-CN")}
    >
      {locale === "zh-CN" ? t("switchToEnglish") : t("switchToChinese")}
    </button>
  );
}

function SetupSteps({ currentStep }: { currentStep: SetupStep }) {
  const { t } = useAppI18n();
  const steps = [
    t("setupStepDatabase"),
    t("setupStepAdmin"),
    t("setupStepRoles"),
    t("setupStepOrganization"),
    t("setupStepComplete")
  ];

  return (
    <ol className="atlas-setup-steps">
      {steps.map((step, index) => {
        const status =
          currentStep === index ? "is-current" : currentStep > index ? "is-completed" : "";
        return (
          <li key={step} className={`atlas-setup-step ${status}`.trim()}>
            <span className="atlas-setup-step__dot">{index + 1}</span>
            <span className="atlas-setup-step__title">{step}</span>
          </li>
        );
      })}
    </ol>
  );
}

function SetupReportRow({
  label,
  value,
  testId
}: {
  label: string;
  value: string | number;
  testId?: string;
}) {
  return (
    <div className="atlas-setup-report__row">
      <span className="atlas-setup-report__label">{label}</span>
      <span className="atlas-setup-report__value" data-testid={testId}>
        {value}
      </span>
    </div>
  );
}

function formatBooleanFlag(value: boolean | undefined): string {
  return value ? "true" : "false";
}

export function PlatformNotReadyPage() {
  const navigate = useNavigate();
  const { t } = useAppI18n();
  const { loading, platformReady, appReady, refresh } = useBootstrap();
  const [checking, setChecking] = useState(false);

  useEffect(() => {
    if (platformReady) {
      return undefined;
    }

    let active = true;
    let timer: number | null = null;

    const schedule = () => {
      timer = window.setTimeout(async () => {
        if (!active) {
          return;
        }

        setChecking(true);
        try {
          await refresh();
        } finally {
          if (active) {
            setChecking(false);
            schedule();
          }
        }
      }, 2000);
    };

    schedule();

    return () => {
      active = false;
      if (timer !== null) {
        window.clearTimeout(timer);
      }
    };
  }, [platformReady, refresh]);

  if (!loading && platformReady) {
    return <Navigate to={appReady ? "/" : "/app-setup"} replace />;
  }

  return (
    <div className="atlas-not-ready-page">
      <div className="atlas-not-ready-result">
        <div className="atlas-not-ready-result__icon">!</div>
        <h1 className="atlas-not-ready-result__title">{t("platformNotReadyTitle")}</h1>
        <p className="atlas-not-ready-result__subtitle">{t("platformNotReadyDesc")}</p>
        <div className="atlas-not-ready-result__actions">
          <button
            type="button"
            className="atlas-button atlas-button--primary"
            onClick={() => navigate("/app-setup")}
          >
            {t("platformNotReadyGoToAppSetup")}
          </button>
          <button
            type="button"
            className="atlas-button atlas-button--secondary"
            disabled={checking}
            onClick={async () => {
              setChecking(true);
              try {
                await refresh();
              } finally {
                setChecking(false);
              }
            }}
          >
            {checking ? t("setupTesting") : t("platformNotReadyRetry")}
          </button>
        </div>
      </div>
    </div>
  );
}

export function AppSetupPage() {
  const navigate = useNavigate();
  const { locale, t } = useAppI18n();
  const { loading, platformReady, appReady } = useBootstrap();
  const [currentStep, setCurrentStep] = useState<SetupStep>(0);
  const [drivers, setDrivers] = useState<DriverDefinition[]>([]);
  const [testingConnection, setTestingConnection] = useState(false);
  const [connectionTestResult, setConnectionTestResult] = useState<boolean | null>(null);
  const [connectionTestMessage, setConnectionTestMessage] = useState("");
  const [initializing, setInitializing] = useState(false);
  const [completed, setCompleted] = useState(false);
  const [setupError, setSetupError] = useState<string | null>(null);
  const [initReport, setInitReport] = useState<AppSetupInitializeResponse | null>(null);
  const [dbForm, setDbForm] = useState<SetupDbForm>({
    driverCode: "SQLite",
    mode: "raw",
    connectionString: "Data Source=atlas.db",
    visualConfig: {}
  });
  const [adminForm, setAdminForm] = useState({
    appName: "",
    adminUsername: "admin",
    appKey: "app-default"
  });
  const [rolesForm, setRolesForm] = useState({
    selectedRoleCodes: [] as string[]
  });
  const [organizationForm, setOrganizationForm] = useState(() => ({
    departments: buildDefaultDepartments(t),
    positions: buildDefaultPositions(t)
  }));

  const selectedDriver = useMemo(
    () => drivers.find((driver) => driver.code === dbForm.driverCode),
    [dbForm.driverCode, drivers]
  );

  const adminFormValid = useMemo(
    () =>
      adminForm.appName.trim() !== "" &&
      adminForm.adminUsername.trim() !== "" &&
      adminForm.appKey.trim() !== "" &&
      !initializing,
    [adminForm, initializing]
  );

  const organizationFormValid = useMemo(() => {
    const departmentsValid =
      organizationForm.departments.length > 0 &&
      organizationForm.departments.every(
        (department) => department.name.trim() !== "" && (department.code?.trim() ?? "") !== ""
      );
    const positionsValid =
      organizationForm.positions.length > 0 &&
      organizationForm.positions.every(
        (position) => position.name.trim() !== "" && position.code.trim() !== ""
      );

    return departmentsValid && positionsValid && !initializing;
  }, [initializing, organizationForm.departments, organizationForm.positions]);

  useEffect(() => {
    let cancelled = false;

    const loadDrivers = async () => {
      try {
        const response = await getDrivers();
        if (!cancelled && response.success && response.data) {
          setDrivers(response.data);
        }
      } catch {
        if (!cancelled) {
          setDrivers([]);
        }
      }
    };

    void loadDrivers();

    return () => {
      cancelled = true;
    };
  }, []);

  if (!loading && appReady && !completed) {
    return <Navigate to="/" replace />;
  }

  const setVisualField = (code: string, value: string) => {
    setDbForm((previous) => ({
      ...previous,
      visualConfig: {
        ...previous.visualConfig,
        [code]: value
      }
    }));
  };

  const onDriverChange = (event: ChangeEvent<HTMLSelectElement>) => {
    const nextDriverCode = event.target.value;
    const driver = drivers.find((item) => item.code === nextDriverCode);
    const visualConfig: Record<string, string> = {};
    if (driver?.supportsVisual) {
      for (const field of driver.fields) {
        if (field.defaultValue) {
          visualConfig[field.code] = field.defaultValue;
        }
      }
    }

    setConnectionTestResult(null);
    setConnectionTestMessage("");
    setDbForm({
      driverCode: nextDriverCode,
      mode: "raw",
      connectionString: driver?.connectionStringExample || "Data Source=atlas.db",
      visualConfig
    });
  };

  const handleTestConnection = async () => {
    setTestingConnection(true);
    setConnectionTestResult(null);
    setConnectionTestMessage("");

    try {
      const response = await testConnection({
        driverCode: dbForm.driverCode,
        mode: dbForm.mode,
        connectionString: dbForm.mode === "raw" ? dbForm.connectionString : undefined,
        visualConfig: dbForm.mode === "visual" ? dbForm.visualConfig : undefined
      });

      if (response.success && response.data) {
        setConnectionTestResult(response.data.connected);
        setConnectionTestMessage(response.data.message);
      } else {
        setConnectionTestResult(false);
        setConnectionTestMessage(response.message || t("loginFailed"));
      }
    } catch (error) {
      setConnectionTestResult(false);
      setConnectionTestMessage(error instanceof Error ? error.message : t("loginFailed"));
    } finally {
      setTestingConnection(false);
    }
  };

  const handleInitialize = async () => {
    setInitializing(true);
    setSetupError(null);

    try {
      const response = await initializeApp({
        database: {
          driverCode: dbForm.driverCode,
          mode: dbForm.mode,
          connectionString: dbForm.mode === "raw" ? dbForm.connectionString : undefined,
          visualConfig: dbForm.mode === "visual" ? dbForm.visualConfig : undefined
        },
        admin: {
          appName: adminForm.appName.trim(),
          adminUsername: adminForm.adminUsername.trim(),
          appKey: adminForm.appKey.trim() || undefined
        },
        roles: {
          selectedRoleCodes: rolesForm.selectedRoleCodes
        },
        organization: {
          departments: organizationForm.departments.map((department) => ({
            name: department.name.trim(),
            code: department.code?.trim() || undefined,
            parentCode: department.parentCode?.trim() || undefined,
            sortOrder: department.sortOrder ?? 0
          })),
          positions: organizationForm.positions.map((position) => ({
            name: position.name.trim(),
            code: position.code.trim(),
            description: position.description?.trim() || undefined,
            sortOrder: position.sortOrder ?? 0
          }))
        }
      });

      if (response.success) {
        if (response.data?.appKey) {
          rememberConfiguredAppKey(response.data.appKey);
        }
        setInitReport(response.data ?? null);
        setCompleted(true);
        setCurrentStep(4);
        return;
      }

      setSetupError(response.message || t("setupAppSetupFailed"));
      setCurrentStep(4);
    } catch (error) {
      setSetupError(error instanceof Error ? error.message : t("setupAppSetupFailed"));
      setCurrentStep(4);
    } finally {
      setInitializing(false);
    }
  };

  const enterWorkspace = () => {
    const resolvedAppKey = initReport?.appKey || adminForm.appKey.trim() || "app-default";
    rememberConfiguredAppKey(resolvedAppKey);
    navigate(appSignPath(resolvedAppKey), { replace: true });
  };

  return (
    <div className="atlas-setup-page" data-testid="app-setup-page">
      <div className="atlas-setup-page__locale">
        <LocaleSwitchButton />
      </div>
      <div className="atlas-setup-card">
        <h1 className="atlas-setup-card__title">{t("appSetupTitle")}</h1>
        <p className="atlas-setup-card__subtitle">{t("appSetupSubtitle")}</p>

        <SetupSteps currentStep={currentStep} />

        {currentStep === 0 ? (
          <section className="atlas-setup-panel atlas-setup-panel--database">
            <div className="atlas-form-grid">
              <label className="atlas-form-field">
                <span className="atlas-form-field__label">{t("setupDatabaseDriver")}</span>
                <select
                  className="atlas-input"
                  data-testid="app-setup-driver"
                  value={dbForm.driverCode}
                  onChange={onDriverChange}
                >
                  {drivers.map((driver) => (
                    <option key={driver.code} value={driver.code}>
                      {driver.displayName}
                    </option>
                  ))}
                </select>
              </label>

              <fieldset className="atlas-radio-group" data-testid="app-setup-mode">
                <legend className="atlas-form-field__label">{t("setupConnectionMode")}</legend>
                <label className={`atlas-radio-card ${dbForm.mode === "raw" ? "is-selected" : ""}`.trim()}>
                  <input
                    checked={dbForm.mode === "raw"}
                    name="setup-mode"
                    type="radio"
                    value="raw"
                    onChange={() => setDbForm((previous) => ({ ...previous, mode: "raw" }))}
                  />
                  <span>{t("setupModeRaw")}</span>
                </label>
                {selectedDriver?.supportsVisual ? (
                  <label className={`atlas-radio-card ${dbForm.mode === "visual" ? "is-selected" : ""}`.trim()}>
                    <input
                      checked={dbForm.mode === "visual"}
                      name="setup-mode"
                      type="radio"
                      value="visual"
                      onChange={() => setDbForm((previous) => ({ ...previous, mode: "visual" }))}
                    />
                    <span>{t("setupModeVisual")}</span>
                  </label>
                ) : null}
              </fieldset>

              {dbForm.mode === "raw" ? (
                <label className="atlas-form-field atlas-form-field--full">
                  <span className="atlas-form-field__label">{t("setupConnectionString")}</span>
                  <input
                    className="atlas-input"
                    data-testid="app-setup-connection-string"
                    placeholder={selectedDriver?.connectionStringExample || ""}
                    value={dbForm.connectionString}
                    onChange={(event) =>
                      setDbForm((previous) => ({
                        ...previous,
                        connectionString: event.target.value
                      }))
                    }
                  />
                </label>
              ) : null}

              {dbForm.mode === "visual" && selectedDriver ? (
                <div className="atlas-visual-fields atlas-form-field--full">
                  {selectedDriver.fields.map((field) => (
                    <label key={field.code} className="atlas-form-field">
                      <span className="atlas-form-field__label">
                        {field.label}
                        {field.required ? <em className="atlas-required">*</em> : null}
                      </span>
                      {field.secret ? (
                        <input
                          className="atlas-input"
                          data-testid={`app-setup-visual-${field.code}`}
                          placeholder={field.placeholder ?? ""}
                          type="password"
                          value={dbForm.visualConfig[field.code] ?? ""}
                          onChange={(event) => setVisualField(field.code, event.target.value)}
                        />
                      ) : field.multiline ? (
                        <textarea
                          className="atlas-input atlas-input--textarea"
                          data-testid={`app-setup-visual-${field.code}`}
                          placeholder={field.placeholder ?? ""}
                          rows={3}
                          value={dbForm.visualConfig[field.code] ?? ""}
                          onChange={(event) => setVisualField(field.code, event.target.value)}
                        />
                      ) : (
                        <input
                          className="atlas-input"
                          data-testid={`app-setup-visual-${field.code}`}
                          placeholder={field.placeholder ?? field.defaultValue ?? ""}
                          value={dbForm.visualConfig[field.code] ?? ""}
                          onChange={(event) => setVisualField(field.code, event.target.value)}
                        />
                      )}
                    </label>
                  ))}
                </div>
              ) : null}
            </div>

            <div className="atlas-setup-toolbar">
              <button
                type="button"
                className="atlas-button atlas-button--secondary"
                data-testid="app-setup-test-connection"
                disabled={testingConnection}
                onClick={() => void handleTestConnection()}
              >
                {testingConnection ? t("setupTesting") : t("setupTestConnection")}
              </button>
              {connectionTestResult !== null ? (
                <span
                  className={`atlas-pill ${connectionTestResult ? "is-success" : "is-error"}`.trim()}
                  data-testid="app-setup-test-result"
                >
                  {connectionTestResult ? t("setupTestSuccess") : connectionTestMessage}
                </span>
              ) : null}
            </div>

            <div className="atlas-setup-actions">
              <span />
              <button
                type="button"
                className="atlas-button atlas-button--primary"
                data-testid="app-setup-next-step"
                disabled={!connectionTestResult}
                onClick={() => setCurrentStep(1)}
              >
                {t("setupNext")}
              </button>
            </div>
          </section>
        ) : null}

        {currentStep === 1 ? (
          <section className="atlas-setup-panel">
            <div className="atlas-form-grid">
              <label className="atlas-form-field atlas-form-field--full">
                <span className="atlas-form-field__label">{t("setupAppName")}</span>
                <input
                  className="atlas-input"
                  data-testid="app-setup-name"
                  placeholder={t("setupAppNamePlaceholder")}
                  value={adminForm.appName}
                  onChange={(event) =>
                    setAdminForm((previous) => ({
                      ...previous,
                      appName: event.target.value
                    }))
                  }
                />
              </label>

              <label className="atlas-form-field atlas-form-field--full">
                <span className="atlas-form-field__label">{t("setupAdminUsername")}</span>
                <input
                  className="atlas-input"
                  data-testid="app-setup-admin-username"
                  placeholder={t("setupAdminUsernamePlaceholder")}
                  value={adminForm.adminUsername}
                  onChange={(event) =>
                    setAdminForm((previous) => ({
                      ...previous,
                      adminUsername: event.target.value
                    }))
                  }
                />
              </label>

              <label className="atlas-form-field atlas-form-field--full">
                <span className="atlas-form-field__label">{t("setupAppKey")}</span>
                <input
                  className="atlas-input"
                  data-testid="app-setup-app-key"
                  placeholder={t("setupAppKeyPlaceholder")}
                  value={adminForm.appKey}
                  onChange={(event) =>
                    setAdminForm((previous) => ({
                      ...previous,
                      appKey: event.target.value
                    }))
                  }
                />
              </label>
            </div>

            <div className="atlas-setup-actions">
              <button
                type="button"
                className="atlas-button atlas-button--secondary"
                data-testid="app-setup-prev-step"
                onClick={() => setCurrentStep(0)}
              >
                {t("setupPrev")}
              </button>
              <button
                type="button"
                className="atlas-button atlas-button--primary"
                data-testid="app-setup-next-to-roles"
                disabled={!adminFormValid}
                onClick={() => setCurrentStep(2)}
              >
                {t("setupNext")}
              </button>
            </div>
          </section>
        ) : null}

        {currentStep === 2 ? (
          <section className="atlas-setup-panel">
            <div className="atlas-info-banner">{t("setupRequiredRolesHint")}</div>
            <div className="atlas-required-role-list">
              <span className="atlas-tag" data-testid="app-setup-role-required-app-admin">
                AppAdmin
              </span>
              <span className="atlas-tag" data-testid="app-setup-role-required-app-member">
                AppMember
              </span>
            </div>

            <div className="atlas-optional-role-block">
              <div className="atlas-section-title">{t("setupOptionalRolesTitle")}</div>
              <div className="atlas-field-hint">{t("setupOptionalRolesDesc")}</div>
              <div className="atlas-role-grid">
                {optionalRoleTemplates.map((role) => {
                  const checked = rolesForm.selectedRoleCodes.includes(role.code);
                  return (
                    <label
                      key={role.code}
                      className={`atlas-role-card ${checked ? "is-selected" : ""}`.trim()}
                    >
                      <span className="atlas-role-card__header">
                        <input
                          checked={checked}
                          data-testid={`app-setup-role-${role.code}`}
                          type="checkbox"
                          value={role.code}
                          onChange={(event) => {
                            setRolesForm((previous) => {
                              const current = new Set(previous.selectedRoleCodes);
                              if (event.target.checked) {
                                current.add(role.code);
                              } else {
                                current.delete(role.code);
                              }

                              return {
                                selectedRoleCodes: Array.from(current)
                              };
                            });
                          }}
                        />
                        <span>{t(role.labelKey)}</span>
                      </span>
                      <span className="atlas-field-hint">{t(role.descKey)}</span>
                    </label>
                  );
                })}
              </div>
            </div>

            <div className="atlas-setup-actions">
              <button
                type="button"
                className="atlas-button atlas-button--secondary"
                data-testid="app-setup-back-to-admin"
                onClick={() => setCurrentStep(1)}
              >
                {t("setupPrev")}
              </button>
              <button
                type="button"
                className="atlas-button atlas-button--primary"
                data-testid="app-setup-next-to-org"
                onClick={() => setCurrentStep(3)}
              >
                {t("setupNext")}
              </button>
            </div>
          </section>
        ) : null}

        {currentStep === 3 ? (
          <section className="atlas-setup-panel">
            <div className="atlas-org-section">
              <div className="atlas-org-section__header">
                <div>
                  <div className="atlas-section-title">{t("setupDepartmentSectionTitle")}</div>
                  <div className="atlas-field-hint">{t("setupDepartmentSectionDesc")}</div>
                </div>
                <button
                  type="button"
                  className="atlas-button atlas-button--secondary"
                  data-testid="app-setup-add-department"
                  onClick={() =>
                    setOrganizationForm((previous) => ({
                      ...previous,
                      departments: [
                        ...previous.departments,
                        {
                          name: "",
                          code: "",
                          parentCode: "",
                          sortOrder: previous.departments.length * 10
                        }
                      ]
                    }))
                  }
                >
                  {t("setupAddDepartment")}
                </button>
              </div>

              {organizationForm.departments.map((department, index) => (
                <div key={`department-${index}`} className="atlas-config-row">
                  <input
                    className="atlas-input"
                    data-testid={`app-setup-department-name-${index}`}
                    placeholder={t("setupDepartmentNamePlaceholder")}
                    value={department.name}
                    onChange={(event) =>
                      setOrganizationForm((previous) => ({
                        ...previous,
                        departments: previous.departments.map((item, itemIndex) =>
                          itemIndex === index ? { ...item, name: event.target.value } : item
                        )
                      }))
                    }
                  />
                  <input
                    className="atlas-input"
                    data-testid={`app-setup-department-code-${index}`}
                    placeholder={t("setupDepartmentCodePlaceholder")}
                    value={department.code ?? ""}
                    onChange={(event) =>
                      setOrganizationForm((previous) => ({
                        ...previous,
                        departments: previous.departments.map((item, itemIndex) =>
                          itemIndex === index ? { ...item, code: event.target.value } : item
                        )
                      }))
                    }
                  />
                  <input
                    className="atlas-input"
                    data-testid={`app-setup-department-parent-${index}`}
                    placeholder={t("setupDepartmentParentPlaceholder")}
                    value={department.parentCode ?? ""}
                    onChange={(event) =>
                      setOrganizationForm((previous) => ({
                        ...previous,
                        departments: previous.departments.map((item, itemIndex) =>
                          itemIndex === index ? { ...item, parentCode: event.target.value } : item
                        )
                      }))
                    }
                  />
                  <input
                    className="atlas-input"
                    data-testid={`app-setup-department-sort-${index}`}
                    inputMode="numeric"
                    type="number"
                    value={department.sortOrder}
                    onChange={(event) =>
                      setOrganizationForm((previous) => ({
                        ...previous,
                        departments: previous.departments.map((item, itemIndex) =>
                          itemIndex === index
                            ? {
                                ...item,
                                sortOrder: Number.parseInt(event.target.value || "0", 10) || 0
                              }
                            : item
                        )
                      }))
                    }
                  />
                  {organizationForm.departments.length > 1 ? (
                    <button
                      type="button"
                      className="atlas-button atlas-button--danger"
                      data-testid={`app-setup-remove-department-${index}`}
                      onClick={() =>
                        setOrganizationForm((previous) => ({
                          ...previous,
                          departments: previous.departments.filter((_, itemIndex) => itemIndex !== index)
                        }))
                      }
                    >
                      {t("setupRemoveRow")}
                    </button>
                  ) : null}
                </div>
              ))}
            </div>

            <div className="atlas-org-section">
              <div className="atlas-org-section__header">
                <div>
                  <div className="atlas-section-title">{t("setupPositionSectionTitle")}</div>
                  <div className="atlas-field-hint">{t("setupPositionSectionDesc")}</div>
                </div>
                <button
                  type="button"
                  className="atlas-button atlas-button--secondary"
                  data-testid="app-setup-add-position"
                  onClick={() =>
                    setOrganizationForm((previous) => ({
                      ...previous,
                      positions: [
                        ...previous.positions,
                        {
                          name: "",
                          code: "",
                          description: "",
                          sortOrder: previous.positions.length * 10
                        }
                      ]
                    }))
                  }
                >
                  {t("setupAddPosition")}
                </button>
              </div>

              {organizationForm.positions.map((position, index) => (
                <div key={`position-${index}`} className="atlas-config-row">
                  <input
                    className="atlas-input"
                    data-testid={`app-setup-position-name-${index}`}
                    placeholder={t("setupPositionNamePlaceholder")}
                    value={position.name}
                    onChange={(event) =>
                      setOrganizationForm((previous) => ({
                        ...previous,
                        positions: previous.positions.map((item, itemIndex) =>
                          itemIndex === index ? { ...item, name: event.target.value } : item
                        )
                      }))
                    }
                  />
                  <input
                    className="atlas-input"
                    data-testid={`app-setup-position-code-${index}`}
                    placeholder={t("setupPositionCodePlaceholder")}
                    value={position.code}
                    onChange={(event) =>
                      setOrganizationForm((previous) => ({
                        ...previous,
                        positions: previous.positions.map((item, itemIndex) =>
                          itemIndex === index ? { ...item, code: event.target.value } : item
                        )
                      }))
                    }
                  />
                  <input
                    className="atlas-input"
                    data-testid={`app-setup-position-description-${index}`}
                    placeholder={t("setupPositionDescriptionPlaceholder")}
                    value={position.description ?? ""}
                    onChange={(event) =>
                      setOrganizationForm((previous) => ({
                        ...previous,
                        positions: previous.positions.map((item, itemIndex) =>
                          itemIndex === index ? { ...item, description: event.target.value } : item
                        )
                      }))
                    }
                  />
                  <input
                    className="atlas-input"
                    data-testid={`app-setup-position-sort-${index}`}
                    inputMode="numeric"
                    type="number"
                    value={position.sortOrder}
                    onChange={(event) =>
                      setOrganizationForm((previous) => ({
                        ...previous,
                        positions: previous.positions.map((item, itemIndex) =>
                          itemIndex === index
                            ? {
                                ...item,
                                sortOrder: Number.parseInt(event.target.value || "0", 10) || 0
                              }
                            : item
                        )
                      }))
                    }
                  />
                  {organizationForm.positions.length > 1 ? (
                    <button
                      type="button"
                      className="atlas-button atlas-button--danger"
                      data-testid={`app-setup-remove-position-${index}`}
                      onClick={() =>
                        setOrganizationForm((previous) => ({
                          ...previous,
                          positions: previous.positions.filter((_, itemIndex) => itemIndex !== index)
                        }))
                      }
                    >
                      {t("setupRemoveRow")}
                    </button>
                  ) : null}
                </div>
              ))}
            </div>

            <div className="atlas-setup-actions">
              <button
                type="button"
                className="atlas-button atlas-button--secondary"
                data-testid="app-setup-back-to-roles"
                onClick={() => setCurrentStep(2)}
              >
                {t("setupPrev")}
              </button>
              <button
                type="button"
                className="atlas-button atlas-button--primary"
                data-testid="app-setup-initialize"
                disabled={!organizationFormValid || initializing}
                onClick={() => void handleInitialize()}
              >
                {initializing ? t("setupInitializing") : t("setupStartInitialization")}
              </button>
            </div>
          </section>
        ) : null}

        {currentStep === 4 && completed ? (
          <section className="atlas-result-card atlas-result-card--success" data-testid="app-setup-success">
            <div className="atlas-result-card__icon">✓</div>
            <h2 className="atlas-result-card__title">{t("setupAppSetupComplete")}</h2>
            <p className="atlas-result-card__subtitle">{t("setupAppSetupCompleteDesc")}</p>

            {initReport ? (
              <div className="atlas-setup-report">
                <SetupReportRow
                  label={t("setupPlatformStatus")}
                  testId="app-setup-report-platform-status"
                  value={initReport.platformStatus}
                />
                <SetupReportRow
                  label={t("setupAppStatus")}
                  testId="app-setup-report-app-status"
                  value={initReport.appStatus}
                />
                <SetupReportRow
                  label={t("setupAppSetupCompleted")}
                  testId="app-setup-report-app-completed"
                  value={formatBooleanFlag(initReport.appSetupCompleted)}
                />
                <SetupReportRow
                  label={t("setupDbConnected")}
                  testId="app-setup-report-db-connected"
                  value={formatBooleanFlag(initReport.databaseConnected)}
                />
                <SetupReportRow
                  label={t("setupCoreTablesVerified")}
                  testId="app-setup-report-core-tables"
                  value={formatBooleanFlag(initReport.coreTablesVerified)}
                />
                <SetupReportRow
                  label={t("setupReportRoles")}
                  testId="app-setup-report-roles-created"
                  value={initReport.rolesCreated}
                />
                <SetupReportRow
                  label={t("setupReportDepartments")}
                  testId="app-setup-report-departments-created"
                  value={initReport.departmentsCreated}
                />
                <SetupReportRow
                  label={t("setupReportPositions")}
                  testId="app-setup-report-positions-created"
                  value={initReport.positionsCreated}
                />
                <SetupReportRow
                  label={t("setupReportAdmin")}
                  testId="app-setup-report-admin-bound"
                  value={formatBooleanFlag(initReport.adminBound)}
                />
              </div>
            ) : null}

            <div className="atlas-warning-banner">
              <strong>{t("setupRestartRequired")}</strong>
              <p>{t("setupRestartRequiredDesc")}</p>
            </div>

            <button
              type="button"
              className="atlas-button atlas-button--primary atlas-button--large"
              data-testid="app-setup-enter-workspace"
              onClick={enterWorkspace}
            >
              {t("setupGoToLogin")}
            </button>
          </section>
        ) : null}

        {currentStep === 4 && setupError ? (
          <section className="atlas-result-card atlas-result-card--error" data-testid="app-setup-failed">
            <div className="atlas-result-card__icon">×</div>
            <h2 className="atlas-result-card__title">{t("setupAppSetupFailed")}</h2>
            <p className="atlas-result-card__subtitle">{setupError}</p>
            <button
              type="button"
              className="atlas-button atlas-button--primary"
              onClick={() => {
                setSetupError(null);
                setCompleted(false);
                setCurrentStep(0);
              }}
            >
              {t("platformNotReadyRetry")}
            </button>
          </section>
        ) : null}

        <div className="atlas-setup-locale-note" aria-hidden="true">
          {locale}
        </div>
      </div>
    </div>
  );
}

function buildDefaultDepartments(
  t: (key: "setupDefaultDepartmentHq" | "setupDefaultDepartmentRd" | "setupDefaultDepartmentSecOps") => string
): AppSetupDepartmentConfig[] {
  return [
    { name: t("setupDefaultDepartmentHq"), code: "HQ", parentCode: "", sortOrder: 0 },
    { name: t("setupDefaultDepartmentRd"), code: "RD", parentCode: "HQ", sortOrder: 10 },
    { name: t("setupDefaultDepartmentSecOps"), code: "SECOPS", parentCode: "HQ", sortOrder: 20 }
  ];
}

function buildDefaultPositions(
  t: (key: "setupDefaultPositionSysAdmin" | "setupDefaultPositionSysAdminDesc" | "setupDefaultPositionSecLead" | "setupDefaultPositionSecLeadDesc") => string
): AppSetupPositionConfig[] {
  return [
    {
      name: t("setupDefaultPositionSysAdmin"),
      code: "SYS_ADMIN",
      description: t("setupDefaultPositionSysAdminDesc"),
      sortOrder: 10
    },
    {
      name: t("setupDefaultPositionSecLead"),
      code: "SEC_LEAD",
      description: t("setupDefaultPositionSecLeadDesc"),
      sortOrder: 20
    }
  ];
}

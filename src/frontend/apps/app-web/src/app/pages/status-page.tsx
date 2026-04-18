import { useEffect, useMemo, useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import {
  Button,
  Checkbox,
  Descriptions,
  Input,
  InputNumber,
  Radio,
  RadioGroup,
  Select,
  Space,
  Tag,
  TextArea,
  Typography
} from "@douyinfe/semi-ui";
import { appSignPath } from "../app-paths";
import { useAppI18n } from "../i18n";
import { useBootstrap } from "../bootstrap-context";
import { rememberConfiguredAppKey } from "../../services/api-core";
import {
  getDrivers,
  initializeApp,
  testConnection,
  type AppSetupDepartmentConfig,
  type AppSetupInitializeResponse,
  type AppSetupPositionConfig,
  type DriverDefinition
} from "../../services/api-setup";
import {
  FormCard,
  InfoBanner,
  PageShell,
  ResultCard,
  SectionCard,
  StateBadge,
  StepsBar
} from "../_shared";

const { Title, Text } = Typography;

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
    <Button
      theme="borderless"
      type="tertiary"
      onClick={() => setLocale(locale === "zh-CN" ? "en-US" : "zh-CN")}
    >
      {locale === "zh-CN" ? t("switchToEnglish") : t("switchToChinese")}
    </Button>
  );
}

function formatBooleanFlag(value: boolean | undefined): string {
  return value ? "true" : "false";
}

export function PlatformNotReadyPage() {
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
    <PageShell centered maxWidth={520}>
      <ResultCard
        status="warning"
        title={t("platformNotReadyTitle")}
        description={t("platformNotReadyDesc")}
        actions={
          <>
            <Button
              type="primary"
              theme="solid"
              onClick={() => {
                window.location.href = "/app-setup";
              }}
            >
              {t("platformNotReadyGoToAppSetup")}
            </Button>
            <Button
              type="tertiary"
              theme="light"
              loading={checking}
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
            </Button>
          </>
        }
      />
    </PageShell>
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
    setCurrentStep(0);
    setTestingConnection(false);
    setConnectionTestResult(null);
    setConnectionTestMessage("");
    setInitializing(false);
    setCompleted(false);
    setSetupError(null);
    setInitReport(null);
  }, []);

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

  const onDriverChange = (value: string | number | unknown[] | Record<string, unknown> | undefined) => {
    const nextDriverCode = String(value ?? "");
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

  const stepsConfig = [
    { title: t("setupStepDatabase") },
    { title: t("setupStepAdmin") },
    { title: t("setupStepRoles") },
    { title: t("setupStepOrganization") },
    { title: t("setupStepComplete") }
  ];

  return (
    <PageShell centered maxWidth={960} testId="app-setup-page">
      <div style={{ position: "absolute", top: 24, right: 24 }}>
        <LocaleSwitchButton />
      </div>
      <FormCard title={t("appSetupTitle")} subtitle={t("appSetupSubtitle")}>
        {!loading && !platformReady ? (
          <div style={{ marginBottom: 16 }}>
            <InfoBanner
              variant="warning"
              compact
              title={t("platformNotReadyTitle")}
              description={t("platformNotReadyDesc")}
            />
          </div>
        ) : null}

        <StepsBar steps={stepsConfig} current={currentStep} />

        {currentStep === 0 ? (
          <SectionCard>
            <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
              <FieldLabel label={t("setupDatabaseDriver")}>
                <Select
                  data-testid="app-setup-driver"
                  value={dbForm.driverCode}
                  onChange={onDriverChange}
                  style={{ width: "100%" }}
                  optionList={drivers.map((driver) => ({
                    label: driver.displayName,
                    value: driver.code
                  }))}
                />
              </FieldLabel>

              <FieldLabel label={t("setupConnectionMode")}>
                <RadioGroup
                  data-testid="app-setup-mode"
                  type="button"
                  value={dbForm.mode}
                  onChange={(event) => {
                    const nextMode = String(event.target.value) === "visual" ? "visual" : "raw";
                    setDbForm((previous) => ({ ...previous, mode: nextMode }));
                  }}
                >
                  <Radio value="raw">{t("setupModeRaw")}</Radio>
                  {selectedDriver?.supportsVisual ? (
                    <Radio value="visual">{t("setupModeVisual")}</Radio>
                  ) : null}
                </RadioGroup>
              </FieldLabel>

              {dbForm.mode === "raw" ? (
                <FieldLabel label={t("setupConnectionString")}>
                  <Input
                    data-testid="app-setup-connection-string"
                    placeholder={selectedDriver?.connectionStringExample || ""}
                    value={dbForm.connectionString}
                    onChange={(value) =>
                      setDbForm((previous) => ({ ...previous, connectionString: value }))
                    }
                  />
                </FieldLabel>
              ) : null}

              {dbForm.mode === "visual" && selectedDriver ? (
                <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                  {selectedDriver.fields.map((field) => (
                    <FieldLabel
                      key={field.code}
                      label={
                        <span>
                          {field.label}
                          {field.required ? (
                            <Text type="danger" style={{ marginLeft: 4 }}>
                              *
                            </Text>
                          ) : null}
                        </span>
                      }
                    >
                      {field.secret ? (
                        <Input
                          mode="password"
                          data-testid={`app-setup-visual-${field.code}`}
                          placeholder={field.placeholder ?? ""}
                          value={dbForm.visualConfig[field.code] ?? ""}
                          onChange={(value) => setVisualField(field.code, value)}
                        />
                      ) : field.multiline ? (
                        <TextArea
                          data-testid={`app-setup-visual-${field.code}`}
                          placeholder={field.placeholder ?? ""}
                          rows={3}
                          value={dbForm.visualConfig[field.code] ?? ""}
                          onChange={(value) => setVisualField(field.code, value)}
                        />
                      ) : (
                        <Input
                          data-testid={`app-setup-visual-${field.code}`}
                          placeholder={field.placeholder ?? field.defaultValue ?? ""}
                          value={dbForm.visualConfig[field.code] ?? ""}
                          onChange={(value) => setVisualField(field.code, value)}
                        />
                      )}
                    </FieldLabel>
                  ))}
                </div>
              ) : null}

              <Space align="center">
                <Button
                  type="tertiary"
                  theme="light"
                  data-testid="app-setup-test-connection"
                  loading={testingConnection}
                  onClick={() => void handleTestConnection()}
                >
                  {testingConnection ? t("setupTesting") : t("setupTestConnection")}
                </Button>
                {connectionTestResult !== null ? (
                  <StateBadge
                    variant={connectionTestResult ? "success" : "danger"}
                    testId="app-setup-test-result"
                  >
                    {connectionTestResult ? t("setupTestSuccess") : connectionTestMessage}
                  </StateBadge>
                ) : null}
              </Space>

              <StepActions>
                <span />
                <Button
                  type="primary"
                  theme="solid"
                  data-testid="app-setup-next-step"
                  disabled={!connectionTestResult}
                  onClick={() => setCurrentStep(1)}
                >
                  {t("setupNext")}
                </Button>
              </StepActions>
            </div>
          </SectionCard>
        ) : null}

        {currentStep === 1 ? (
          <SectionCard>
            <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
              <FieldLabel label={t("setupAppName")}>
                <Input
                  data-testid="app-setup-name"
                  placeholder={t("setupAppNamePlaceholder")}
                  value={adminForm.appName}
                  onChange={(value) => setAdminForm((previous) => ({ ...previous, appName: value }))}
                />
              </FieldLabel>

              <FieldLabel label={t("setupAdminUsername")}>
                <Input
                  data-testid="app-setup-admin-username"
                  placeholder={t("setupAdminUsernamePlaceholder")}
                  value={adminForm.adminUsername}
                  onChange={(value) =>
                    setAdminForm((previous) => ({ ...previous, adminUsername: value }))
                  }
                />
              </FieldLabel>

              <FieldLabel label={t("setupAppKey")}>
                <Input
                  data-testid="app-setup-app-key"
                  placeholder={t("setupAppKeyPlaceholder")}
                  value={adminForm.appKey}
                  onChange={(value) => setAdminForm((previous) => ({ ...previous, appKey: value }))}
                />
              </FieldLabel>

              <StepActions>
                <Button
                  type="tertiary"
                  theme="light"
                  data-testid="app-setup-prev-step"
                  onClick={() => setCurrentStep(0)}
                >
                  {t("setupPrev")}
                </Button>
                <Button
                  type="primary"
                  theme="solid"
                  data-testid="app-setup-next-to-roles"
                  disabled={!adminFormValid}
                  onClick={() => setCurrentStep(2)}
                >
                  {t("setupNext")}
                </Button>
              </StepActions>
            </div>
          </SectionCard>
        ) : null}

        {currentStep === 2 ? (
          <SectionCard>
            <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
              <InfoBanner variant="info" compact description={t("setupRequiredRolesHint")} />
              <Space>
                <Tag color="blue" data-testid="app-setup-role-required-app-admin">
                  AppAdmin
                </Tag>
                <Tag color="blue" data-testid="app-setup-role-required-app-member">
                  AppMember
                </Tag>
              </Space>

              <div>
                <Title heading={6} style={{ margin: "0 0 4px" }}>
                  {t("setupOptionalRolesTitle")}
                </Title>
                <Text type="tertiary" style={{ display: "block", marginBottom: 12 }}>
                  {t("setupOptionalRolesDesc")}
                </Text>
                <div
                  style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))",
                    gap: 12
                  }}
                >
                  {optionalRoleTemplates.map((role) => {
                    const checked = rolesForm.selectedRoleCodes.includes(role.code);
                    return (
                      <label
                        key={role.code}
                        style={{
                          display: "flex",
                          flexDirection: "column",
                          gap: 4,
                          padding: 12,
                          borderRadius: 8,
                          border: `1px solid ${
                            checked ? "var(--semi-color-primary)" : "var(--semi-color-border)"
                          }`,
                          background: checked
                            ? "var(--semi-color-primary-light-default)"
                            : "var(--semi-color-bg-2)",
                          cursor: "pointer"
                        }}
                      >
                        <span style={{ display: "flex", alignItems: "center", gap: 8 }}>
                          <Checkbox
                            checked={checked}
                            data-testid={`app-setup-role-${role.code}`}
                            value={role.code}
                            onChange={(event) => {
                              const isChecked = Boolean(event.target.checked);
                              setRolesForm((previous) => {
                                const current = new Set(previous.selectedRoleCodes);
                                if (isChecked) {
                                  current.add(role.code);
                                } else {
                                  current.delete(role.code);
                                }
                                return { selectedRoleCodes: Array.from(current) };
                              });
                            }}
                          />
                          <Text strong>{t(role.labelKey)}</Text>
                        </span>
                        <Text type="tertiary" style={{ fontSize: 12 }}>
                          {t(role.descKey)}
                        </Text>
                      </label>
                    );
                  })}
                </div>
              </div>

              <StepActions>
                <Button
                  type="tertiary"
                  theme="light"
                  data-testid="app-setup-back-to-admin"
                  onClick={() => setCurrentStep(1)}
                >
                  {t("setupPrev")}
                </Button>
                <Button
                  type="primary"
                  theme="solid"
                  data-testid="app-setup-next-to-org"
                  onClick={() => setCurrentStep(3)}
                >
                  {t("setupNext")}
                </Button>
              </StepActions>
            </div>
          </SectionCard>
        ) : null}

        {currentStep === 3 ? (
          <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
            <SectionCard
              title={t("setupDepartmentSectionTitle")}
              subtitle={t("setupDepartmentSectionDesc")}
              actions={
                <Button
                  type="tertiary"
                  theme="light"
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
                </Button>
              }
            >
              <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                {organizationForm.departments.map((department, index) => (
                  <ConfigRow
                    key={`department-${index}`}
                    canRemove={organizationForm.departments.length > 1}
                    onRemove={() =>
                      setOrganizationForm((previous) => ({
                        ...previous,
                        departments: previous.departments.filter((_, itemIndex) => itemIndex !== index)
                      }))
                    }
                    removeTestId={`app-setup-remove-department-${index}`}
                  >
                    <Input
                      data-testid={`app-setup-department-name-${index}`}
                      placeholder={t("setupDepartmentNamePlaceholder")}
                      value={department.name}
                      onChange={(value) =>
                        setOrganizationForm((previous) => ({
                          ...previous,
                          departments: previous.departments.map((item, itemIndex) =>
                            itemIndex === index ? { ...item, name: value } : item
                          )
                        }))
                      }
                    />
                    <Input
                      data-testid={`app-setup-department-code-${index}`}
                      placeholder={t("setupDepartmentCodePlaceholder")}
                      value={department.code ?? ""}
                      onChange={(value) =>
                        setOrganizationForm((previous) => ({
                          ...previous,
                          departments: previous.departments.map((item, itemIndex) =>
                            itemIndex === index ? { ...item, code: value } : item
                          )
                        }))
                      }
                    />
                    <Input
                      data-testid={`app-setup-department-parent-${index}`}
                      placeholder={t("setupDepartmentParentPlaceholder")}
                      value={department.parentCode ?? ""}
                      onChange={(value) =>
                        setOrganizationForm((previous) => ({
                          ...previous,
                          departments: previous.departments.map((item, itemIndex) =>
                            itemIndex === index ? { ...item, parentCode: value } : item
                          )
                        }))
                      }
                    />
                    <InputNumber
                      data-testid={`app-setup-department-sort-${index}`}
                      value={department.sortOrder}
                      onChange={(value) => {
                        const numericValue =
                          typeof value === "number" ? value : Number.parseInt(String(value ?? "0"), 10) || 0;
                        setOrganizationForm((previous) => ({
                          ...previous,
                          departments: previous.departments.map((item, itemIndex) =>
                            itemIndex === index ? { ...item, sortOrder: numericValue } : item
                          )
                        }));
                      }}
                    />
                  </ConfigRow>
                ))}
              </div>
            </SectionCard>

            <SectionCard
              title={t("setupPositionSectionTitle")}
              subtitle={t("setupPositionSectionDesc")}
              actions={
                <Button
                  type="tertiary"
                  theme="light"
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
                </Button>
              }
            >
              <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                {organizationForm.positions.map((position, index) => (
                  <ConfigRow
                    key={`position-${index}`}
                    canRemove={organizationForm.positions.length > 1}
                    onRemove={() =>
                      setOrganizationForm((previous) => ({
                        ...previous,
                        positions: previous.positions.filter((_, itemIndex) => itemIndex !== index)
                      }))
                    }
                    removeTestId={`app-setup-remove-position-${index}`}
                  >
                    <Input
                      data-testid={`app-setup-position-name-${index}`}
                      placeholder={t("setupPositionNamePlaceholder")}
                      value={position.name}
                      onChange={(value) =>
                        setOrganizationForm((previous) => ({
                          ...previous,
                          positions: previous.positions.map((item, itemIndex) =>
                            itemIndex === index ? { ...item, name: value } : item
                          )
                        }))
                      }
                    />
                    <Input
                      data-testid={`app-setup-position-code-${index}`}
                      placeholder={t("setupPositionCodePlaceholder")}
                      value={position.code}
                      onChange={(value) =>
                        setOrganizationForm((previous) => ({
                          ...previous,
                          positions: previous.positions.map((item, itemIndex) =>
                            itemIndex === index ? { ...item, code: value } : item
                          )
                        }))
                      }
                    />
                    <Input
                      data-testid={`app-setup-position-description-${index}`}
                      placeholder={t("setupPositionDescriptionPlaceholder")}
                      value={position.description ?? ""}
                      onChange={(value) =>
                        setOrganizationForm((previous) => ({
                          ...previous,
                          positions: previous.positions.map((item, itemIndex) =>
                            itemIndex === index ? { ...item, description: value } : item
                          )
                        }))
                      }
                    />
                    <InputNumber
                      data-testid={`app-setup-position-sort-${index}`}
                      value={position.sortOrder}
                      onChange={(value) => {
                        const numericValue =
                          typeof value === "number" ? value : Number.parseInt(String(value ?? "0"), 10) || 0;
                        setOrganizationForm((previous) => ({
                          ...previous,
                          positions: previous.positions.map((item, itemIndex) =>
                            itemIndex === index ? { ...item, sortOrder: numericValue } : item
                          )
                        }));
                      }}
                    />
                  </ConfigRow>
                ))}
              </div>
            </SectionCard>

            <StepActions>
              <Button
                type="tertiary"
                theme="light"
                data-testid="app-setup-back-to-roles"
                onClick={() => setCurrentStep(2)}
              >
                {t("setupPrev")}
              </Button>
              <Button
                type="primary"
                theme="solid"
                data-testid="app-setup-initialize"
                disabled={!organizationFormValid || initializing}
                loading={initializing}
                onClick={() => void handleInitialize()}
              >
                {initializing ? t("setupInitializing") : t("setupStartInitialization")}
              </Button>
            </StepActions>
          </div>
        ) : null}

        {currentStep === 4 && completed ? (
          <div data-testid="app-setup-success">
            <ResultCard
              status="success"
              title={t("setupAppSetupComplete")}
              description={t("setupAppSetupCompleteDesc")}
              extra={
                initReport ? (
                  <div style={{ marginTop: 8 }}>
                    <Descriptions
                      data={[
                        {
                          key: t("setupPlatformStatus"),
                          value: (
                            <span data-testid="app-setup-report-platform-status">
                              {initReport.platformStatus}
                            </span>
                          )
                        },
                        {
                          key: t("setupAppStatus"),
                          value: <span data-testid="app-setup-report-app-status">{initReport.appStatus}</span>
                        },
                        {
                          key: t("setupAppSetupCompleted"),
                          value: (
                            <span data-testid="app-setup-report-app-completed">
                              {formatBooleanFlag(initReport.appSetupCompleted)}
                            </span>
                          )
                        },
                        {
                          key: t("setupDbConnected"),
                          value: (
                            <span data-testid="app-setup-report-db-connected">
                              {formatBooleanFlag(initReport.databaseConnected)}
                            </span>
                          )
                        },
                        {
                          key: t("setupCoreTablesVerified"),
                          value: (
                            <span data-testid="app-setup-report-core-tables">
                              {formatBooleanFlag(initReport.coreTablesVerified)}
                            </span>
                          )
                        },
                        {
                          key: t("setupReportRoles"),
                          value: (
                            <span data-testid="app-setup-report-roles-created">
                              {initReport.rolesCreated}
                            </span>
                          )
                        },
                        {
                          key: t("setupReportDepartments"),
                          value: (
                            <span data-testid="app-setup-report-departments-created">
                              {initReport.departmentsCreated}
                            </span>
                          )
                        },
                        {
                          key: t("setupReportPositions"),
                          value: (
                            <span data-testid="app-setup-report-positions-created">
                              {initReport.positionsCreated}
                            </span>
                          )
                        },
                        {
                          key: t("setupReportAdmin"),
                          value: (
                            <span data-testid="app-setup-report-admin-bound">
                              {formatBooleanFlag(initReport.adminBound)}
                            </span>
                          )
                        }
                      ]}
                    />
                  </div>
                ) : null
              }
              actions={
                <Button
                  type="primary"
                  theme="solid"
                  size="large"
                  data-testid="app-setup-enter-workspace"
                  onClick={enterWorkspace}
                >
                  {t("setupGoToLogin")}
                </Button>
              }
            />
            <div style={{ marginTop: 16 }}>
              <InfoBanner
                variant="warning"
                title={t("setupRestartRequired")}
                description={t("setupRestartRequiredDesc")}
              />
            </div>
          </div>
        ) : null}

        {currentStep === 4 && setupError ? (
          <div data-testid="app-setup-failed">
            <ResultCard
              status="error"
              title={t("setupAppSetupFailed")}
              description={setupError}
              actions={
                <Button
                  type="primary"
                  theme="solid"
                  onClick={() => {
                    setSetupError(null);
                    setCompleted(false);
                    setCurrentStep(0);
                  }}
                >
                  {t("platformNotReadyRetry")}
                </Button>
              }
            />
          </div>
        ) : null}

        <div aria-hidden="true" style={{ display: "none" }}>
          {locale}
        </div>
      </FormCard>
    </PageShell>
  );
}

interface FieldLabelProps {
  label: React.ReactNode;
  children: React.ReactNode;
}

function FieldLabel({ label, children }: FieldLabelProps) {
  return (
    <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <Text strong>{label}</Text>
      {children}
    </label>
  );
}

interface StepActionsProps {
  children: React.ReactNode;
}

function StepActions({ children }: StepActionsProps) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        marginTop: 8,
        gap: 12
      }}
    >
      {children}
    </div>
  );
}

interface ConfigRowProps {
  canRemove: boolean;
  onRemove: () => void;
  removeTestId: string;
  children: React.ReactNode;
}

function ConfigRow({ canRemove, onRemove, removeTestId, children }: ConfigRowProps) {
  const { t } = useAppI18n();
  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: "1fr 1fr 1fr 100px auto",
        gap: 8,
        alignItems: "center"
      }}
    >
      {children}
      {canRemove ? (
        <Button type="danger" theme="borderless" data-testid={removeTestId} onClick={onRemove}>
          {t("setupRemoveRow")}
        </Button>
      ) : (
        <span />
      )}
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

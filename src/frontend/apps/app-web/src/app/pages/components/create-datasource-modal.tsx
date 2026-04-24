import { useEffect, useMemo, useState } from "react";
import { Button, Input, Modal, Select, Spin, Tabs, Toast, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../../i18n";
import {
  createTenantDataSource,
  getTenantDataSource,
  getTenantDataSourceDrivers,
  type DataSourceDriverDefinition,
  type TenantDataSourceCreateRequest,
  type TenantDataSourceDto,
  testTenantDataSourceConnection
} from "../../../services/api-tenant-datasource";

const DEFAULT_MODE = "raw";

export interface CreateDataSourceModalProps {
  visible: boolean;
  title?: string;
  initialName?: string;
  onClose: () => void;
  onCreated: (dataSource: TenantDataSourceDto) => void | Promise<void>;
}

function buildSignature(request: TenantDataSourceCreateRequest): string {
  return JSON.stringify(request);
}

function normalizeVisualConfig(config: Record<string, string>): Record<string, string> {
  return Object.fromEntries(
    Object.entries(config)
      .map(([key, value]) => [key, value.trim()])
      .filter(([, value]) => value.length > 0)
  );
}

export function CreateDataSourceModal({
  visible,
  title,
  initialName,
  onClose,
  onCreated
}: CreateDataSourceModalProps) {
  const { t } = useAppI18n();
  const [loadingDrivers, setLoadingDrivers] = useState(false);
  const [drivers, setDrivers] = useState<DataSourceDriverDefinition[]>([]);
  const [driverCode, setDriverCode] = useState("");
  const [name, setName] = useState("");
  const [mode, setMode] = useState<"raw" | "visual">(DEFAULT_MODE);
  const [connectionString, setConnectionString] = useState("");
  const [visualConfig, setVisualConfig] = useState<Record<string, string>>({});
  const [testing, setTesting] = useState(false);
  const [saving, setSaving] = useState(false);
  const [testPassed, setTestPassed] = useState(false);
  const [testMessage, setTestMessage] = useState<string | null>(null);
  const [testedSignature, setTestedSignature] = useState<string | null>(null);

  const selectedDriver = useMemo(
    () => drivers.find(item => item.code === driverCode) ?? drivers[0] ?? null,
    [driverCode, drivers]
  );

  useEffect(() => {
    if (!visible) {
      return;
    }

    let cancelled = false;
    setLoadingDrivers(true);
    void getTenantDataSourceDrivers()
      .then(items => {
        if (cancelled) {
          return;
        }
        setDrivers(items);
        const firstDriver = items[0]?.code ?? "";
        setDriverCode(current =>
          current && items.some(item => item.code === current) ? current : firstDriver
        );
      })
      .catch(error => {
        if (!cancelled) {
          Toast.error(error instanceof Error ? error.message : t("cozeLibraryQueryFailed"));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoadingDrivers(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [t, visible]);

  useEffect(() => {
    if (!visible) {
      return;
    }

    setName(initialName ?? "");
    setMode(DEFAULT_MODE);
    setConnectionString("");
    setVisualConfig({});
    setTestPassed(false);
    setTestMessage(null);
    setTestedSignature(null);
  }, [initialName, visible]);

  useEffect(() => {
    setTestPassed(false);
    setTestedSignature(null);
  }, [connectionString, driverCode, mode, visualConfig]);

  const buildCreateRequest = (): TenantDataSourceCreateRequest => {
    const trimmedName = name.trim();
    if (!trimmedName) {
      throw new Error(t("cozeLibraryCreateNameRequired"));
    }

    const request: TenantDataSourceCreateRequest = {
      name: trimmedName,
      dbType: driverCode,
      mode,
      ownershipScope: "Platform",
      connectionString: mode === "raw" ? connectionString.trim() : "",
      visualConfig: mode === "visual" ? normalizeVisualConfig(visualConfig) : undefined,
      maxPoolSize: 50,
      connectionTimeoutSeconds: 15
    };

    if (mode === "raw" && !request.connectionString) {
      throw new Error(t("setupConsoleMigrationConnectionStringLabel"));
    }

    return request;
  };

  const ensureTestPassed = async (): Promise<TenantDataSourceCreateRequest> => {
    const request = buildCreateRequest();
    const signature = buildSignature(request);
    if (testPassed && testedSignature === signature) {
      return request;
    }

    setTesting(true);
    try {
      const result = await testTenantDataSourceConnection({
        dbType: driverCode,
        mode,
        connectionString: mode === "raw" ? request.connectionString : undefined,
        visualConfig: mode === "visual" ? request.visualConfig ?? undefined : undefined
      });
      const passed = Boolean(result.success);
      const message = result.success ? t("setupTestSuccess") : (result.errorMessage ?? t("loginFailed"));
      setTestPassed(passed);
      setTestMessage(message);
      setTestedSignature(signature);
      if (!passed) {
        throw new Error(message);
      }

      return request;
    } finally {
      setTesting(false);
    }
  };

  const handleTest = async () => {
    try {
      const request = buildCreateRequest();
      setTesting(true);
      const result = await testTenantDataSourceConnection({
        dbType: driverCode,
        mode,
        connectionString: mode === "raw" ? request.connectionString : undefined,
        visualConfig: mode === "visual" ? request.visualConfig ?? undefined : undefined
      });
      const passed = Boolean(result.success);
      const message = result.success ? t("setupTestSuccess") : (result.errorMessage ?? t("loginFailed"));
      setTestPassed(passed);
      setTestMessage(message);
      setTestedSignature(buildSignature(request));
      if (passed) {
        Toast.success(message);
      } else {
        Toast.error(message);
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : t("cozeLibraryQueryFailed");
      setTestPassed(false);
      setTestMessage(message);
      Toast.error(message);
    } finally {
      setTesting(false);
    }
  };

  const handleSave = async () => {
    try {
      setSaving(true);
      const request = await ensureTestPassed();
      const id = await createTenantDataSource(request);
      const created = await getTenantDataSource(id);
      await onCreated(created);
      Toast.success(t("cozeLibraryCreateSuccess"));
      onClose();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : t("cozeLibraryQueryFailed"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      title={title ?? t("cozeLibraryCreateDataSourceTitle")}
      visible={visible}
      onCancel={onClose}
      onOk={() => void handleSave()}
      okText={t("cozeCommonConfirm")}
      cancelText={t("cozeCommonCancel")}
      confirmLoading={saving}
      width={760}
    >
      {loadingDrivers ? (
        <div style={{ display: "flex", justifyContent: "center", padding: 48 }}>
          <Spin />
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            <Typography.Text strong>{t("cozeLibraryCreateName")}</Typography.Text>
            <Input value={name} onChange={setName} placeholder={t("cozeLibraryCreateNameRequired")} />
          </label>

          <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            <Typography.Text strong>{t("setupDatabaseDriver")}</Typography.Text>
            <Select
              value={driverCode}
              onChange={value => setDriverCode(String(value ?? ""))}
              optionList={drivers.map(item => ({
                value: item.code,
                label: `${item.displayName} (${item.code})`
              }))}
            />
          </label>

          <Tabs
            type="button"
            activeKey={mode}
            onChange={key => setMode((key as "raw" | "visual") ?? DEFAULT_MODE)}
            tabList={[
              { itemKey: "raw", tab: t("setupModeRaw") },
              { itemKey: "visual", tab: t("setupModeVisual"), disabled: selectedDriver?.supportsVisual === false }
            ]}
          />

          {mode === "raw" ? (
            <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              <Typography.Text strong>{t("setupConnectionString")}</Typography.Text>
              <Input.TextArea
                value={connectionString}
                onChange={setConnectionString}
                autosize={{ minRows: 4, maxRows: 8 }}
                placeholder={selectedDriver?.connectionStringExample ?? ""}
              />
            </label>
          ) : (
            <div style={{ display: "grid", gridTemplateColumns: "repeat(2, minmax(0, 1fr))", gap: 12 }}>
              {selectedDriver?.fields.map(field => (
                <label
                  key={field.key}
                  style={{
                    display: "flex",
                    flexDirection: "column",
                    gap: 6,
                    gridColumn: field.multiline ? "1 / -1" : undefined
                  }}
                >
                  <Typography.Text strong>
                    {field.label}
                    {field.required ? " *" : ""}
                  </Typography.Text>
                  {field.multiline ? (
                    <Input.TextArea
                      value={visualConfig[field.key] ?? ""}
                      onChange={value =>
                        setVisualConfig(current => ({
                          ...current,
                          [field.key]: value
                        }))
                      }
                      autosize={{ minRows: 3, maxRows: 6 }}
                      placeholder={field.placeholder ?? undefined}
                    />
                  ) : (
                    <Input
                      value={visualConfig[field.key] ?? ""}
                      onChange={value =>
                        setVisualConfig(current => ({
                          ...current,
                          [field.key]: value
                        }))
                      }
                      type={field.secret ? "password" : "text"}
                      placeholder={field.placeholder ?? undefined}
                    />
                  )}
                </label>
              ))}
            </div>
          )}

          <div
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              gap: 12,
              padding: 12,
              borderRadius: 8,
              background: "var(--semi-color-fill-0)"
            }}
          >
            <Typography.Text type={testPassed ? "success" : "tertiary"}>
              {testMessage ?? t("cozeLibraryCreateDataSourceNeedTest")}
            </Typography.Text>
            <Button loading={testing} onClick={() => void handleTest()}>
              {t("setupTestConnection")}
            </Button>
          </div>
        </div>
      )}
    </Modal>
  );
}

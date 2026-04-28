import * as React from "react";
import { useEffect, useMemo, useState } from "react";
import { useParams, useSearchParams } from "react-router-dom";
import { Banner, Button, Card, Empty, Select, Spin, Switch, Tabs, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { intelligenceApi } from "@coze-arch/bot-api";
import {
  type PublishRecordDetail
} from "@coze-arch/bot-api/intelligence_api";
import { useAppI18n } from "../i18n";
import {
  createCozePublishTrigger,
  deleteCozePublishTrigger,
  getCozePublishLogList,
  type CozePublishLogItem,
  getCozePublishTriggerList,
  type CozePublishTriggerItem,
  updateCozePublishTrigger
} from "../../services/api-coze-publish-manage";
import { buildDefaultTriggerFormValues, normalizeTriggerConfigJson } from "./coze-trigger-form-helpers";
import {
  formatPublishTime,
  getConnectorStatusKey,
  getPublishStatusKey,
  normalizePublishManageTab,
  summarizeConnectors
} from "./coze-agent-publish-manage-helpers";
import { buildDefaultPublishLogFilters, normalizePositiveInt } from "./coze-publish-log-helpers";
import { buildPublishLogPreview, formatPublishLogPayload } from "./coze-publish-log-payload-helpers";

const { Title, Text } = Typography;

export function CozeAgentPublishManagePage() {
  const { bot_id: botId = "" } = useParams<{ bot_id: string }>();
  const { t } = useAppI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);
  const [records, setRecords] = useState<PublishRecordDetail[]>([]);
  const [currentRecord, setCurrentRecord] = useState<PublishRecordDetail>();
  const [triggers, setTriggers] = useState<CozePublishTriggerItem[]>([]);
  const [logs, setLogs] = useState<CozePublishLogItem[]>([]);
  const [triggerLoadFailed, setTriggerLoadFailed] = useState(false);
  const [logLoadFailed, setLogLoadFailed] = useState(false);
  const [triggerActionKey, setTriggerActionKey] = useState("");
  const [expandedLogId, setExpandedLogId] = useState("");

  const recordIdFromQuery = searchParams.get("recordId")?.trim() ?? "";
  const activeTab = normalizePublishManageTab(searchParams.get("tab"));
  const logFilters = {
    source: searchParams.get("logSource")?.trim() ?? buildDefaultPublishLogFilters().source,
    kind: searchParams.get("logKind")?.trim() ?? buildDefaultPublishLogFilters().kind,
    pageIndex: normalizePositiveInt(searchParams.get("logPage"), buildDefaultPublishLogFilters().pageIndex),
    pageSize: normalizePositiveInt(searchParams.get("logPageSize"), buildDefaultPublishLogFilters().pageSize)
  };

  useEffect(() => {
    let cancelled = false;

    async function load() {
      if (!botId) {
        setRecords([]);
        setCurrentRecord(undefined);
        setLoading(false);
        return;
      }

      setLoading(true);
      setLoadFailed(false);

      try {
        const listResponse = await intelligenceApi.GetPublishRecordList({ project_id: botId });
        if (cancelled) {
          return;
        }

        const nextRecords = listResponse.data ?? [];
        setRecords(nextRecords);

        const targetRecordId = recordIdFromQuery || nextRecords[0]?.publish_record_id || "";
        if (!targetRecordId) {
          setCurrentRecord(undefined);
          return;
        }

        const detailResponse = await intelligenceApi.GetPublishRecordDetail({
          project_id: botId,
          publish_record_id: targetRecordId
        });
        if (cancelled) {
          return;
        }

        setCurrentRecord(
          detailResponse.data ??
            nextRecords.find(item => item.publish_record_id === targetRecordId) ??
            nextRecords[0]
        );
      } catch {
        if (!cancelled) {
          setLoadFailed(true);
          setRecords([]);
          setCurrentRecord(undefined);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [botId, recordIdFromQuery]);

  useEffect(() => {
    let cancelled = false;

    async function loadTriggers() {
      if (!botId) {
        setTriggers([]);
        setTriggerLoadFailed(false);
        return;
      }

      try {
        const nextTriggers = await getCozePublishTriggerList(botId);
        if (!cancelled) {
          setTriggers(nextTriggers);
          setTriggerLoadFailed(false);
        }
      } catch {
        if (!cancelled) {
          setTriggers([]);
          setTriggerLoadFailed(true);
        }
      }
    }

    void loadTriggers();

    return () => {
      cancelled = true;
    };
  }, [botId]);

  useEffect(() => {
    let cancelled = false;

    async function loadLogs() {
      if (!botId) {
        setLogs([]);
        setLogLoadFailed(false);
        return;
      }

      try {
        const nextLogs = await getCozePublishLogList({
          projectId: botId,
          source: logFilters.source || undefined,
          kind: logFilters.kind || undefined,
          pageIndex: logFilters.pageIndex,
          pageSize: logFilters.pageSize
        });
        if (!cancelled) {
          setLogs(nextLogs);
          setLogLoadFailed(false);
        }
      } catch {
        if (!cancelled) {
          setLogs([]);
          setLogLoadFailed(true);
        }
      }
    }

    void loadLogs();

    return () => {
      cancelled = true;
    };
  }, [botId, logFilters.kind, logFilters.pageIndex, logFilters.pageSize, logFilters.source]);

  const updateLogFilters = (next: Partial<typeof logFilters>) => {
    const nextParams = new URLSearchParams(searchParams);
    const merged = {
      ...logFilters,
      ...next
    };

    if (merged.source) {
      nextParams.set("logSource", merged.source);
    } else {
      nextParams.delete("logSource");
    }

    if (merged.kind) {
      nextParams.set("logKind", merged.kind);
    } else {
      nextParams.delete("logKind");
    }

    nextParams.set("logPage", String(merged.pageIndex));
    nextParams.set("logPageSize", String(merged.pageSize));
    setSearchParams(nextParams, { replace: true });
  };

  const connectorSummary = useMemo(
    () => summarizeConnectors(currentRecord?.connector_publish_result),
    [currentRecord?.connector_publish_result]
  );

  const selectedRecordId = currentRecord?.publish_record_id ?? recordIdFromQuery ?? "";
  const publishStatusKey = getPublishStatusKey(currentRecord?.publish_status);
  const publishStatus = publishStatusKey
    ? {
        color:
          publishStatusKey === "cozePublishManageStatusSuccess"
            ? ("green" as const)
            : publishStatusKey === "cozePublishManageStatusFailed"
              ? ("red" as const)
              : ("orange" as const),
        label: t(publishStatusKey)
      }
    : { color: "grey" as const, label: "-" };
  const connectors = currentRecord?.connector_publish_result ?? [];

  const handleTabChange = (key: string) => {
    const nextTab = normalizePublishManageTab(key);
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("tab", nextTab);
    setSearchParams(nextParams, { replace: true });
  };

  const handleRecordChange = (value: string) => {
    const nextParams = new URLSearchParams(searchParams);
    if (value) {
      nextParams.set("recordId", value);
    } else {
      nextParams.delete("recordId");
    }
    setSearchParams(nextParams, { replace: true });
  };

  const refreshTriggers = async () => {
    if (!botId) {
      setTriggers([]);
      return;
    }

    const nextTriggers = await getCozePublishTriggerList(botId);
    setTriggers(nextTriggers);
    setTriggerLoadFailed(false);
  };

  const handleToggleTrigger = async (trigger: CozePublishTriggerItem, enabled: boolean) => {
    try {
      setTriggerActionKey(`toggle:${trigger.trigger_id}`);
      await updateCozePublishTrigger({
        projectId: botId,
        triggerId: trigger.trigger_id,
        name: trigger.name,
        triggerType: trigger.trigger_type,
        configJson: trigger.config_json,
        enabled
      });
      await refreshTriggers();
      Toast.success(t("cozePublishManageTriggerEnableSuccess"));
    } catch (error) {
      Toast.error((error as Error).message || t("cozePublishManageLoadFailed"));
    } finally {
      setTriggerActionKey("");
    }
  };

  const handleDeleteTrigger = async (trigger: CozePublishTriggerItem) => {
    try {
      setTriggerActionKey(`delete:${trigger.trigger_id}`);
      await deleteCozePublishTrigger(botId, trigger.trigger_id);
      await refreshTriggers();
      Toast.success(t("cozePublishManageTriggerDeleteSuccess"));
    } catch (error) {
      Toast.error((error as Error).message || t("cozePublishManageLoadFailed"));
    } finally {
      setTriggerActionKey("");
    }
  };

  const handleSubmitTrigger = async (
    values: { name: string; triggerType: string; configJson: string; enabled: boolean },
    editingTrigger?: CozePublishTriggerItem | null
  ) => {
    try {
      setTriggerActionKey(editingTrigger ? `edit:${editingTrigger.trigger_id}` : "create");
      if (editingTrigger) {
        await updateCozePublishTrigger({
          projectId: botId,
          triggerId: editingTrigger.trigger_id,
          name: values.name,
          triggerType: values.triggerType,
          configJson: values.configJson,
          enabled: values.enabled
        });
        Toast.success(t("cozePublishManageTriggerUpdateSuccess"));
      } else {
        await createCozePublishTrigger({
          projectId: botId,
          name: values.name,
          triggerType: values.triggerType,
          configJson: values.configJson,
          enabled: values.enabled
        });
        Toast.success(t("cozePublishManageTriggerCreateSuccess"));
      }
      await refreshTriggers();
    } catch (error) {
      Toast.error((error as Error).message || t("cozePublishManageLoadFailed"));
    } finally {
      setTriggerActionKey("");
    }
  };

  const openCreateTrigger = async () => {
    const defaults = buildDefaultTriggerFormValues();
    const name = typeof window !== "undefined"
      ? window.prompt(t("cozePublishManageTriggerName"), defaults.name)
      : defaults.name;
    if (!name || !name.trim()) {
      return;
    }
    const triggerType = typeof window !== "undefined"
      ? window.prompt(t("cozePublishManageTriggerType"), defaults.triggerType)
      : defaults.triggerType;
    const configJson = typeof window !== "undefined"
      ? window.prompt(t("cozePublishManageTriggerConfig"), defaults.configJson)
      : defaults.configJson;
    await handleSubmitTrigger({
      name: name.trim(),
      triggerType: (triggerType || defaults.triggerType).trim(),
      configJson: normalizeTriggerConfigJson(configJson || defaults.configJson),
      enabled: defaults.enabled
    });
  };

  const openEditTrigger = async (trigger: CozePublishTriggerItem) => {
    const name = typeof window !== "undefined"
      ? window.prompt(t("cozePublishManageTriggerName"), trigger.name)
      : trigger.name;
    if (!name || !name.trim()) {
      return;
    }
    const triggerType = typeof window !== "undefined"
      ? window.prompt(t("cozePublishManageTriggerType"), trigger.trigger_type || "schedule")
      : trigger.trigger_type;
    const configJson = typeof window !== "undefined"
      ? window.prompt(t("cozePublishManageTriggerConfig"), normalizeTriggerConfigJson(trigger.config_json))
      : trigger.config_json;
    await handleSubmitTrigger({
      name: name.trim(),
      triggerType: (triggerType || trigger.trigger_type || "schedule").trim(),
      configJson: normalizeTriggerConfigJson(configJson || trigger.config_json),
      enabled: trigger.enabled
    }, trigger);
  };

  return (
    <div style={{ maxWidth: 960, margin: "0 auto", padding: 24 }}>
      <div style={{ marginBottom: 16 }}>
        <Title heading={3} style={{ marginBottom: 4 }}>
          {t("cozePublishManageTitle")}
        </Title>
        <Text type="tertiary">{t("cozePublishManageSubtitle")}</Text>
      </div>

      {loadFailed ? (
        <Banner type="danger" fullMode={false} bordered description={t("cozePublishManageLoadFailed")} />
      ) : null}

      <Spin spinning={loading}>
        {!loading && !currentRecord ? (
          <Card bodyStyle={{ padding: 32 }}>
            <Empty description={t("cozePublishManageNoRecord")} />
          </Card>
        ) : (
          <div style={{ display: "grid", gap: 16 }}>
            <Card bodyStyle={{ padding: 16 }}>
              <div style={{ display: "flex", justifyContent: "space-between", gap: 16, flexWrap: "wrap", alignItems: "center" }}>
                <div style={{ display: "flex", gap: 24, flexWrap: "wrap", alignItems: "center" }}>
                  <div>
                    <Text type="tertiary">{t("cozePublishManageVersion")}</Text>
                    <div style={{ marginTop: 4 }}>
                      <Text strong>{currentRecord?.version_number ?? "-"}</Text>
                    </div>
                  </div>
                  <div>
                    <Text type="tertiary">{t("cozePublishManagePublishedAt")}</Text>
                    <div style={{ marginTop: 4 }}>
                      <Text strong>{formatPublishTime((currentRecord as PublishRecordDetail & { publish_time?: string } | undefined)?.publish_time)}</Text>
                    </div>
                  </div>
                  <div>
                    <Text type="tertiary">{t("cozePublishManageStatus")}</Text>
                    <div style={{ marginTop: 4 }}>
                      <Tag color={publishStatus.color}>{publishStatus.label}</Tag>
                    </div>
                  </div>
                </div>
                <div style={{ minWidth: 240 }}>
                  <Select
                    placeholder={t("cozePublishManageVersion")}
                    value={selectedRecordId}
                    optionList={records.map(item => ({
                      label: item.version_number ?? item.publish_record_id ?? "-",
                      value: item.publish_record_id ?? ""
                    }))}
                    onChange={value => {
                      if (typeof value === "string") {
                        handleRecordChange(value);
                      }
                    }}
                  />
                </div>
              </div>
            </Card>

            <Tabs activeKey={activeTab} onChange={handleTabChange} type="line">
              <Tabs.TabPane tab={t("cozePublishManageTabAnalysis")} itemKey="analysis">
                <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: 16 }}>
                  <Card bodyStyle={{ padding: 16 }}>
                    <Text type="tertiary">{t("cozePublishManageConnectorCount")}</Text>
                    <div style={{ marginTop: 8 }}>
                      <Title heading={4}>{connectorSummary.total}</Title>
                    </div>
                  </Card>
                  <Card bodyStyle={{ padding: 16 }}>
                    <Text type="tertiary">{t("cozePublishManageSuccessCount")}</Text>
                    <div style={{ marginTop: 8 }}>
                      <Title heading={4}>{connectorSummary.successCount}</Title>
                    </div>
                  </Card>
                  <Card bodyStyle={{ padding: 16 }}>
                    <Text type="tertiary">{t("cozePublishManageFailedCount")}</Text>
                    <div style={{ marginTop: 8 }}>
                      <Title heading={4}>{connectorSummary.failedCount}</Title>
                    </div>
                  </Card>
                </div>
              </Tabs.TabPane>

              <Tabs.TabPane tab={t("cozePublishManageTabLogs")} itemKey="logs">
                <div style={{ display: "grid", gap: 12 }}>
                  {logLoadFailed ? (
                    <Banner type="danger" fullMode={false} bordered description={t("cozePublishManageLoadFailed")} />
                  ) : null}
                  <Card bodyStyle={{ padding: 16 }}>
                    <div style={{ display: "grid", gap: 12 }}>
                      <Text strong>{t("cozePublishManageHistory")}</Text>
                      {records.map(record => {
                        const recordStatusKey = getPublishStatusKey(record.publish_status);
                        const recordStatus = recordStatusKey
                          ? {
                              color:
                                recordStatusKey === "cozePublishManageStatusSuccess"
                                  ? ("green" as const)
                                  : recordStatusKey === "cozePublishManageStatusFailed"
                                    ? ("red" as const)
                                    : ("orange" as const),
                              label: t(recordStatusKey)
                            }
                          : { color: "grey" as const, label: "-" };
                        const publishTime = formatPublishTime((record as PublishRecordDetail & { publish_time?: string }).publish_time);
                        return (
                          <div
                            key={record.publish_record_id ?? record.version_number ?? publishTime}
                            style={{
                              display: "grid",
                              gap: 8,
                              padding: 12,
                              borderRadius: 12,
                              border: "1px solid var(--semi-color-border)"
                            }}
                          >
                            <div style={{ display: "flex", justifyContent: "space-between", gap: 12, flexWrap: "wrap", alignItems: "center" }}>
                              <Text strong>{record.version_number ?? "-"}</Text>
                              <Tag color={recordStatus.color}>{recordStatus.label}</Tag>
                            </div>
                            <div>
                              <Text type="tertiary">{t("cozePublishManagePublishedAt")}</Text>
                              <div style={{ marginTop: 4 }}>
                                <Text>{publishTime}</Text>
                              </div>
                            </div>
                            <div>
                              <Text type="tertiary">{t("cozePublishManageReleaseNote")}</Text>
                              <div style={{ marginTop: 4 }}>
                                <Text>{record.publish_status_msg || "-"}</Text>
                              </div>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </Card>
                  <Card bodyStyle={{ padding: 16 }}>
                    <div style={{ display: "grid", gap: 12 }}>
                      <Text strong>{t("cozePublishManageTabLogs")}</Text>
                      <div style={{ display: "flex", gap: 12, flexWrap: "wrap" }}>
                        <Select
                          style={{ minWidth: 180 }}
                          placeholder={t("cozePublishManageLogFilterSource")}
                          value={logFilters.source || undefined}
                          optionList={[
                            { label: "agent", value: "agent" },
                            { label: "dispatch", value: "dispatch" },
                            { label: "workflow", value: "workflow" }
                          ]}
                          onChange={value => {
                            updateLogFilters({
                              source: typeof value === "string" ? value : "",
                              pageIndex: 1
                            });
                          }}
                        />
                        <Select
                          style={{ minWidth: 180 }}
                          placeholder={t("cozePublishManageLogFilterKind")}
                          value={logFilters.kind || undefined}
                          optionList={[
                            { label: "publish", value: "publish" },
                            { label: "message", value: "message" },
                            { label: "tool", value: "tool" }
                          ]}
                          onChange={value => {
                            updateLogFilters({
                              kind: typeof value === "string" ? value : "",
                              pageIndex: 1
                            });
                          }}
                        />
                        <Select
                          style={{ minWidth: 140 }}
                          placeholder={t("cozePublishManageLogPageSize")}
                          value={String(logFilters.pageSize)}
                          optionList={[
                            { label: "10", value: "10" },
                            { label: "20", value: "20" },
                            { label: "50", value: "50" }
                          ]}
                          onChange={value => {
                            updateLogFilters({
                              pageSize: normalizePositiveInt(typeof value === "string" ? value : String(logFilters.pageSize), 20),
                              pageIndex: 1
                            });
                          }}
                        />
                        <Button
                          disabled={logFilters.pageIndex <= 1}
                          onClick={() => {
                            updateLogFilters({
                              pageIndex: Math.max(1, logFilters.pageIndex - 1)
                            });
                          }}
                        >
                          {t("cozePublishManageLogPrevPage")}
                        </Button>
                        <Button
                          disabled={logs.length < logFilters.pageSize}
                          onClick={() => {
                            updateLogFilters({
                              pageIndex: logFilters.pageIndex + 1
                            });
                          }}
                        >
                          {t("cozePublishManageLogNextPage")}
                        </Button>
                      </div>
                      {logs.length === 0 ? (
                        <Empty description={t("cozePublishManageNoRuntimeLogs")} />
                      ) : (
                        logs.map(item => (
                          <div
                            key={item.log_id}
                            style={{
                              display: "grid",
                              gap: 8,
                              padding: 12,
                              borderRadius: 12,
                              border: "1px solid var(--semi-color-border)"
                            }}
                          >
                            <div style={{ display: "flex", justifyContent: "space-between", gap: 12, flexWrap: "wrap", alignItems: "center" }}>
                              <Text strong>{item.log_id}</Text>
                              <Text type="tertiary">{formatPublishTime(item.occurred_at)}</Text>
                            </div>
                            <div>
                              <Text type="tertiary">{t("cozePublishManageLogSource")}</Text>
                              <div style={{ marginTop: 4 }}>
                                <Text>{item.source || "-"}</Text>
                              </div>
                            </div>
                            <div>
                              <Text type="tertiary">{t("cozePublishManageLogKind")}</Text>
                              <div style={{ marginTop: 4 }}>
                                <Text>{item.kind || "-"}</Text>
                              </div>
                            </div>
                            <div>
                              <Text type="tertiary">Trace</Text>
                              <div style={{ marginTop: 4 }}>
                                <Text>{item.trace_id || "-"}</Text>
                              </div>
                            </div>
                            <div>
                              <Text type="tertiary">{t("cozePublishManageLogPayload")}</Text>
                              <div style={{ marginTop: 4, display: "grid", gap: 8 }}>
                                <Text>{buildPublishLogPreview(item.payload)}</Text>
                                <Button
                                  theme="borderless"
                                  style={{ paddingLeft: 0, justifyContent: "flex-start" }}
                                  onClick={() => {
                                    setExpandedLogId(current => current === item.log_id ? "" : item.log_id);
                                  }}
                                >
                                  {expandedLogId === item.log_id
                                    ? t("cozePublishManageLogPayloadHide")
                                    : t("cozePublishManageLogPayloadView")}
                                </Button>
                                {expandedLogId === item.log_id ? (
                                  <pre
                                    style={{
                                      margin: 0,
                                      padding: 12,
                                      borderRadius: 12,
                                      background: "var(--semi-color-fill-0)",
                                      overflowX: "auto",
                                      whiteSpace: "pre-wrap",
                                      wordBreak: "break-word",
                                      fontSize: 12
                                    }}
                                  >
                                    {formatPublishLogPayload(item.payload)}
                                  </pre>
                                ) : null}
                              </div>
                            </div>
                          </div>
                        ))
                      )}
                    </div>
                  </Card>
                  {connectors.length === 0 ? (
                    <Card bodyStyle={{ padding: 24 }}>
                      <Empty description={t("cozePublishManageNoRecord")} />
                    </Card>
                  ) : (
                    connectors.map(connector => {
                      const statusKey = getConnectorStatusKey(connector.connector_publish_status);
                      const status = statusKey
                        ? {
                            color:
                              statusKey === "cozePublishManageStatusSuccess"
                                ? ("green" as const)
                                : statusKey === "cozePublishManageStatusFailed"
                                  ? ("red" as const)
                                  : statusKey === "cozePublishManageStatusDisabled"
                                    ? ("grey" as const)
                                    : ("orange" as const),
                            label: t(statusKey)
                          }
                        : { color: "grey" as const, label: "-" };
                      return (
                        <Card
                          key={`${connector.connector_id ?? "connector"}-${connector.connector_name ?? "unknown"}`}
                          bodyStyle={{ padding: 16 }}
                        >
                          <div style={{ display: "flex", justifyContent: "space-between", gap: 16, flexWrap: "wrap", alignItems: "center" }}>
                            <div>
                              <Text strong>{connector.connector_name ?? connector.connector_id ?? "-"}</Text>
                              <div style={{ marginTop: 8 }}>
                                <Tag color={status.color}>{status.label}</Tag>
                              </div>
                            </div>
                            <div style={{ minWidth: 260 }}>
                              <Text type="tertiary">{t("cozePublishManageLogMessage")}</Text>
                              <div style={{ marginTop: 4 }}>
                                <Text>{connector.connector_publish_status_msg || "-"}</Text>
                              </div>
                            </div>
                          </div>
                        </Card>
                      );
                    })
                  )}
                </div>
              </Tabs.TabPane>

              <Tabs.TabPane tab={t("cozePublishManageTabTriggers")} itemKey="triggers">
                <div style={{ display: "grid", gap: 12 }}>
                  <Banner type="info" fullMode={false} bordered description={t("cozePublishManageTriggerHint")} />
                  <div style={{ display: "flex", justifyContent: "flex-end" }}>
                    <Button
                      theme="solid"
                      onClick={() => { void openCreateTrigger(); }}
                      disabled={triggerActionKey.length > 0}
                    >
                      {t("cozePublishManageTriggerCreateAction")}
                    </Button>
                  </div>
                  {triggerLoadFailed ? (
                    <Banner type="danger" fullMode={false} bordered description={t("cozePublishManageLoadFailed")} />
                  ) : null}
                  {triggers.length === 0 ? (
                    <Card bodyStyle={{ padding: 24 }}>
                      <Empty description={t("cozePublishManageNoTriggers")} />
                    </Card>
                  ) : (
                    triggers.map(trigger => (
                      <Card
                        key={trigger.trigger_id}
                        bodyStyle={{ padding: 16 }}
                      >
                        <div style={{ display: "grid", gap: 12 }}>
                          <div style={{ display: "flex", justifyContent: "space-between", gap: 16, flexWrap: "wrap", alignItems: "center" }}>
                            <Text strong>{trigger.name}</Text>
                            <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
                              <Switch
                                checked={trigger.enabled}
                                disabled={triggerActionKey.length > 0}
                                onChange={next => {
                                  void handleToggleTrigger(trigger, next);
                                }}
                              />
                              <Button
                                theme="borderless"
                                disabled={triggerActionKey.length > 0}
                                onClick={() => { void openEditTrigger(trigger); }}
                              >
                                {t("cozePublishManageTriggerEditAction")}
                              </Button>
                              <Button
                                type="danger"
                                theme="borderless"
                                loading={triggerActionKey === `delete:${trigger.trigger_id}`}
                                disabled={triggerActionKey.length > 0}
                                onClick={() => {
                                  void handleDeleteTrigger(trigger);
                                }}
                              >
                                {t("cozePublishManageTriggerDeleteAction")}
                              </Button>
                            </div>
                          </div>
                          <div>
                            <Text type="tertiary">{t("cozePublishManageTriggerType")}</Text>
                            <div style={{ marginTop: 4 }}>
                              <Text>{trigger.trigger_type || "-"}</Text>
                            </div>
                          </div>
                          <div>
                            <Text type="tertiary">{t("cozePublishManageUpdatedAt")}</Text>
                            <div style={{ marginTop: 4 }}>
                              <Text>{formatPublishTime(trigger.updated_at)}</Text>
                            </div>
                          </div>
                        </div>
                      </Card>
                    ))
                  )}
                </div>
              </Tabs.TabPane>
            </Tabs>
          </div>
        )}
      </Spin>
    </div>
  );
}

import * as React from "react";
import { useEffect, useMemo, useState } from "react";
import { useParams, useSearchParams } from "react-router-dom";
import { Banner, Card, Empty, Select, Spin, Tabs, Tag, Typography } from "@douyinfe/semi-ui";
import { intelligenceApi } from "@coze-arch/bot-api";
import {
  type PublishRecordDetail
} from "@coze-arch/bot-api/intelligence_api";
import { useAppI18n } from "../i18n";
import {
  formatPublishTime,
  getConnectorStatusKey,
  getPublishStatusKey,
  normalizePublishManageTab,
  summarizeConnectors
} from "./coze-agent-publish-manage-helpers";

const { Title, Text } = Typography;

export function CozeAgentPublishManagePage() {
  const { bot_id: botId = "" } = useParams<{ bot_id: string }>();
  const { t } = useAppI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);
  const [records, setRecords] = useState<PublishRecordDetail[]>([]);
  const [currentRecord, setCurrentRecord] = useState<PublishRecordDetail>();

  const recordIdFromQuery = searchParams.get("recordId")?.trim() ?? "";
  const activeTab = normalizePublishManageTab(searchParams.get("tab"));

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
                  {connectors.length === 0 ? (
                    <Card bodyStyle={{ padding: 24 }}>
                      <Empty description={t("cozePublishManageNoRecord")} />
                    </Card>
                  ) : (
                    connectors.map(connector => (
                      <Card
                        key={`${connector.connector_id ?? "connector"}-${connector.connector_name ?? "unknown"}-trigger`}
                        bodyStyle={{ padding: 16 }}
                      >
                        <div style={{ display: "grid", gap: 8 }}>
                          <Text strong>{connector.connector_name ?? connector.connector_id ?? "-"}</Text>
                          <div>
                            <Text type="tertiary">{t("cozePublishManageShareLink")}</Text>
                            <div style={{ marginTop: 4 }}>
                              <Text link={{ href: connector.share_link ?? "", target: "_blank" }}>
                                {connector.share_link || "-"}
                              </Text>
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

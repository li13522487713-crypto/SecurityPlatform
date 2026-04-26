import { Button, Empty, Space, Spin, Table, Tabs, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconPlay, IconRefresh } from "@douyinfe/semi-icons";
import { useEffect, useState } from "react";
import type {
  DatabaseCenterColumnSummary,
  DatabaseCenterConnectionLog,
  DatabaseCenterObjectSummary,
  DatabaseCenterSourceDetail,
  DatabaseCenterSourceSummary
} from "../../../services/api-database-center";
import { listDatabaseCenterConnectionLogs, testDatabaseCenterSource } from "../../../services/api-database-center";
import type { DatabaseCenterLabels } from "./database-center-labels";
import type { DatabaseCenterObjectAction } from "./database-structure-workbench";

const { Text } = Typography;

interface InstanceDetailPanelProps {
  labels: DatabaseCenterLabels;
  source: DatabaseCenterSourceSummary | DatabaseCenterSourceDetail | null;
  selectedObject: DatabaseCenterObjectSummary | null;
  columns: DatabaseCenterColumnSummary[];
  onObjectAction: (object: DatabaseCenterObjectSummary, action: DatabaseCenterObjectAction) => void;
  onRefresh: () => void;
}

export function InstanceDetailPanel({ labels, source, selectedObject, columns, onObjectAction, onRefresh }: InstanceDetailPanelProps) {
  const [testing, setTesting] = useState(false);
  const [logs, setLogs] = useState<DatabaseCenterConnectionLog[]>([]);
  const [loadingLogs, setLoadingLogs] = useState(false);
  const logColumns: ColumnProps<DatabaseCenterConnectionLog>[] = [
    { title: labels.updatedAt, dataIndex: "createdAt", width: 160, render: value => value ? String(value).slice(0, 19) : "-" },
    { title: "Status", dataIndex: "success", width: 90, render: value => <Tag color={value ? "green" : "red"}>{value ? labels.connected : labels.disconnected}</Tag> },
    { title: "Message", dataIndex: "message", render: value => value ? String(value) : "-" }
  ];

  useEffect(() => {
    if (source?.id) {
      void loadLogs();
    } else {
      setLogs([]);
    }
  }, [source?.id]);

  return (
    <aside className="database-center-panel">
      <div className="database-center-panel__header">
        <Text strong>{labels.details}</Text>
        {source ? <Tag color={source.provisionState === "Ready" ? "green" : "orange"}>{source.provisionState ?? "Pending"}</Tag> : null}
      </div>
      <div className="database-center-panel__body">
        {!source ? <Empty description={labels.noSource} /> : (
          <Space vertical align="start" style={{ width: "100%" }}>
            <Tabs type="line" size="small">
              <Tabs.TabPane tab={labels.basicInfo} itemKey="basic">
                <DetailRow label={labels.name} value={source.name} />
                <DetailRow label={labels.driver} value={source.driverCode} />
                <DetailRow label={labels.provisionMode} value={source.provisionState ?? "-"} />
                <DetailRow label={labels.physicalDatabase} value={source.physicalDatabaseName ?? "-"} />
                <DetailRow label={labels.defaultSchema} value={source.defaultSchemaName ?? "-"} />
                <DetailRow label={labels.updatedAt} value={source.updatedAt ?? "-"} />
                {selectedObject ? (
                  <>
                    <Text strong style={{ marginTop: 12 }}>{labels.structure}</Text>
                    <DetailRow label={labels.name} value={selectedObject.name} />
                    <DetailRow label="Type" value={selectedObject.objectType} />
                    <DetailRow label="Rows" value={selectedObject.rowCount ?? "-"} />
                    <DetailRow label={labels.description} value={selectedObject.comment ?? "-"} />
                    <Text type="tertiary">{labels.columns}: {columns.length}</Text>
                    {columns.slice(0, 10).map(column => (
                      <DetailRow
                        key={column.name}
                        label={column.name}
                        value={`${column.dataType}${column.primaryKey ? " / PK" : ""}${column.nullable ? "" : " / NOT NULL"}`}
                      />
                    ))}
                  </>
                ) : null}
              </Tabs.TabPane>
              <Tabs.TabPane tab={labels.connectionInfo} itemKey="connection">
                <DetailRow label={labels.connectionString} value={source.maskedConnectionSummary ?? "-"} />
                <DetailRow label={labels.hostProfile} value={source.hostProfileName ?? source.hostProfileId ?? "-"} />
                <DetailRow label={labels.driver} value={source.driverCode} />
                <Space wrap style={{ marginTop: 12 }}>
                  <Button icon={<IconPlay />} loading={testing} onClick={() => void testConnection()}>{labels.testConnection}</Button>
                  <Button icon={<IconRefresh />} onClick={onRefresh}>{labels.refresh}</Button>
                </Space>
              </Tabs.TabPane>
              <Tabs.TabPane tab={labels.configInfo} itemKey="config">
                <DetailRow label={labels.hostProfile} value={source.hostProfileName ?? source.hostProfileId ?? "-"} />
                <DetailRow label={labels.physicalDatabase} value={source.physicalDatabaseName ?? "-"} />
                <DetailRow label={labels.defaultSchema} value={source.defaultSchemaName ?? "-"} />
                <DetailRow label={labels.readOnly} value={source.readOnly ? labels.readOnly : "-"} />
              </Tabs.TabPane>
              <Tabs.TabPane tab={labels.performanceMonitor} itemKey="performance">
                <Spin spinning={loadingLogs}>
                  <Space vertical align="stretch" style={{ width: "100%" }}>
                    <Text strong>{labels.objectStats}</Text>
                    <DetailRow label={labels.tables} value={selectedObject ? "-" : source.draftObjectCount ?? "-"} />
                    <DetailRow label={labels.recentLogs} value={logs.length} />
                    <Table rowKey="id" size="small" pagination={false} columns={logColumns} dataSource={logs} />
                  </Space>
                </Spin>
              </Tabs.TabPane>
            </Tabs>
            <Space vertical align="stretch" style={{ width: "100%" }}>
              <Text strong>{labels.actions}</Text>
              <Space wrap>
                <Button disabled={!selectedObject} onClick={() => selectedObject && onObjectAction(selectedObject, "preview")}>{labels.dataPreview}</Button>
                <Button disabled={!selectedObject} onClick={() => selectedObject && onObjectAction(selectedObject, "ddl")}>{labels.viewDdl}</Button>
                <Button disabled={!selectedObject} onClick={() => selectedObject && onObjectAction(selectedObject, "structure")}>{labels.editStructure}</Button>
                <Button disabled={!selectedObject} type="danger" onClick={() => selectedObject && onObjectAction(selectedObject, "delete")}>{labels.deleteObject}</Button>
              </Space>
            </Space>
          </Space>
        )}
      </div>
    </aside>
  );

  async function testConnection() {
    if (!source?.id) return;

    setTesting(true);
    try {
      const result = await testDatabaseCenterSource(source.id);
      if (result.success) {
        Toast.success(result.message || labels.testSuccess);
      } else {
        Toast.error(result.message || labels.disconnected);
      }
      await loadLogs();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setTesting(false);
    }
  }

  async function loadLogs() {
    if (!source?.id) return;

    setLoadingLogs(true);
    try {
      setLogs(await listDatabaseCenterConnectionLogs(source.id));
    } catch {
      setLogs([]);
    } finally {
      setLoadingLogs(false);
    }
  }
}

function DetailRow({ label, value }: { label: string; value: unknown }) {
  return (
    <div className="database-center-detail-row">
      <Text type="tertiary">{label}</Text>
      <Text ellipsis={{ showTooltip: true }}>{value == null || value === "" ? "-" : String(value)}</Text>
    </div>
  );
}

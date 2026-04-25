import { Empty, Space, Tag, Typography } from "@douyinfe/semi-ui";
import type {
  DatabaseCenterColumnSummary,
  DatabaseCenterObjectSummary,
  DatabaseCenterSourceDetail,
  DatabaseCenterSourceSummary
} from "../../../services/api-database-center";
import type { DatabaseCenterLabels } from "./database-center-labels";

const { Text } = Typography;

interface InstanceDetailPanelProps {
  labels: DatabaseCenterLabels;
  source: DatabaseCenterSourceSummary | DatabaseCenterSourceDetail | null;
  selectedObject: DatabaseCenterObjectSummary | null;
  columns: DatabaseCenterColumnSummary[];
}

export function InstanceDetailPanel({ labels, source, selectedObject, columns }: InstanceDetailPanelProps) {
  return (
    <aside className="database-center-panel">
      <div className="database-center-panel__header">
        <Text strong>{labels.details}</Text>
        {source ? <Tag color={source.provisionState === "Ready" ? "green" : "orange"}>{source.provisionState ?? "Pending"}</Tag> : null}
      </div>
      <div className="database-center-panel__body">
        {!source ? <Empty description={labels.noSource} /> : (
          <Space vertical align="start" style={{ width: "100%" }}>
            <DetailRow label={labels.name} value={source.name} />
            <DetailRow label={labels.driver} value={source.driverCode} />
            <DetailRow label={labels.hostProfile} value={source.hostProfileName ?? source.hostProfileId ?? "-"} />
            <DetailRow label={labels.physicalDatabase} value={source.physicalDatabaseName ?? "-"} />
            <DetailRow label={labels.defaultSchema} value={source.defaultSchemaName ?? "-"} />
            <DetailRow label="Connection" value={source.maskedConnectionSummary ?? "-"} />
            <DetailRow label="Updated" value={source.updatedAt ?? "-"} />
            {selectedObject ? (
              <>
                <Text strong style={{ marginTop: 12 }}>{labels.structure}</Text>
                <DetailRow label={labels.name} value={selectedObject.name} />
                <DetailRow label="Type" value={selectedObject.objectType} />
                <DetailRow label="Rows" value={selectedObject.rowCount ?? "-"} />
                <DetailRow label={labels.description} value={selectedObject.comment ?? "-"} />
                <Text type="tertiary">{labels.columns}: {columns.length}</Text>
                {columns.slice(0, 12).map(column => (
                  <DetailRow
                    key={column.name}
                    label={column.name}
                    value={`${column.dataType}${column.primaryKey ? " / PK" : ""}${column.nullable ? "" : " / NOT NULL"}`}
                  />
                ))}
              </>
            ) : null}
          </Space>
        )}
      </div>
    </aside>
  );
}

function DetailRow({ label, value }: { label: string; value: unknown }) {
  return (
    <div className="database-center-detail-row">
      <Text type="tertiary">{label}</Text>
      <Text ellipsis={{ showTooltip: true }}>{value == null || value === "" ? "-" : String(value)}</Text>
    </div>
  );
}

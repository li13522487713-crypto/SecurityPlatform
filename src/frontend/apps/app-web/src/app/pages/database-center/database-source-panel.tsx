import { Button, Empty, Input, Space, Spin, Tag, Typography } from "@douyinfe/semi-ui";
import { IconPlus, IconRefresh, IconSearch, IconSetting } from "@douyinfe/semi-icons";
import type { DatabaseCenterSourceSummary } from "../../../services/api-database-center";
import type { DatabaseCenterLabels } from "./database-center-labels";

const { Text } = Typography;

interface DatabaseSourcePanelProps {
  labels: DatabaseCenterLabels;
  sources: DatabaseCenterSourceSummary[];
  selectedSourceId: string;
  keyword: string;
  loading: boolean;
  onKeywordChange: (value: string) => void;
  onSelectSource: (id: string) => void;
  onRefresh: () => void;
  onOpenCreate: () => void;
  onOpenHostProfiles: () => void;
}

export function DatabaseSourcePanel({
  labels,
  sources,
  selectedSourceId,
  keyword,
  loading,
  onKeywordChange,
  onSelectSource,
  onRefresh,
  onOpenCreate,
  onOpenHostProfiles
}: DatabaseSourcePanelProps) {
  return (
    <aside className="database-center-panel">
      <div className="database-center-panel__header">
        <Text strong>{labels.sources}</Text>
        <Space spacing={4}>
          <Button icon={<IconRefresh />} theme="borderless" onClick={onRefresh} />
          <Button icon={<IconSetting />} theme="borderless" onClick={onOpenHostProfiles} />
          <Button icon={<IconPlus />} theme="solid" onClick={onOpenCreate} />
        </Space>
      </div>
      <div className="database-center-panel__body">
        <Space vertical align="start" style={{ width: "100%" }}>
          <Input
            prefix={<IconSearch />}
            placeholder={labels.searchSources}
            value={keyword}
            onChange={onKeywordChange}
          />
          <Spin spinning={loading}>
            <Space vertical align="start" style={{ width: "100%" }}>
              {sources.length === 0 ? <Empty description={labels.noSource} /> : null}
              {sources.map(source => (
                <button
                  key={source.id}
                  type="button"
                  className={`database-center-source${source.id === selectedSourceId ? " database-center-source--active" : ""}`}
                  onClick={() => onSelectSource(source.id)}
                >
                  <Space vertical align="start" spacing={4} style={{ width: "100%" }}>
                    <Space style={{ width: "100%", justifyContent: "space-between" }}>
                      <Text strong ellipsis={{ showTooltip: true }} style={{ maxWidth: 160 }}>{source.name}</Text>
                      <Tag color={(source.status ?? source.provisionState) === "Ready" ? "green" : "orange"}>{source.status ?? source.provisionState ?? "Pending"}</Tag>
                    </Space>
                    <Space spacing={4} wrap>
                      <Tag>{source.driverCode}</Tag>
                      <Tag color={source.environment === "Online" ? "orange" : "green"}>{source.environment ?? "Draft"}</Tag>
                      {source.readOnly ? <Tag color="orange">{labels.readOnly}</Tag> : null}
                    </Space>
                    <Text type="tertiary" size="small" ellipsis={{ showTooltip: true }}>
                      {source.maskedConnectionSummary || source.address || source.physicalDatabaseName || source.description || source.id}
                    </Text>
                  </Space>
                </button>
              ))}
            </Space>
          </Spin>
        </Space>
      </div>
    </aside>
  );
}

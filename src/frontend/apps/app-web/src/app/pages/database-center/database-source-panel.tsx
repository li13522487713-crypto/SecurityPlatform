import { Button, Dropdown, Empty, Input, Select, Space, Spin, Tag, Typography } from "@douyinfe/semi-ui";
import { IconMore, IconPlus, IconRefresh, IconSearch, IconSetting } from "@douyinfe/semi-icons";
import { useMemo, useState } from "react";
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
  onOpenMigration?: (source: DatabaseCenterSourceSummary) => void;
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
  onOpenHostProfiles,
  onOpenMigration
}: DatabaseSourcePanelProps) {
  const [driverFilter, setDriverFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState("all");
  const [environmentFilter, setEnvironmentFilter] = useState("all");
  const driverOptions = useMemo(
    () => Array.from(new Set(sources.map(item => item.driverCode).filter(Boolean))).map(value => ({ value, label: value })),
    [sources]
  );
  const visibleSources = useMemo(
    () => sources.filter(source => {
      const status = source.status ?? source.provisionState ?? "Pending";
      return (driverFilter === "all" || source.driverCode === driverFilter)
        && (statusFilter === "all" || status === statusFilter)
        && (environmentFilter === "all" || source.environment === environmentFilter);
    }),
    [driverFilter, environmentFilter, sources, statusFilter]
  );

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
          <Space spacing={6} wrap>
            <Select
              size="small"
              value={driverFilter}
              style={{ width: 108 }}
              optionList={[{ value: "all", label: labels.allDrivers }, ...driverOptions]}
              onChange={value => setDriverFilter(String(value))}
            />
            <Select
              size="small"
              value={statusFilter}
              style={{ width: 108 }}
              optionList={[
                { value: "all", label: labels.allStatuses },
                { value: "Ready", label: "Ready" },
                { value: "Pending", label: "Pending" },
                { value: "Failed", label: "Failed" }
              ]}
              onChange={value => setStatusFilter(String(value))}
            />
            <Select
              size="small"
              value={environmentFilter}
              style={{ width: 108 }}
              optionList={[
                { value: "all", label: labels.allEnvironments },
                { value: "Draft", label: "Draft" },
                { value: "Online", label: "Online" }
              ]}
              onChange={value => setEnvironmentFilter(String(value))}
            />
          </Space>
          <Spin spinning={loading} style={{ width: "100%" }}>
            <Space vertical align="start" className="database-center-source-list">
              {visibleSources.length === 0 ? <Empty description={labels.noSource} /> : null}
              {visibleSources.map(source => {
                const status = source.status ?? source.provisionState ?? "Pending";
                const isReady = status === "Ready";
                return (
                <button
                  key={source.id}
                  type="button"
                  className={`database-center-source${source.id === selectedSourceId ? " database-center-source--active" : ""}`}
                  onClick={() => onSelectSource(source.id)}
                  onContextMenu={event => {
                    event.preventDefault();
                    onSelectSource(source.id);
                  }}
                >
                  <div className="database-center-source__content">
                    <div className="database-center-source__main">
                      <div className="database-center-source__name">
                        <span className={`database-center-driver database-center-driver--${source.driverCode.toLowerCase()}`}>{source.driverCode.slice(0, 2).toUpperCase()}</span>
                        <Text strong ellipsis={{ showTooltip: true }}>{source.name}</Text>
                      </div>
                      <Tag className="database-center-source__state" color={isReady ? "green" : "red"}>{isReady ? labels.connected : labels.disconnected}</Tag>
                    </div>
                    <Space className="database-center-source__tags" spacing={4} wrap>
                      <Tag>{source.driverCode}</Tag>
                      <Tag color={source.environment === "Online" ? "orange" : "green"}>{source.environment ?? "Draft"}</Tag>
                      {source.readOnly ? <Tag color="orange">{labels.readOnly}</Tag> : null}
                    </Space>
                    <Text className="database-center-source__summary" type="tertiary" size="small" ellipsis={{ showTooltip: true }}>
                      {source.maskedConnectionSummary || source.address || source.physicalDatabaseName || source.description || source.id}
                    </Text>
                    <div className="database-center-source__footer">
                      <Text className="database-center-source__updated" type="tertiary" size="small" ellipsis={{ showTooltip: true }}>{labels.updatedAt}: {source.updatedAt ? String(source.updatedAt).slice(0, 19) : "-"}</Text>
                      <Dropdown
                        trigger="click"
                        render={
                          <Dropdown.Menu>
                            <Dropdown.Item onClick={() => onSelectSource(source.id)}>{labels.editStructure}</Dropdown.Item>
                            <Dropdown.Item onClick={onOpenHostProfiles}>{labels.hostProfiles}</Dropdown.Item>
                            {source.aiDatabaseId ? (
                              <Dropdown.Item onClick={() => onOpenMigration?.(source)}>{labels.migrateDatabase}</Dropdown.Item>
                            ) : null}
                            <Dropdown.Item onClick={onRefresh}>{labels.refresh}</Dropdown.Item>
                          </Dropdown.Menu>
                        }
                      >
                        <Button theme="borderless" size="small" icon={<IconMore />} onClick={event => event.stopPropagation()} />
                      </Dropdown>
                    </div>
                  </div>
                </button>
                );
              })}
            </Space>
          </Spin>
        </Space>
      </div>
    </aside>
  );
}

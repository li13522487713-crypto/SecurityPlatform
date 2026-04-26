import { Button, Dropdown, Empty, Input, Select, Space, Spin, Tree, Typography } from "@douyinfe/semi-ui";
import { IconMore, IconSearch } from "@douyinfe/semi-icons";
import { useState } from "react";
import type { TreeNodeData } from "@douyinfe/semi-ui/lib/es/tree";
import type {
  DatabaseCenterEnvironment,
  DatabaseCenterObjectSummary,
  DatabaseCenterSchemaStructure,
  DatabaseCenterSchemaSummary
} from "../../../services/api-database-center";
import type { DatabaseCenterLabels } from "./database-center-labels";

const { Text } = Typography;

interface DatabaseSchemaTreeProps {
  labels: DatabaseCenterLabels;
  environment: DatabaseCenterEnvironment;
  schemas: DatabaseCenterSchemaSummary[];
  selectedSchema: string;
  structure: DatabaseCenterSchemaStructure | null;
  loading: boolean;
  onEnvironmentChange: (value: DatabaseCenterEnvironment) => void;
  onSchemaChange: (value: string) => void;
  onObjectSelect: (object: DatabaseCenterObjectSummary, action?: "structure" | "preview" | "ddl" | "delete") => void;
}

export function DatabaseSchemaTree({
  labels,
  environment,
  schemas,
  selectedSchema,
  structure,
  loading,
  onEnvironmentChange,
  onSchemaChange,
  onObjectSelect
}: DatabaseSchemaTreeProps) {
  const [keyword, setKeyword] = useState("");
  const objectByKey = new Map<string, DatabaseCenterObjectSummary>();
  const normalizedKeyword = keyword.trim().toLowerCase();
  const treeData: TreeNodeData[] = schemas.map(schema => {
    const objects = schema.name === selectedSchema ? structure?.objects ?? [] : [];
    const groups = [
      { key: "table", label: labels.tables },
      { key: "view", label: labels.views },
      { key: "procedure", label: labels.procedures },
      { key: "function", label: labels.functions },
      { key: "trigger", label: labels.triggers },
      { key: "event", label: labels.events }
    ].map(group => ({
      key: `${schema.name}:${group.key}`,
      label: `${group.label} (${objects.filter(item => item.objectType === group.key).length})`,
      children: objects
        .filter(item => item.objectType === group.key)
        .filter(item => !normalizedKeyword || item.name.toLowerCase().includes(normalizedKeyword) || schema.name.toLowerCase().includes(normalizedKeyword))
        .map(item => {
          const key = `${schema.name}:${item.objectType}:${item.name}`;
          objectByKey.set(key, item);
          return {
            key,
            label: (
              <Space className="database-center-tree-object" style={{ width: "100%", justifyContent: "space-between" }}>
                <span>{item.name}</span>
                {(item.objectType === "table" || item.objectType === "view") ? (
                  <Dropdown
                    trigger="click"
                    render={
                      <Dropdown.Menu>
                        <Dropdown.Item onClick={() => onObjectSelect(item, "structure")}>{labels.editStructure}</Dropdown.Item>
                        <Dropdown.Item onClick={() => onObjectSelect(item, "preview")}>{labels.queryData}</Dropdown.Item>
                        <Dropdown.Item onClick={() => onObjectSelect(item, "ddl")}>{labels.viewDdl}</Dropdown.Item>
                        <Dropdown.Item type="danger" onClick={() => onObjectSelect(item, "delete")}>{labels.deleteObject}</Dropdown.Item>
                      </Dropdown.Menu>
                    }
                  >
                    <Button
                      size="small"
                      theme="borderless"
                      icon={<IconMore />}
                      onClick={event => event.stopPropagation()}
                    />
                  </Dropdown>
                ) : null}
              </Space>
            ),
            isLeaf: true
          };
        })
    }));

    return {
      key: `schema:${schema.name}`,
      label: schema.defaultSchema ? `${schema.name} *` : schema.name,
      children: groups
    };
  });

  return (
    <aside className="database-center-panel">
      <div className="database-center-panel__header">
        <Text strong>{labels.schemas}</Text>
        <Select
          size="small"
          value={environment}
          style={{ width: 104 }}
          disabled
          onChange={value => onEnvironmentChange(String(value) as DatabaseCenterEnvironment)}
          optionList={[
            { value: "Draft", label: "Draft" },
            { value: "Online", label: "Online" }
          ]}
        />
      </div>
      <div className="database-center-panel__body">
        <Input
          prefix={<IconSearch />}
          placeholder={labels.searchSchemas}
          value={keyword}
          onChange={setKeyword}
          style={{ marginBottom: 10 }}
        />
        <Spin spinning={loading}>
          {schemas.length === 0 ? (
            <Empty description={labels.noSchema} />
          ) : (
            <Tree
              treeData={treeData}
              defaultExpandAll
              selectedKey={selectedSchema ? `schema:${selectedSchema}` : undefined}
              onSelect={(key: string) => {
                if (key.startsWith("schema:")) {
                  onSchemaChange(key.slice("schema:".length));
                  return;
                }

                const object = objectByKey.get(key);
                if (object) {
                  onObjectSelect(object);
                }
              }}
            />
          )}
        </Spin>
      </div>
    </aside>
  );
}

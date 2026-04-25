import { Empty, Select, Spin, Tree, Typography } from "@douyinfe/semi-ui";
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
  onObjectSelect: (object: DatabaseCenterObjectSummary) => void;
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
  const objectByKey = new Map<string, DatabaseCenterObjectSummary>();
  const treeData: TreeNodeData[] = schemas.map(schema => {
    const objects = schema.name === selectedSchema ? structure?.objects ?? [] : [];
    const groups = [
      { key: "table", label: labels.tables },
      { key: "view", label: labels.views },
      { key: "procedure", label: labels.procedures },
      { key: "trigger", label: labels.triggers }
    ].map(group => ({
      key: `${schema.name}:${group.key}`,
      label: `${group.label} (${objects.filter(item => item.objectType === group.key).length})`,
      children: objects
        .filter(item => item.objectType === group.key)
        .map(item => {
          const key = `${schema.name}:${item.objectType}:${item.name}`;
          objectByKey.set(key, item);
          return { key, label: item.name, isLeaf: true };
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

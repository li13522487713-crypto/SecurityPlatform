import { Button, Dropdown, Empty, Space, Table, Tag, Typography } from "@douyinfe/semi-ui";
import { IconChevronDown } from "@douyinfe/semi-icons";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import type { DatabaseCenterObjectSummary } from "../../../services/api-database-center";
import type { DatabaseCenterLabels } from "./database-center-labels";

interface TableObjectListProps {
  labels: DatabaseCenterLabels;
  objects: DatabaseCenterObjectSummary[];
  onSelectObject: (object: DatabaseCenterObjectSummary, action?: "structure" | "preview" | "ddl") => void;
  onDeleteObject?: (object: DatabaseCenterObjectSummary) => void;
}

const { Text } = Typography;

export function TableObjectList({ labels, objects, onSelectObject, onDeleteObject }: TableObjectListProps) {
  const columns: ColumnProps<DatabaseCenterObjectSummary>[] = [
    {
      title: labels.name,
      dataIndex: "name",
      width: 220,
      render: (_value: unknown, record) => (
        <Button theme="borderless" onClick={() => onSelectObject(record, "structure")}>
          <Text ellipsis={{ showTooltip: true }} style={{ maxWidth: 180 }}>{record.name}</Text>
        </Button>
      )
    },
    {
      title: "Type",
      width: 120,
      dataIndex: "objectType",
      render: (value: unknown) => <Tag>{String(value)}</Tag>
    },
    { title: "Schema", dataIndex: "schema", width: 120, render: (value: unknown) => value ? String(value) : "-" },
    { title: "Rows", dataIndex: "rowCount", width: 110, render: (value: unknown) => value == null ? "-" : String(value) },
    { title: "Updated", dataIndex: "updatedAt", width: 180, render: (value: unknown) => value ? String(value) : "-" },
    {
      title: labels.description,
      dataIndex: "comment",
      width: 260,
      render: (value: unknown) => <Text type="tertiary" ellipsis={{ showTooltip: true }}>{value ? String(value) : "-"}</Text>
    },
    {
      title: labels.actions,
      dataIndex: "_actions",
      width: 138,
      fixed: "right",
      render: (_value: unknown, record) => {
        const actionable = record.objectType === "table" || record.objectType === "view";
        return (
          <Dropdown
            trigger="click"
            position="bottomRight"
            render={
              <Dropdown.Menu>
                <Dropdown.Item disabled={!actionable} onClick={() => onSelectObject(record, "structure")}>{labels.editStructure}</Dropdown.Item>
                <Dropdown.Item disabled={!actionable} onClick={() => onSelectObject(record, "preview")}>{labels.queryData}</Dropdown.Item>
                <Dropdown.Item disabled={!actionable} onClick={() => onSelectObject(record, "ddl")}>{labels.viewDdl}</Dropdown.Item>
                <Dropdown.Item disabled={!actionable || !onDeleteObject} type="danger" onClick={() => onDeleteObject?.(record)}>{labels.deleteObject}</Dropdown.Item>
              </Dropdown.Menu>
            }
          >
            <Button theme="borderless" size="small" icon={<IconChevronDown />}>{labels.actions}</Button>
          </Dropdown>
        );
      }
    }
  ];

  if (objects.length === 0) {
    return <Empty description={labels.emptyStructure} />;
  }

  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      <div className="database-center-table-scroll">
        <Table
          rowKey={record => [record.schema, record.objectType, record.name, record.id].filter(Boolean).join(":")}
          size="small"
          pagination={false}
          columns={columns}
          dataSource={objects}
          scroll={{ x: 1148 }}
          onRow={record => ({
            onContextMenu: event => {
              event.preventDefault();
              if (record.objectType === "table" || record.objectType === "view") {
                onSelectObject(record, "preview");
              }
            }
          })}
        />
      </div>
    </Space>
  );
}

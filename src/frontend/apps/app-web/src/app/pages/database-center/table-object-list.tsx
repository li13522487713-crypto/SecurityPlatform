import { Button, Empty, Space, Table, Tag, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import type { DatabaseCenterObjectSummary } from "../../../services/api-database-center";
import type { DatabaseCenterLabels } from "./database-center-labels";

interface TableObjectListProps {
  labels: DatabaseCenterLabels;
  objects: DatabaseCenterObjectSummary[];
  onSelectObject: (object: DatabaseCenterObjectSummary) => void;
}

const { Text } = Typography;

export function TableObjectList({ labels, objects, onSelectObject }: TableObjectListProps) {
  const columns: ColumnProps<DatabaseCenterObjectSummary>[] = [
    {
      title: labels.name,
      dataIndex: "name",
      render: (_value: unknown, record) => (
        <Button theme="borderless" onClick={() => onSelectObject(record)}>{record.name}</Button>
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
      render: (value: unknown) => <Text type="tertiary">{value ? String(value) : "-"}</Text>
    }
  ];

  if (objects.length === 0) {
    return <Empty description={labels.emptyStructure} />;
  }

  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      <Table rowKey="id" size="small" pagination={false} columns={columns} dataSource={objects} />
    </Space>
  );
}

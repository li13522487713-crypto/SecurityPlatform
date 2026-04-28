import { Table, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import type { PreviewDataResponse } from "../../../services/api-database-structure";

const { Text } = Typography;

export function DataPreviewTable({ data }: { data: PreviewDataResponse }) {
  const rows = data.rows.map((row, index) => ({
    __rowKey: String(index),
    ...row
  }));

  const columns: ColumnProps<Record<string, unknown>>[] = data.columns.map(column => ({
    title: column.name,
    dataIndex: column.name,
    width: 180,
    render: (value: unknown) => {
      if (value == null) {
        return <Text type="tertiary">NULL</Text>;
      }

      const text = String(value);
      return <Text ellipsis={{ showTooltip: true }}>{text.length > 240 ? `${text.slice(0, 240)}...` : text}</Text>;
    }
  }));

  return (
    <div className="database-center-table-scroll">
      <Table
        rowKey="__rowKey"
        columns={columns}
        dataSource={rows}
        pagination={false}
        size="small"
        scroll={{ x: Math.max(720, columns.length * 180) }}
      />
    </div>
  );
}

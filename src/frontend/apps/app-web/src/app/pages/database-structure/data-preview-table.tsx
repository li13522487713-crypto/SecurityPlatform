import { Table, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import type { PreviewDataResponse } from "../../../services/api-database-structure";

const { Text } = Typography;

export function DataPreviewTable({ data }: { data: PreviewDataResponse }) {
  const columns: ColumnProps<Record<string, unknown>>[] = data.columns.map(column => ({
    title: column.name,
    dataIndex: column.name,
    render: (value: unknown) => {
      if (value == null) {
        return <Text type="tertiary">NULL</Text>;
      }

      const text = String(value);
      return text.length > 120 ? `${text.slice(0, 120)}...` : text;
    }
  }));

  return (
    <Table
      rowKey={(_, index) => String(index ?? "")}
      columns={columns}
      dataSource={data.rows}
      pagination={false}
      size="small"
    />
  );
}

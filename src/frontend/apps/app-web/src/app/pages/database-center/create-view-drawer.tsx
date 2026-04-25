import { useMemo, useState } from "react";
import { Button, Input, SideSheet, Space, Table, TextArea, Toast, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconEyeOpened, IconSave } from "@douyinfe/semi-icons";
import {
  createView,
  previewViewSql,
  type PreviewDataResponse
} from "../../../services/api-database-structure";
import { SqlCodeEditor } from "../database-structure/sql-code-editor";
import type { DatabaseCenterLabels } from "./database-center-labels";
import { formatSql } from "./sql-format-utils";

const { Text } = Typography;

interface CreateViewDrawerProps {
  labels: DatabaseCenterLabels;
  visible: boolean;
  sourceId: string;
  schemaName: string;
  onClose: () => void;
  onCreated: () => Promise<void> | void;
}

export function CreateViewDrawer({ labels, visible, sourceId, schemaName, onClose, onCreated }: CreateViewDrawerProps) {
  const [viewName, setViewName] = useState("");
  const [comment, setComment] = useState("");
  const [sql, setSql] = useState("SELECT\n  id,\n  name\nFROM sqlite_master\nWHERE type = 'table';");
  const [preview, setPreview] = useState<PreviewDataResponse | null>(null);
  const [loading, setLoading] = useState(false);

  const columns: ColumnProps<Record<string, unknown>>[] = useMemo(
    () => (preview?.columns ?? []).map(column => ({
      title: column.name,
      dataIndex: column.name,
      render: (value: unknown) => value == null ? <Text type="tertiary">NULL</Text> : String(value)
    })),
    [preview]
  );

  return (
    <SideSheet visible={visible} onCancel={onClose} title={labels.createView} width={980}>
      <Space vertical align="start" style={{ width: "100%" }}>
        <Space style={{ width: "100%" }}>
          <Input placeholder={labels.viewName} value={viewName} onChange={setViewName} style={{ width: 260 }} />
          <Input placeholder={labels.comment} value={comment} onChange={setComment} style={{ width: 320 }} />
        </Space>
        <TextArea
          autosize={{ minRows: 2, maxRows: 3 }}
          placeholder={labels.viewSelectSql}
          value={sql}
          onChange={(value: string) => setSql(value)}
        />
        <Space>
          <Button onClick={() => setSql(formatSql(sql))}>{labels.format}</Button>
          <Button icon={<IconEyeOpened />} loading={loading} disabled={!sql.trim()} onClick={() => void previewSql()}>{labels.preview}</Button>
          <Button icon={<IconSave />} theme="solid" loading={loading} disabled={!viewName.trim() || !sql.trim()} onClick={() => void save()}>{labels.create}</Button>
        </Space>
        <SqlCodeEditor value={sql} onChange={setSql} height={260} />
        {preview ? (
          <Table
            rowKey="__rowKey"
            size="small"
            pagination={false}
            columns={columns}
            dataSource={preview.rows.map((row, index) => ({ ...row, __rowKey: String(index) }))}
          />
        ) : null}
      </Space>
    </SideSheet>
  );

  async function previewSql() {
    setLoading(true);
    try {
      const result = await previewViewSql(sourceId, { schema: schemaName, sql, limit: 100 });
      setPreview(result);
      Toast.success(labels.previewSuccess);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoading(false);
    }
  }

  async function save() {
    setLoading(true);
    try {
      await createView(sourceId, {
        schema: schemaName,
        viewName: viewName.trim(),
        comment: comment.trim() || undefined,
        sql,
        mode: "SelectOnly"
      });
      Toast.success(labels.createSuccess);
      await onCreated();
      onClose();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoading(false);
    }
  }
}

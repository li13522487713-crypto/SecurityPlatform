import { useState } from "react";
import { Button, SideSheet, Space, Toast, Typography } from "@douyinfe/semi-ui";
import { IconSave } from "@douyinfe/semi-icons";
import { createTableSql } from "../../../services/api-database-structure";
import { SqlCodeEditor } from "../database-structure/sql-code-editor";
import type { DatabaseCenterLabels } from "./database-center-labels";
import { formatSql } from "./sql-format-utils";

const { Text } = Typography;

interface ImportDdlDrawerProps {
  labels: DatabaseCenterLabels;
  visible: boolean;
  sourceId: string;
  schemaName: string;
  canEdit: boolean;
  onClose: () => void;
  onImported: () => Promise<void> | void;
}

export function ImportDdlDrawer({
  labels,
  visible,
  sourceId,
  schemaName,
  canEdit,
  onClose,
  onImported
}: ImportDdlDrawerProps) {
  const [sql, setSql] = useState("CREATE TABLE imported_table (\n  id INTEGER PRIMARY KEY AUTOINCREMENT,\n  name TEXT NOT NULL,\n  created_at DATETIME DEFAULT CURRENT_TIMESTAMP\n);");
  const [loading, setLoading] = useState(false);

  return (
    <SideSheet visible={visible} title={labels.importDdl} width={820} onCancel={onClose}>
      <Space vertical align="stretch" style={{ width: "100%" }}>
        <Text type="tertiary">{labels.importDdlHint}</Text>
        <Space>
          <Button onClick={() => setSql(formatSql(sql))}>{labels.format}</Button>
          <Button icon={<IconSave />} theme="solid" loading={loading} disabled={!canEdit || !sql.trim()} onClick={() => void execute()}>
            {labels.execute}
          </Button>
        </Space>
        <SqlCodeEditor value={sql} onChange={setSql} height={420} />
      </Space>
    </SideSheet>
  );

  async function execute() {
    setLoading(true);
    try {
      await createTableSql(sourceId, { schema: schemaName, sql });
      Toast.success(labels.createSuccess);
      await onImported();
      onClose();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoading(false);
    }
  }
}

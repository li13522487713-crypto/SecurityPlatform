import { useEffect, useState } from "react";
import { Button, Input, Radio, SideSheet, Space, Toast } from "@douyinfe/semi-ui";
import { useAppI18n } from "../../i18n";
import { createView, previewViewSql, type PreviewDataResponse } from "../../../services/api-database-structure";
import { DataPreviewTable } from "./data-preview-table";
import { SqlCodeEditor } from "./sql-code-editor";

interface CreateViewDrawerProps {
  visible: boolean;
  databaseId: string;
  onClose: () => void;
  onCreated: () => Promise<void>;
}

export function CreateViewDrawer({ visible, databaseId, onClose, onCreated }: CreateViewDrawerProps) {
  const { t } = useAppI18n();
  const [viewName, setViewName] = useState("");
  const [schema, setSchema] = useState("");
  const [comment, setComment] = useState("");
  const [mode, setMode] = useState<"SelectOnly" | "CreateViewSql">("SelectOnly");
  const [sql, setSql] = useState("SELECT 1 AS id");
  const [preview, setPreview] = useState<PreviewDataResponse | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!visible) {
      setViewName("");
      setSchema("");
      setComment("");
      setMode("SelectOnly");
      setSql("SELECT 1 AS id");
      setPreview(null);
    }
  }, [visible]);

  async function handlePreview() {
    try {
      setBusy(true);
      setPreview(await previewViewSql(databaseId, { sql, limit: 100 }));
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : t("databaseStructureLoadFailed"));
    } finally {
      setBusy(false);
    }
  }

  async function handleCreate() {
    if (!viewName.trim()) {
      Toast.warning(t("databaseStructureViewName"));
      return;
    }

    try {
      setBusy(true);
      await createView(databaseId, { schema: schema || undefined, viewName, comment, sql, mode });
      Toast.success(t("databaseStructureCreateSuccess"));
      onClose();
      await onCreated();
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : t("databaseStructureLoadFailed"));
    } finally {
      setBusy(false);
    }
  }

  return (
    <SideSheet visible={visible} onCancel={onClose} title={t("databaseStructureNewView")} width="min(900px, calc(100vw - 32px))">
      <Space vertical align="start" style={{ width: "100%" }}>
        <Input placeholder={t("databaseStructureViewName")} value={viewName} onChange={setViewName} />
        <Input placeholder="schema" value={schema} onChange={setSchema} />
        <Input placeholder={t("databaseStructureComment")} value={comment} onChange={setComment} />
        <Radio.Group value={mode} onChange={event => setMode(event.target.value)}>
          <Radio value="SelectOnly">SELECT</Radio>
          <Radio value="CreateViewSql">CREATE VIEW</Radio>
        </Radio.Group>
        <SqlCodeEditor value={sql} onChange={setSql} height={320} />
        <Space wrap>
          <Button onClick={onClose}>{t("databaseStructureCancel")}</Button>
          <Button loading={busy} onClick={() => void handlePreview()}>{t("databaseStructurePreview")}</Button>
          <Button loading={busy} theme="solid" onClick={() => void handleCreate()}>{t("databaseStructureCreateView")}</Button>
        </Space>
        {preview ? <DataPreviewTable data={preview} /> : null}
      </Space>
    </SideSheet>
  );
}

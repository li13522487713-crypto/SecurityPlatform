import { useCallback, useState } from "react";
import { Form, InputNumber, Modal, Select, Toast } from "@douyinfe/semi-ui";
import { importLibraryItem, type LibraryResourceType } from "../../../services/api-ai-workspace";
import { useAppI18n } from "../../i18n";
import type { AppMessageKey } from "../../messages";

type ImportableType = "workflow" | "plugin" | "knowledge-base" | "database";

const IMPORTABLE_TYPES: { value: LibraryResourceType; labelKey: AppMessageKey }[] = [
  { value: "workflow", labelKey: "cozeLibraryTabWorkflow" },
  { value: "plugin", labelKey: "cozeLibraryTabPlugin" },
  { value: "knowledge-base", labelKey: "cozeLibraryTabKnowledge" },
  { value: "database", labelKey: "cozeLibraryTabDatabase" }
];

export interface LibraryImportModalProps {
  visible: boolean;
  onClose: () => void;
  onImported: () => void;
}

export function LibraryImportModal({ visible, onClose, onImported }: LibraryImportModalProps) {
  const { t } = useAppI18n();
  const [submitting, setSubmitting] = useState(false);
  const [resourceType, setResourceType] = useState<LibraryResourceType>("knowledge-base");
  const [libraryItemId, setLibraryItemId] = useState<number | undefined>(undefined);

  const handleOk = useCallback(async () => {
    if (!libraryItemId || libraryItemId <= 0) {
      Toast.warning(t("cozeLibraryImportIdRequired"));
      return;
    }
    setSubmitting(true);
    try {
      await importLibraryItem({
        resourceType: resourceType as ImportableType,
        libraryItemId
      });
      Toast.success(t("cozeLibraryImportSuccess"));
      onImported();
    } catch (error) {
      Toast.error((error as Error).message || t("cozeLibraryQueryFailed"));
    } finally {
      setSubmitting(false);
    }
  }, [libraryItemId, onImported, resourceType, t]);

  return (
    <Modal
      visible={visible}
      title={t("cozeLibraryImportTitle")}
      onOk={() => void handleOk()}
      onCancel={onClose}
      confirmLoading={submitting}
      okText={t("cozeLibraryImportAction")}
      cancelText={t("cozeCommonCancel")}
    >
      <Form labelPosition="left" labelWidth={96}>
        <Form.Slot label={t("cozeLibraryImportResourceType")}>
          <Select
            value={resourceType}
            onChange={v => setResourceType(v as LibraryResourceType)}
            optionList={IMPORTABLE_TYPES.map(x => ({ value: x.value, label: t(x.labelKey) }))}
            style={{ width: "100%" }}
          />
        </Form.Slot>
        <Form.Slot label={t("cozeLibraryImportItemId")}>
          <InputNumber
            value={libraryItemId}
            onChange={v => setLibraryItemId(typeof v === "number" ? v : Number(v) || undefined)}
            style={{ width: "100%" }}
            placeholder={t("cozeLibraryImportItemIdPlaceholder")}
          />
        </Form.Slot>
      </Form>
    </Modal>
  );
}

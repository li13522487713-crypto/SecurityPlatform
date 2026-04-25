import { useEffect, useState } from "react";
import { Checkbox, Input, Modal, Space, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../../i18n";
import type { DatabaseObjectDto } from "../../../services/api-database-structure";

const { Text } = Typography;

interface DangerDeleteModalProps {
  visible: boolean;
  object: DatabaseObjectDto | null;
  loading?: boolean;
  onCancel: () => void;
  onConfirm: (confirmName: string) => Promise<void>;
}

export function DangerDeleteModal({ visible, object, loading = false, onCancel, onConfirm }: DangerDeleteModalProps) {
  const { t } = useAppI18n();
  const [confirmName, setConfirmName] = useState("");
  const [confirmDanger, setConfirmDanger] = useState(false);

  useEffect(() => {
    if (!visible) {
      setConfirmName("");
      setConfirmDanger(false);
    }
  }, [visible]);

  const matched = Boolean(object && confirmName === object.name && confirmDanger);

  return (
    <Modal
      visible={visible}
      title={t("databaseStructureDeleteTitle")}
      okText={t("databaseStructureActionDelete")}
      cancelText={t("databaseStructureCancel")}
      okType="danger"
      okButtonProps={{ disabled: !matched, loading }}
      onCancel={onCancel}
      onOk={() => object ? onConfirm(confirmName) : undefined}
    >
      <Space vertical align="stretch" style={{ width: "100%" }}>
        <Text>{t("databaseStructureDeleteContent")}</Text>
        <Text strong>{object?.name}</Text>
        <Input value={confirmName} placeholder={object?.name} onChange={setConfirmName} />
        <Checkbox checked={confirmDanger} onChange={event => setConfirmDanger(Boolean(event.target.checked))}>
          {t("databaseStructureDeleteContent")}
        </Checkbox>
      </Space>
    </Modal>
  );
}

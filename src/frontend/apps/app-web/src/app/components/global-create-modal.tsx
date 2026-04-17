import { useState } from "react";
import { Modal, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../i18n";
import { CreateAgentModal } from "./create-agent-modal";
import { CreateAppModal } from "./create-app-modal";

interface GlobalCreateModalProps {
  visible: boolean;
  workspaceId: string;
  onClose: () => void;
}

type ChooseTarget = "agent" | "app";

export function GlobalCreateModal({ visible, workspaceId, onClose }: GlobalCreateModalProps) {
  const { t } = useAppI18n();
  const [target, setTarget] = useState<ChooseTarget | null>(null);

  const handleClose = () => {
    setTarget(null);
    onClose();
  };

  return (
    <>
      <Modal
        title={t("cozeCreateModalTitle")}
        visible={visible && target === null}
        onCancel={handleClose}
        footer={null}
        width={680}
        data-testid="coze-global-create-modal"
      >
        <div className="coze-create-cards">
          <button
            type="button"
            className="coze-create-card"
            onClick={() => setTarget("agent")}
            data-testid="coze-global-create-agent"
          >
            <div className="coze-create-card__icon" aria-hidden>
              {t("cozeCreateChooseAgent").slice(0, 1)}
            </div>
            <Typography.Title heading={5} style={{ margin: "12px 0 4px" }}>
              {t("cozeCreateChooseAgent")}
            </Typography.Title>
            <Typography.Text type="tertiary">{t("cozeCreateChooseAgentDesc")}</Typography.Text>
          </button>
          <button
            type="button"
            className="coze-create-card"
            onClick={() => setTarget("app")}
            data-testid="coze-global-create-app"
          >
            <div className="coze-create-card__icon" aria-hidden>
              {t("cozeCreateChooseApp").slice(0, 1)}
            </div>
            <Typography.Title heading={5} style={{ margin: "12px 0 4px" }}>
              {t("cozeCreateChooseApp")}
            </Typography.Title>
            <Typography.Text type="tertiary">{t("cozeCreateChooseAppDesc")}</Typography.Text>
          </button>
        </div>
      </Modal>

      <CreateAgentModal
        visible={visible && target === "agent"}
        workspaceId={workspaceId}
        onClose={handleClose}
      />
      <CreateAppModal
        visible={visible && target === "app"}
        workspaceId={workspaceId}
        onClose={handleClose}
      />
    </>
  );
}

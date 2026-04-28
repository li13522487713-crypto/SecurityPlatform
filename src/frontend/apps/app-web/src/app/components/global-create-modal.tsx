import { useState } from "react";
import { Modal, Tag, Typography } from "@douyinfe/semi-ui";
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
        width={900}
        className="coze-global-create-modal"
        data-testid="coze-global-create-modal"
      >
        <div className="coze-create-cards">
          <button
            type="button"
            className="coze-create-card"
            onClick={() => setTarget("agent")}
            data-testid="coze-global-create-agent"
          >
            <div className="coze-create-card__visual coze-create-card__visual--agent" aria-hidden>
              <div className="coze-create-agent-preview">
                <div className="coze-create-agent-preview__bubble coze-create-agent-preview__bubble--top" />
                <div className="coze-create-agent-preview__bubble coze-create-agent-preview__bubble--middle" />
                <div className="coze-create-agent-preview__bubble coze-create-agent-preview__bubble--bottom" />
                <div className="coze-create-agent-preview__avatar coze-create-agent-preview__avatar--top" />
                <div className="coze-create-agent-preview__avatar coze-create-agent-preview__avatar--middle" />
                <div className="coze-create-agent-preview__avatar coze-create-agent-preview__avatar--bottom" />
              </div>
              <div className="coze-create-card__announce">
                <span>{t("cozeCreateAgentSupportNotice")}</span>
                <span className="coze-create-card__announce-close">×</span>
              </div>
            </div>
            <div className="coze-create-card__content">
              <Typography.Title heading={4} className="coze-create-card__title">
                {t("cozeCreateChooseAgent")}
              </Typography.Title>
              <Typography.Text type="tertiary" className="coze-create-card__desc">
                {t("cozeCreateChooseAgentDesc")}
              </Typography.Text>
              <div
                aria-hidden
                className="semi-button semi-button-primary semi-button-block"
                style={{
                  marginTop: 16,
                  borderRadius: 8,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  minHeight: 32,
                  pointerEvents: "none"
                }}
              >
                <span className="semi-button-content">+ {t("cozeCreateChooseAgent")}</span>
              </div>
            </div>
          </button>
          <button
            type="button"
            className="coze-create-card"
            onClick={() => setTarget("app")}
            data-testid="coze-global-create-app"
          >
            <div className="coze-create-card__visual coze-create-card__visual--app" aria-hidden>
              <div className="coze-create-app-preview">
                <div className="coze-create-app-preview__desktop">
                  <div className="coze-create-app-preview__desktop-dots">
                    <span />
                    <span />
                    <span />
                  </div>
                  <div className="coze-create-app-preview__desktop-line coze-create-app-preview__desktop-line--title" />
                  <div className="coze-create-app-preview__desktop-line" />
                  <div className="coze-create-app-preview__desktop-line coze-create-app-preview__desktop-line--short" />
                </div>
                <div className="coze-create-app-preview__phone">
                  <div className="coze-create-app-preview__phone-time" />
                  <div className="coze-create-app-preview__phone-title" />
                  <div className="coze-create-app-preview__phone-line" />
                  <div className="coze-create-app-preview__phone-input" />
                </div>
              </div>
            </div>
            <div className="coze-create-card__content">
              <div className="coze-create-card__title-row">
                <Typography.Title heading={4} className="coze-create-card__title">
                  {t("cozeCreateChooseApp")}
                </Typography.Title>
                <Tag size="small" color="blue" className="coze-create-card__beta-tag">
                  {t("cozeCreateAppBetaTag")}
                </Tag>
              </div>
              <Typography.Text type="tertiary" className="coze-create-card__desc">
                {t("cozeCreateChooseAppDesc")}
              </Typography.Text>
              <div
                aria-hidden
                className="semi-button semi-button-primary semi-button-block"
                style={{
                  marginTop: 16,
                  borderRadius: 8,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  minHeight: 32,
                  pointerEvents: "none"
                }}
              >
                <span className="semi-button-content">+ 低代码搭建</span>
              </div>
            </div>
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

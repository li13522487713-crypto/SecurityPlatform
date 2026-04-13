import { Alert, Button, Input, Switch } from "antd";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { patchChatflowRoleConfig, readChatflowRoleConfig, validateChatflowRoleConfig } from "../editor/chatflow-role-config";
import { useWorkflowEditorStore } from "../stores/workflow-editor-store";

interface ChatflowRolePanelProps {
  visible: boolean;
  readOnly?: boolean;
  onClose: () => void;
}

export function ChatflowRolePanel(props: ChatflowRolePanelProps) {
  const { t } = useTranslation();
  const store = useWorkflowEditorStore();
  const roleConfig = useMemo(() => readChatflowRoleConfig(store.canvasGlobals), [store.canvasGlobals]);
  const issues = useMemo(() => validateChatflowRoleConfig(roleConfig), [roleConfig]);
  const isReadOnly = Boolean(props.readOnly);

  if (!props.visible) {
    return null;
  }

  const avatarText = (roleConfig.avatarLabel || roleConfig.roleName || t("wfUi.chatflowRole.avatarFallback")).trim().slice(0, 1).toUpperCase();

  function updateConfig(patch: Parameters<typeof patchChatflowRoleConfig>[1]) {
    if (isReadOnly) {
      return;
    }
    store.setCanvasGlobals(patchChatflowRoleConfig(store.canvasGlobals, patch));
    store.setDirty(true);
  }

  return (
    <aside className="wf-react-chatflow-role-panel">
      <div className="wf-react-chatflow-role-header">
        <div>
          <div className="wf-react-chatflow-role-title">{t("wfUi.chatflowRole.title")}</div>
          <div className="wf-react-chatflow-role-subtitle">{t("wfUi.chatflowRole.subtitle")}</div>
        </div>
        <Button size="small" onClick={props.onClose}>
          {t("wfUi.chatflowRole.close")}
        </Button>
      </div>

      <div className="wf-react-chatflow-role-scroll">
        {issues.length > 0 ? (
          <Alert
            type="warning"
            showIcon
            className="wf-react-chatflow-role-alert"
            message={t("wfUi.chatflowRole.validationTitle")}
            description={
              <ul className="wf-react-chatflow-role-alert-list">
                {issues.map((issue) => (
                  <li key={issue}>{issue}</li>
                ))}
              </ul>
            }
          />
        ) : null}
        <section className="wf-react-chatflow-role-section">
          <header className="wf-react-chatflow-role-section-title">{t("wfUi.chatflowRole.infoTitle")}</header>
          <label className="wf-react-chatflow-role-field">
            <span>{t("wfUi.chatflowRole.roleName")}</span>
            <Input
              value={roleConfig.roleName}
              disabled={isReadOnly}
              maxLength={50}
              placeholder={t("wfUi.chatflowRole.roleNamePlaceholder")}
              onChange={(event) => updateConfig({ roleName: event.target.value })}
            />
          </label>
          <label className="wf-react-chatflow-role-field">
            <span>{t("wfUi.chatflowRole.roleDescription")}</span>
            <Input.TextArea
              rows={5}
              value={roleConfig.roleDescription}
              disabled={isReadOnly}
              maxLength={600}
              placeholder={t("wfUi.chatflowRole.roleDescriptionPlaceholder")}
              onChange={(event) => updateConfig({ roleDescription: event.target.value })}
            />
          </label>
          <div className="wf-react-chatflow-role-avatar-row">
            <div className="wf-react-chatflow-role-avatar">{avatarText}</div>
            <label className="wf-react-chatflow-role-field wf-react-chatflow-role-field-inline">
              <span>{t("wfUi.chatflowRole.avatarLabel")}</span>
              <Input
                value={roleConfig.avatarLabel}
                disabled={isReadOnly}
                maxLength={2}
                placeholder={t("wfUi.chatflowRole.avatarPlaceholder")}
                onChange={(event) => updateConfig({ avatarLabel: event.target.value })}
              />
            </label>
          </div>
        </section>

        <section className="wf-react-chatflow-role-section">
          <header className="wf-react-chatflow-role-section-title">{t("wfUi.chatflowRole.openingTitle")}</header>
          <label className="wf-react-chatflow-role-field">
            <span>{t("wfUi.chatflowRole.openingText")}</span>
            <Input.TextArea
              rows={6}
              value={roleConfig.openingText}
              disabled={isReadOnly}
              maxLength={2000}
              placeholder={t("wfUi.chatflowRole.openingTextPlaceholder")}
              onChange={(event) => updateConfig({ openingText: event.target.value })}
            />
          </label>
        </section>

        <section className="wf-react-chatflow-role-section">
          <header className="wf-react-chatflow-role-section-title">{t("wfUi.chatflowRole.openingQuestionsTitle")}</header>
          <div className="wf-react-chatflow-role-toggle">
            <span>{t("wfUi.chatflowRole.showAllQuestions")}</span>
            <Switch
              checked={roleConfig.showAllOpeningQuestions}
              disabled={isReadOnly}
              onChange={(checked) => updateConfig({ showAllOpeningQuestions: checked })}
            />
          </div>
          <div className="wf-react-chatflow-role-questions">
            {roleConfig.openingQuestions.map((question, index) => (
              <div key={`${index.toString(36)}-${question}`} className="wf-react-chatflow-role-question-row">
                <Input
                  value={question}
                  disabled={isReadOnly}
                  maxLength={100}
                  placeholder={t("wfUi.chatflowRole.openingQuestionPlaceholder")}
                  onChange={(event) => {
                    const nextQuestions = [...roleConfig.openingQuestions];
                    nextQuestions[index] = event.target.value;
                    updateConfig({ openingQuestions: nextQuestions });
                  }}
                />
                {!isReadOnly ? (
                  <Button
                    size="small"
                    type="text"
                    onClick={() => {
                      const nextQuestions = roleConfig.openingQuestions.filter((_, currentIndex) => currentIndex !== index);
                      updateConfig({ openingQuestions: nextQuestions });
                    }}
                  >
                    {t("wfUi.chatflowRole.removeQuestion")}
                  </Button>
                ) : null}
              </div>
            ))}
            {!isReadOnly ? (
              <Button
                size="small"
                type="dashed"
                disabled={roleConfig.openingQuestions.length >= 8}
                onClick={() => updateConfig({ openingQuestions: [...roleConfig.openingQuestions, ""] })}
              >
                {t("wfUi.chatflowRole.addQuestion")}
              </Button>
            ) : null}
          </div>
        </section>
      </div>
    </aside>
  );
}

import { Button, Modal, Typography } from "@douyinfe/semi-ui";
import type { StudioLocale } from "../types";
import { getStudioCopy } from "../copy";

export interface AgentPublishModalProps {
  visible: boolean;
  releaseNote: string;
  onReleaseNoteChange: (value: string) => void;
  onCancel: () => void;
  onConfirm: () => void;
  /** 提交中（发布 API 调用期间） */
  submitting: boolean;
  locale: StudioLocale;
}

export function AgentPublishModal({
  visible,
  releaseNote,
  onReleaseNoteChange,
  onCancel,
  onConfirm,
  submitting,
  locale
}: AgentPublishModalProps) {
  const copy = getStudioCopy(locale);
  return (
    <Modal
      title={copy.assistant.publishModalTitle}
      visible={visible}
      onCancel={onCancel}
      footer={
        <div style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}>
          <Button onClick={onCancel} disabled={submitting}>
            {copy.common.cancel}
          </Button>
          <Button theme="solid" type="primary" loading={submitting} onClick={onConfirm}>
            {copy.assistant.publishModalConfirm}
          </Button>
        </div>
      }
    >
      <div className="module-studio__stack">
        <Typography.Text type="tertiary">
          {copy.assistant.publishModalNoteHint}
        </Typography.Text>
        <textarea
          className="module-studio__textarea"
          rows={5}
          placeholder={copy.assistant.publishModalNotePlaceholder}
          value={releaseNote}
          onChange={event => onReleaseNoteChange(event.target.value)}
          disabled={submitting}
        />
      </div>
    </Modal>
  );
}

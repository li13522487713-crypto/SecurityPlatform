import { Button, Modal, Typography } from "@douyinfe/semi-ui";

export interface AgentPublishModalProps {
  visible: boolean;
  releaseNote: string;
  onReleaseNoteChange: (value: string) => void;
  onCancel: () => void;
  onConfirm: () => void;
  /** 提交中（发布 API 调用期间） */
  submitting: boolean;
}

export function AgentPublishModal({
  visible,
  releaseNote,
  onReleaseNoteChange,
  onCancel,
  onConfirm,
  submitting
}: AgentPublishModalProps) {
  return (
    <Modal
      title="发布智能体"
      visible={visible}
      onCancel={onCancel}
      footer={
        <div style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}>
          <Button onClick={onCancel} disabled={submitting}>
            取消
          </Button>
          <Button theme="solid" type="primary" loading={submitting} onClick={onConfirm}>
            确认发布
          </Button>
        </div>
      }
    >
      <div className="module-studio__stack">
        <Typography.Text type="tertiary">
          发布说明将随版本记录保存，便于审计与回滚对照（可选）。
        </Typography.Text>
        <textarea
          className="module-studio__textarea"
          rows={5}
          placeholder="例如：修复知识库检索阈值、更新插件工具超时策略。"
          value={releaseNote}
          onChange={event => onReleaseNoteChange(event.target.value)}
          disabled={submitting}
        />
      </div>
    </Modal>
  );
}

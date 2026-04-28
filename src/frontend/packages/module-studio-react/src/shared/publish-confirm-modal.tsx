import React, { useState } from "react";
import { Modal, TextArea, Typography } from "@douyinfe/semi-ui";
import type { StudioLocale } from "../types";
import { formatStudioTemplate, getStudioCopy } from "../copy";

export interface PublishConfirmModalProps {
  visible: boolean;
  resourceName: string;
  resourceType: "agent" | "app" | "workflow";
  onCancel: () => void;
  onPublish: (releaseNote: string) => Promise<void>;
  pendingChanges?: string[];
  locale: StudioLocale;
}

export function PublishConfirmModal({
  visible,
  resourceName,
  resourceType,
  onCancel,
  onPublish,
  pendingChanges = [],
  locale
}: PublishConfirmModalProps) {
  const copy = getStudioCopy(locale);
  const [submitting, setSubmitting] = useState(false);
  const [releaseNote, setReleaseNote] = useState("");

  const handleSubmit = async () => {
    setSubmitting(true);
    try {
      await onPublish(releaseNote.trim());
      setReleaseNote("");
    } finally {
      setSubmitting(false);
    }
  };

  const handleCancel = () => {
    setReleaseNote("");
    onCancel();
  };

  const resourceLabel =
    resourceType === "agent"
      ? copy.publishConfirm.resourceAgent
      : resourceType === "app"
        ? copy.publishConfirm.resourceApp
        : copy.publishConfirm.resourceWorkflow;

  return (
    <Modal
      title={`${copy.publishConfirm.titlePrefix} ${resourceName}`}
      visible={visible}
      onCancel={handleCancel}
      onOk={handleSubmit}
      okButtonProps={{ loading: submitting }}
      okText={copy.publishConfirm.okText}
      cancelText={copy.common.cancel}
      width={500}
    >
      <div style={{ marginBottom: 24 }}>
        <Typography.Text>
          {formatStudioTemplate(copy.publishConfirm.bodyTemplate, { type: resourceLabel })}
        </Typography.Text>
      </div>

      {pendingChanges.length > 0 && (
        <div style={{ marginBottom: 16 }}>
          <Typography.Text strong>{copy.publishConfirm.pendingChanges}</Typography.Text>
          <ul>
            {pendingChanges.map((change, idx) => (
              <li key={idx}><Typography.Text type="tertiary">{change}</Typography.Text></li>
            ))}
          </ul>
        </div>
      )}

      <div style={{ marginTop: 12 }}>
        <Typography.Text strong style={{ display: "block", marginBottom: 8 }}>
          {copy.publishConfirm.noteOptional}
        </Typography.Text>
        <TextArea
          value={releaseNote}
          onChange={setReleaseNote}
          placeholder={copy.publishConfirm.notePlaceholder}
          rows={4}
        />
      </div>
    </Modal>
  );
}

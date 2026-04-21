import { useEffect, useRef, useState } from "react";
import { Input, Modal, TextArea, Toast, Typography } from "@douyinfe/semi-ui";
import { getTenantId } from "@atlas/shared-react-core/utils";
import { workspaceHomePath } from "@atlas/app-shell-shared";
import { createWorkspace } from "../../services/api-org-workspaces";
import { useAppI18n } from "../i18n";
import { useNavigate } from "react-router-dom";

const NAME_MAX = 50;
const DESC_MAX = 2000;

interface CreateWorkspaceModalProps {
  visible: boolean;
  onClose: () => void;
  onCreated?: (newId: string) => void;
  onSubmit?: (values: { name: string; description?: string }) => Promise<void | string>;
}

export function CreateWorkspaceModal({ visible, onClose, onCreated, onSubmit }: CreateWorkspaceModalProps) {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const [name, setName] = useState("");
  const [desc, setDesc] = useState("");
  const [saving, setSaving] = useState(false);
  const nameRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (visible) {
      setName("");
      setDesc("");
      setTimeout(() => nameRef.current?.focus(), 100);
    }
  }, [visible]);

  const handleOk = async () => {
    const trimmed = name.trim();
    if (!trimmed) {
      Toast.warning(t("workspaceListNamePlaceholder"));
      return;
    }
    const orgId = getTenantId();
    if (!orgId && !onSubmit) return;
    setSaving(true);
    try {
      if (onSubmit) {
        await onSubmit({ name: trimmed, description: desc.trim() || undefined });
        onClose();
      } else {
        const newId = await createWorkspace(orgId!, {
          name: trimmed,
          description: desc.trim() || undefined
        });
        Toast.success(t("workspaceListCreatedSuccess"));
        onClose();
        if (onCreated) {
          onCreated(newId);
        } else {
          navigate(workspaceHomePath(newId));
        }
      }
    } catch (err) {
      Toast.error((err as Error).message || t("workspaceListActionFailed"));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      title={null}
      header={null}
      footer={null}
      visible={visible}
      onCancel={onClose}
      closeOnEsc
      maskClosable={!saving}
      style={{ width: 520, borderRadius: 20, overflow: "hidden", padding: 0 }}
      bodyStyle={{ padding: 0 }}
      data-testid="create-workspace-modal"
    >
      <div style={{ padding: "32px 36px 28px" }}>
        {/* Header row: title + close */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
          <Typography.Title heading={4} style={{ margin: 0, fontWeight: 700, fontSize: 20, color: "#1d2129" }}>
            {t("workspaceListCreateDialogTitle") || "创建新工作空间"}
          </Typography.Title>
          <button
            type="button"
            onClick={onClose}
            style={{
              background: "transparent",
              border: "none",
              cursor: "pointer",
              fontSize: 20,
              color: "#86909c",
              lineHeight: 1,
              padding: "2px 4px",
              borderRadius: 6
            }}
          >
            ✕
          </button>
        </div>

        {/* Subtitle */}
        <Typography.Text type="tertiary" style={{ fontSize: 14, lineHeight: 1.6, display: "block", marginBottom: 24 }}>
          通过创建工作空间，将支持项目、智能体、插件、工作流和知识库在工作空间内进行协作和共享。
        </Typography.Text>

        {/* Large icon */}
        <div style={{ display: "flex", justifyContent: "center", marginBottom: 28 }}>
          <div style={{
            width: 80, height: 80,
            borderRadius: 20,
            background: "linear-gradient(135deg, #e8846a 0%, #d4603e 100%)",
            display: "flex", alignItems: "center", justifyContent: "center",
            boxShadow: "0 8px 24px rgba(212, 96, 62, 0.3)"
          }}>
            <svg width="40" height="40" viewBox="0 0 40 40" fill="none">
              <circle cx="14" cy="14" r="7" fill="white" opacity="0.9" />
              <circle cx="26" cy="14" r="7" fill="white" opacity="0.7" />
              <ellipse cx="14" cy="30" rx="10" ry="6" fill="white" opacity="0.9" />
              <ellipse cx="28" cy="30" rx="9" ry="6" fill="white" opacity="0.6" />
            </svg>
          </div>
        </div>

        {/* Name field */}
        <div style={{ marginBottom: 20 }}>
          <div style={{ display: "flex", alignItems: "center", marginBottom: 8 }}>
            <Typography.Text style={{ fontWeight: 500, fontSize: 14, color: "#1d2129" }}>
              工作空间名称
            </Typography.Text>
            <span style={{ color: "#f53f3f", marginLeft: 4, fontSize: 14 }}>*</span>
          </div>
          <Input
            ref={nameRef as React.Ref<HTMLInputElement>}
            value={name}
            onChange={setName}
            placeholder="请输入工作空间名称"
            maxLength={NAME_MAX}
            showClear
            size="large"
            suffix={
              <span style={{ fontSize: 12, color: "#c9cdd4", paddingRight: 4 }}>
                {name.length}/{NAME_MAX}
              </span>
            }
            style={{ borderRadius: 10 }}
            data-testid="create-workspace-name"
          />
        </div>

        {/* Description field */}
        <div style={{ marginBottom: 32 }}>
          <Typography.Text style={{ fontWeight: 500, fontSize: 14, color: "#1d2129", display: "block", marginBottom: 8 }}>
            描述
          </Typography.Text>
          <div style={{ position: "relative" }}>
            <TextArea
              value={desc}
              onChange={setDesc}
              placeholder="描述工作空间"
              rows={4}
              style={{ borderRadius: 10, paddingBottom: 28, resize: "none" }}
              data-testid="create-workspace-desc"
            />
            <span style={{
              position: "absolute", right: 12, bottom: 10,
              fontSize: 12, color: "#c9cdd4", pointerEvents: "none"
            }}>
              {desc.length}/{DESC_MAX}
            </span>
          </div>
        </div>

        {/* Footer buttons */}
        <div style={{ display: "flex", justifyContent: "flex-end", gap: 12 }}>
          <button
            type="button"
            onClick={onClose}
            disabled={saving}
            style={{
              padding: "10px 28px",
              borderRadius: 10,
              border: "1px solid #e5e6eb",
              background: "#fff",
              cursor: "pointer",
              fontSize: 15,
              fontWeight: 500,
              color: "#4e5969"
            }}
          >
            {t("cancel") || "取消"}
          </button>
          <button
            type="button"
            onClick={() => { void handleOk(); }}
            disabled={saving || !name.trim()}
            style={{
              padding: "10px 28px",
              borderRadius: 10,
              border: "none",
              background: saving || !name.trim() ? "#c2c7d0" : "linear-gradient(135deg, #4080ff 0%, #1d57d8 100%)",
              cursor: saving || !name.trim() ? "not-allowed" : "pointer",
              fontSize: 15,
              fontWeight: 500,
              color: "#fff",
              boxShadow: saving || !name.trim() ? "none" : "0 4px 12px rgba(64,128,255,0.3)",
              transition: "all 0.2s"
            }}
            data-testid="create-workspace-submit"
          >
            {saving ? "创建中..." : (t("workspaceListCreate") || "确认")}
          </button>
        </div>
      </div>
    </Modal>
  );
}

import { readChatflowRoleConfig } from "../../editor/chatflow-role-config";
import { useWorkflowEditorStore } from "../../stores/workflow-editor-store";

interface StartContentProps {
  variable?: string;
}

export function StartContent(props: StartContentProps) {
  const globals = useWorkflowEditorStore((state) => state.canvasGlobals);
  const roleConfig = readChatflowRoleConfig(globals);
  const isChatflow = Boolean(
    roleConfig.roleName ||
      roleConfig.roleDescription ||
      roleConfig.avatarLabel ||
      roleConfig.openingText ||
      roleConfig.openingQuestions.length > 0 ||
      Object.prototype.hasOwnProperty.call(globals, "chatflowRoleConfig")
  );

  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">类型: {isChatflow ? "触发器" : "开始节点"}</div>
      {isChatflow ? (
        <div className="wf-node-render-kv wf-node-render-ellipsis">
          角色: {roleConfig.roleName || roleConfig.avatarLabel || "未配置角色"}
        </div>
      ) : null}
      <div className="wf-node-render-tags">
        <span className="wf-node-render-tag">{props.variable ?? "USER_INPUT"}</span>
        {isChatflow ? <span className="wf-node-render-tag">CONVERSATION_NAME</span> : null}
      </div>
      {isChatflow ? (
        <div className="wf-node-render-kv">
          开场白: {roleConfig.openingText ? "已配置" : "未配置"} / 预置问题: {roleConfig.openingQuestions.filter((item) => item.trim()).length}
        </div>
      ) : null}
    </div>
  );
}

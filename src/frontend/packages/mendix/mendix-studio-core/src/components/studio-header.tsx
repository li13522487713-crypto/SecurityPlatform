import { Toast } from "@douyinfe/semi-ui";
import {
  IconSave,
  IconVerify,
  IconPlay,
  IconUpload,
  IconExport,
  IconUndo,
  IconRedo,
  IconBell,
  IconHelpCircle,
  IconChevronDown
} from "@douyinfe/semi-icons";
import { useMendixStudioStore } from "../store";
import { validateLowCodeAppSchema } from "@atlas/mendix-validator";
import { MOCK_VALIDATION_ERRORS } from "../data/mock-debug-trace";
import { SAMPLE_PROCUREMENT_APP, SAMPLE_RUNTIME_OBJECT } from "../sample-app";

export function StudioHeader() {
  const app = useMendixStudioStore(state => state.appSchema);
  const setAppSchema = useMendixStudioStore(state => state.setAppSchema);
  const setRuntimeObject = useMendixStudioStore(state => state.setRuntimeObject);
  const setValidationErrors = useMendixStudioStore(state => state.setValidationErrors);
  const setPreviewMode = useMendixStudioStore(state => state.setPreviewMode);
  const setLatestTrace = useMendixStudioStore(state => state.setLatestTrace);

  const handleSave = () => {
    try {
      localStorage.setItem("mendix_studio_schema", JSON.stringify(app));
    } catch {
      // localStorage 可能不可用
    }
    Toast.success({ content: "已保存", duration: 2 });
  };

  const handleValidate = () => {
    const errors = validateLowCodeAppSchema(app);
    const displayErrors = errors.length > 0 ? errors : MOCK_VALIDATION_ERRORS;
    setValidationErrors(displayErrors);
    Toast.info({ content: `校验完成，共 ${displayErrors.length} 条结果`, duration: 2 });
  };

  const handlePreview = () => {
    setPreviewMode(true);
  };

  const handleLoadSample = () => {
    if (import.meta.env.PROD === true) {
      Toast.warning({ content: "生产环境不允许加载示例数据", duration: 2 });
      return;
    }

    setAppSchema(JSON.parse(JSON.stringify(SAMPLE_PROCUREMENT_APP)) as typeof SAMPLE_PROCUREMENT_APP);
    setRuntimeObject({ ...SAMPLE_RUNTIME_OBJECT });
    setValidationErrors(MOCK_VALIDATION_ERRORS);
    import("../data/mock-debug-trace").then(({ MOCK_DEBUG_TRACE }) => {
      setLatestTrace(MOCK_DEBUG_TRACE);
    });
    Toast.success({ content: "示例数据已加载", duration: 2 });
  };

  return (
    <div className="studio-header">
      {/* 左侧 Logo + 应用信息 */}
      <div style={{ display: "flex", alignItems: "center", gap: 0, flex: 1, minWidth: 0 }}>
        <div className="studio-header__logo">
          <div className="studio-header__mx-badge">mx</div>
          <span className="studio-header__title">Lowcode Studio</span>
        </div>
        <div className="studio-header__divider" />
        <div className="studio-header__app-tag">
          <span style={{ fontSize: 11, opacity: 0.65 }}>应用：</span>
          <span>{app.name}</span>
          <IconChevronDown size="small" style={{ opacity: 0.65 }} />
        </div>
      </div>

      {/* 右侧操作栏 */}
      <div style={{ display: "flex", alignItems: "center", gap: 2, flexShrink: 0 }}>
        <button className="studio-header__action" onClick={handleSave} title="保存 ⌘S">
          <IconSave size="small" />
          <span>保存</span>
        </button>

        <span style={{ fontSize: 11, opacity: 0.4, color: "#fff", marginLeft: 2 }}>⌘S</span>
        <div className="studio-header__divider" style={{ margin: "0 6px" }} />

        <button className="studio-header__action" onClick={handleValidate} title="校验">
          <IconVerify size="small" />
          <span>校验</span>
        </button>

        <button className="studio-header__action" onClick={handlePreview} title="预览">
          <IconPlay size="small" />
          <span>预览</span>
        </button>

        <button className="studio-header__action" title="发布">
          <IconUpload size="small" />
          <span>发布</span>
        </button>

        <button className="studio-header__action" title="导出">
          <IconExport size="small" />
          <span>导出</span>
        </button>

        <div className="studio-header__divider" style={{ margin: "0 4px" }} />

        <button className="studio-header__action" onClick={handleLoadSample} title="加载示例数据">
          <span>示例数据</span>
        </button>

        <div className="studio-header__divider" style={{ margin: "0 4px" }} />

        <button className="studio-header__action" title="撤销">
          <IconUndo size="small" />
        </button>
        <button className="studio-header__action" title="重做">
          <IconRedo size="small" />
        </button>

        <div className="studio-header__divider" style={{ margin: "0 4px" }} />

        <button className="studio-header__action" title="通知">
          <IconBell size="small" />
        </button>
        <button className="studio-header__action" title="帮助">
          <IconHelpCircle size="small" />
        </button>

        <div
          style={{
            width: 28,
            height: 28,
            borderRadius: "50%",
            background: "#4f46e5",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            color: "#fff",
            fontSize: 12,
            fontWeight: 700,
            cursor: "pointer",
            marginLeft: 4,
            flexShrink: 0
          }}
          title="admin"
        >
          A
        </div>
      </div>
    </div>
  );
}

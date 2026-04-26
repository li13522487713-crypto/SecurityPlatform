import {
  IconSetting,
  IconLink,
  IconCopy,
  IconDelete,
  IconMore,
  IconCode,
  IconPlay
} from "@douyinfe/semi-icons";
import { useMendixStudioStore } from "../store";

// 画布中固定展示的表单字段
const FORM_FIELDS_INNER = [
  { id: "field_requestno", label: "申请编号", type: "text", value: "PR-202505-0001", required: false },
  { id: "field_applicant", label: "申请人", type: "ref", value: "张三", required: false },
  { id: "field_dept", label: "所属部门", type: "select", value: "研发部", required: false }
];

const FORM_FIELDS_OUTER = [
  { id: "field_amount", label: "申请金额（元）", type: "number", value: "120000.00", required: true },
  { id: "field_reason", label: "申请原因", type: "textarea", value: "购买办公设备", required: true },
  { id: "field_status", label: "申请状态", type: "select", value: "Draft", required: true }
];

export function PageDesignerCanvas() {
  const selectedWidgetId = useMendixStudioStore(state => state.selectedWidgetId);
  const setSelectedWidgetId = useMendixStudioStore(state => state.setSelectedWidgetId);
  const activeTab = useMendixStudioStore(state => state.activeTab);

  // 读取 store 中 button caption（属性面板联动）
  const appSchema = useMendixStudioStore(state => state.appSchema);
  const page = appSchema.modules[0]?.pages[0];
  const submitBtnWidget = page?.rootWidget?.children?.find(w => w.widgetId === "widget_submit_btn");
  const submitCaption = submitBtnWidget
    ? String((submitBtnWidget.props as Record<string, unknown>).caption ?? "提交审批")
    : "提交审批";

  if (activeTab !== "pageBuilder") {
    return <OtherTabCanvas activeTab={activeTab} />;
  }

  const isSubmitSelected = selectedWidgetId === "widget_submit_btn";
  const isDataViewSelected = selectedWidgetId === "widget_dataview_main";

  return (
    <div className="studio-canvas-wrapper">
      <div className="studio-canvas-board">
        {/* 页面标题 */}
        <div className="studio-canvas__page-title">采购申请单</div>
        <div className="studio-canvas__page-subtitle">请填写采购申请信息</div>

        {/* DataView 容器（带蓝色边框） */}
        <div
          className="studio-dataview"
          style={{
            borderColor: isDataViewSelected ? "var(--studio-blue)" : "var(--studio-blue)",
            cursor: "pointer"
          }}
          onClick={() => setSelectedWidgetId("widget_dataview_main")}
        >
          <div className="studio-dataview__tag">Data View（PurchaseRequest）</div>

          {/* DataView 内字段 */}
          {FORM_FIELDS_INNER.map(field => (
            <div key={field.id} className="studio-form-row">
              <div className={`studio-form-label${field.required ? " studio-form-label--required" : ""}`}>
                {field.label}
              </div>
              <div className="studio-form-field">
                {field.type === "ref" ? (
                  <div className="studio-mock-input studio-mock-input--ref">
                    <span style={{ color: "var(--studio-text-primary)", fontSize: 12 }}>{field.value}</span>
                    <div className="studio-mock-input-ref-btn">…</div>
                  </div>
                ) : field.type === "select" ? (
                  <div className="studio-mock-select">
                    <span style={{ fontSize: 12 }}>{field.value}</span>
                    <span style={{ fontSize: 10, color: "#9ca3af" }}>▼</span>
                  </div>
                ) : (
                  <div className="studio-mock-input">
                    <span style={{ fontSize: 12, color: "#9ca3af" }}>{field.value}</span>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>

        {/* DataView 外部字段 */}
        {FORM_FIELDS_OUTER.map(field => (
          <div
            key={field.id}
            className="studio-form-row"
            style={{ cursor: "pointer" }}
            onClick={() => setSelectedWidgetId(field.id)}
          >
            <div className={`studio-form-label${field.required ? " studio-form-label--required" : ""}`}>
              {field.label}
            </div>
            <div
              className="studio-form-field"
              style={{
                outline: selectedWidgetId === field.id ? "2px solid var(--studio-blue)" : "none",
                outlineOffset: 2,
                borderRadius: 4
              }}
            >
              {field.type === "textarea" ? (
                <div className="studio-mock-textarea">{field.value}</div>
              ) : field.type === "select" ? (
                <div className="studio-mock-select">
                  <span style={{ fontSize: 12 }}>{field.value}</span>
                  <span style={{ fontSize: 10, color: "#9ca3af" }}>▼</span>
                </div>
              ) : (
                <div className="studio-mock-input">
                  <span style={{ fontSize: 12 }}>{field.value}</span>
                </div>
              )}
            </div>
          </div>
        ))}

        {/* 按钮区 */}
        <div className="studio-canvas__btn-row">
          {/* 提交审批按钮（可选中） */}
          <div style={{ position: "relative", display: "inline-block" }}>
            {isSubmitSelected && <FloatingWidgetToolbar />}
            <button
              className={
                "studio-canvas__btn studio-canvas__btn--primary" +
                (isSubmitSelected ? " studio-canvas__btn--selected" : "")
              }
              onClick={() => setSelectedWidgetId("widget_submit_btn")}
            >
              {submitCaption}
            </button>
            {/* resize 控制点 */}
            {isSubmitSelected && <ResizeHandles />}
          </div>

          <button
            className="studio-canvas__btn studio-canvas__btn--default"
            onClick={() => setSelectedWidgetId("widget_save_btn")}
            style={{
              outline: selectedWidgetId === "widget_save_btn" ? "2px solid var(--studio-blue)" : "none",
              outlineOffset: 2
            }}
          >
            保存草稿
          </button>
          <button
            className="studio-canvas__btn studio-canvas__btn--default"
            onClick={() => setSelectedWidgetId("widget_cancel_btn")}
            style={{
              outline: selectedWidgetId === "widget_cancel_btn" ? "2px solid var(--studio-blue)" : "none",
              outlineOffset: 2
            }}
          >
            取消
          </button>
        </div>
      </div>
    </div>
  );
}

function FloatingWidgetToolbar() {
  return (
    <div className="studio-widget-toolbar">
      <div className="studio-widget-toolbar__btn" title="设置">
        <IconSetting style={{ fontSize: 13 }} />
      </div>
      <div className="studio-widget-toolbar__btn" title="动作">
        <IconPlay style={{ fontSize: 13 }} />
      </div>
      <div className="studio-widget-toolbar__btn" title="绑定">
        <IconLink style={{ fontSize: 13 }} />
      </div>
      <div className="studio-widget-toolbar__sep" />
      <div className="studio-widget-toolbar__btn" title="源码">
        <IconCode style={{ fontSize: 13 }} />
      </div>
      <div className="studio-widget-toolbar__btn" title="复制">
        <IconCopy style={{ fontSize: 13 }} />
      </div>
      <div className="studio-widget-toolbar__btn" title="删除">
        <IconDelete style={{ fontSize: 13 }} />
      </div>
      <div className="studio-widget-toolbar__sep" />
      <div className="studio-widget-toolbar__btn" title="更多">
        <IconMore style={{ fontSize: 13 }} />
      </div>
    </div>
  );
}

function ResizeHandles() {
  const positions: Array<{ top?: number | string; left?: number | string; right?: number | string; bottom?: number | string }> = [
    { top: -3, left: -3 },
    { top: -3, right: -3 },
    { bottom: -3, left: -3 },
    { bottom: -3, right: -3 },
    { top: "50%", left: -3 },
    { top: "50%", right: -3 },
    { top: -3, left: "50%" },
    { bottom: -3, left: "50%" }
  ];
  return (
    <>
      {positions.map((pos, i) => (
        <div
          key={i}
          className="studio-resize-handle"
          style={{
            ...pos,
            transform: "translate(-50%, -50%)"
          }}
        />
      ))}
    </>
  );
}

function OtherTabCanvas({ activeTab }: { activeTab: string }) {
  const labels: Record<string, { title: string; subtitle: string; color: string }> = {
    workflowDesigner: { title: "WF_PurchaseApproval", subtitle: "工作流设计器 — 节点编辑视图", color: "#fa8c16" },
    domainModel: { title: "Domain Model", subtitle: "领域模型设计器", color: "#722ed1" },
    securityEditor: { title: "Security", subtitle: "安全策略编辑器", color: "#f5222d" },
    runtimePreview: { title: "Runtime Preview", subtitle: "运行时预览模式", color: "#1677ff" }
  };

  const info = labels[activeTab] ?? { title: activeTab, subtitle: "", color: "#6b7280" };

  return (
    <div className="studio-canvas-wrapper">
      <div className="studio-canvas-board" style={{ display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", minHeight: 400 }}>
        <div
          style={{
            width: 56,
            height: 56,
            borderRadius: 14,
            background: info.color + "18",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            marginBottom: 16,
            fontSize: 28,
            color: info.color
          }}
        >
          ◈
        </div>
        <div style={{ fontSize: 18, fontWeight: 700, color: "var(--studio-text-primary)", marginBottom: 6 }}>
          {info.title}
        </div>
        <div style={{ fontSize: 13, color: "var(--studio-text-secondary)" }}>{info.subtitle}</div>
      </div>
    </div>
  );
}

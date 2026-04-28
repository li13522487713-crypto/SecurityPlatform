import { Input, Select, Switch, Typography } from "@douyinfe/semi-ui";
import { IconPlus, IconLink } from "@douyinfe/semi-icons";
import { useMendixStudioStore } from "../store";

const { Text } = Typography;

const BUTTON_TYPES = [
  { value: "Primary", label: "Primary" },
  { value: "Default", label: "Default" },
  { value: "Danger", label: "Danger" },
  { value: "Warning", label: "Warning" }
];

const SIZES = [
  { value: "small", label: "小" },
  { value: "medium", label: "中（默认）" },
  { value: "large", label: "大" }
];

const ACTION_TYPES = [
  { value: "callMicroflow", label: "调用 Microflow" },
  { value: "callWorkflow", label: "调用 Workflow" },
  { value: "showMessage", label: "显示消息" },
  { value: "noAction", label: "无动作" }
];

const MICROFLOW_OPTIONS = [
  { value: "mf_submit_purchase_request", label: "MF_SubmitPurchaseRequest" },
  { value: "mf_save_draft", label: "MF_SaveDraft" }
];

function ButtonPropertiesPanel() {
  const appSchema = useMendixStudioStore(state => state.appSchema);
  const setAppSchema = useMendixStudioStore(state => state.setAppSchema);
  const inspectorTab = useMendixStudioStore(state => state.inspectorTab);
  const setInspectorTab = useMendixStudioStore(state => state.setInspectorTab);

  const page = appSchema.modules[0]?.pages[0];
  const submitWidget = page?.rootWidget?.children?.find(w => w.widgetId === "widget_submit_btn");
  const captionValue = submitWidget
    ? String((submitWidget.props as Record<string, unknown>).caption ?? "提交审批")
    : "提交审批";

  const updateCaption = (value: string) => {
    const next = JSON.parse(JSON.stringify(appSchema)) as typeof appSchema;
    const pg = next.modules[0]?.pages[0];
    if (!pg) return;
    const w = pg.rootWidget.children?.find(c => c.widgetId === "widget_submit_btn");
    if (w) {
      (w.props as Record<string, unknown>).caption = value;
    }
    setAppSchema(next);
  };

  return (
    <div className="studio-properties">
      {/* 顶部 Tab */}
      <div className="studio-properties__tabs">
        <div
          className={"studio-properties__tab" + (inspectorTab === "property" ? " studio-properties__tab--active" : "")}
          onClick={() => setInspectorTab("property")}
        >
          属性
        </div>
        <div
          className={"studio-properties__tab" + (inspectorTab === "style" ? " studio-properties__tab--active" : "")}
          onClick={() => setInspectorTab("style")}
        >
          事件
        </div>
      </div>

      {/* 组件标题 */}
      <div className="studio-properties__widget-title">
        <span>
          <span style={{ fontWeight: 700 }}>Button</span>
          <span style={{ fontWeight: 400, color: "#6b7280", marginLeft: 4 }}>(submitButton)</span>
        </span>
        <div style={{ display: "flex", gap: 4 }}>
          <div
            style={{
              width: 22,
              height: 22,
              border: "1px solid #e5e7eb",
              borderRadius: 3,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              cursor: "pointer",
              color: "#1677ff",
              fontSize: 10,
              fontWeight: 700,
              background: "#e8f3ff"
            }}
            title="属性"
          >
            P
          </div>
          <div
            style={{
              width: 22,
              height: 22,
              border: "1px solid #e5e7eb",
              borderRadius: 3,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              cursor: "pointer",
              color: "#6b7280",
              fontSize: 10
            }}
            title="样式"
          >
            S
          </div>
        </div>
      </div>

      <div className="studio-properties__body">
        {/* 基础属性 */}
        <div className="studio-properties__section">
          <div className="studio-properties__section-title">基础</div>

          <div className="studio-properties__row">
            <div className="studio-properties__label">文本</div>
            <div className="studio-properties__control">
              <Input
                value={captionValue}
                onChange={v => updateCaption(v)}
                size="small"
                style={{ fontSize: 12, height: 28 }}
              />
            </div>
          </div>

          <div className="studio-properties__row">
            <div className="studio-properties__label">类型</div>
            <div className="studio-properties__control">
              <Select
                defaultValue="Primary"
                optionList={BUTTON_TYPES}
                size="small"
                style={{ width: "100%", height: 28, fontSize: 12 }}
              />
            </div>
          </div>

          <div className="studio-properties__row">
            <div className="studio-properties__label">图标</div>
            <div className="studio-properties__control">
              <Input
                placeholder="请选择图标"
                size="small"
                style={{ fontSize: 12, height: 28 }}
                prefix={<IconLink style={{ fontSize: 11, color: "#9ca3af" }} />}
              />
            </div>
          </div>

          <div className="studio-properties__row">
            <div className="studio-properties__label">大小</div>
            <div className="studio-properties__control">
              <Select
                defaultValue="medium"
                optionList={SIZES}
                size="small"
                style={{ width: "100%", height: 28, fontSize: 12 }}
              />
            </div>
          </div>

          <div className="studio-properties__row">
            <div className="studio-properties__label">加载状态</div>
            <div className="studio-properties__control">
              <Switch size="small" defaultChecked={false} />
            </div>
          </div>
        </div>

        {/* 数据 / 动作 */}
        <div className="studio-properties__section">
          <div className="studio-properties__section-title">数据 / 动作</div>

          <div className="studio-properties__row">
            <div className="studio-properties__label">动作类型</div>
            <div className="studio-properties__control">
              <Select
                defaultValue="callMicroflow"
                optionList={ACTION_TYPES}
                size="small"
                style={{ width: "100%", height: 28, fontSize: 12 }}
              />
            </div>
          </div>

          <div className="studio-properties__row">
            <div className="studio-properties__label">选择 Microflow</div>
            <div className="studio-properties__control">
              <Select
                defaultValue="mf_submit_purchase_request"
                optionList={MICROFLOW_OPTIONS}
                size="small"
                style={{ width: "100%", height: 28, fontSize: 12 }}
              />
            </div>
          </div>

          <div style={{ marginBottom: 6 }}>
            <div style={{ fontSize: 11, color: "#6b7280", marginBottom: 4 }}>参数映射</div>
            <div className="studio-properties__param-item">
              <span style={{ fontSize: 11 }}>Request</span>
              <span style={{ fontSize: 10, color: "#9ca3af" }}>→</span>
              <span style={{ fontSize: 11, color: "#0958d9" }}>PurchaseRequest</span>
              <span style={{ flex: 1 }} />
              <span style={{ fontSize: 10, color: "#9ca3af" }}>↔</span>
            </div>
            <div className="studio-properties__param-item">
              <span style={{ fontSize: 11, color: "#0958d9" }}>Data View</span>
              <span style={{ fontSize: 10, color: "#9ca3af" }}>→</span>
              <span style={{ fontSize: 11, color: "#0958d9" }}>PurchaseRequest</span>
              <span style={{ flex: 1 }} />
              <span style={{ fontSize: 10, color: "#9ca3af" }}>↔</span>
            </div>
            <div className="studio-properties__add-param">
              <IconPlus style={{ fontSize: 12 }} />
              <span>+ 添加参数</span>
            </div>
          </div>
        </div>

        {/* 可见性 */}
        <div className="studio-properties__section">
          <div className="studio-properties__section-title">可见性</div>

          <div className="studio-properties__row">
            <div className="studio-properties__label">条件可见</div>
            <div className="studio-properties__control">
              <Input
                defaultValue="true"
                size="small"
                style={{ fontSize: 12, height: 28 }}
              />
            </div>
            <div className="studio-properties__fx-btn">fx</div>
          </div>

          <div className="studio-properties__row">
            <div className="studio-properties__label">可编辑</div>
            <div className="studio-properties__control">
              <Input
                defaultValue="true"
                size="small"
                style={{ fontSize: 12, height: 28 }}
              />
            </div>
            <div className="studio-properties__fx-btn">fx</div>
          </div>
        </div>

        {/* 样式 */}
        <div className="studio-properties__section">
          <div className="studio-properties__section-title">样式</div>

          <div className="studio-properties__row">
            <div className="studio-properties__label">宽度</div>
            <div className="studio-properties__control">
              <Select
                defaultValue="auto"
                optionList={[
                  { value: "auto", label: "自动" },
                  { value: "full", label: "100%" },
                  { value: "fixed", label: "固定" }
                ]}
                size="small"
                style={{ width: "100%", height: 28, fontSize: 12 }}
              />
            </div>
          </div>

          <div className="studio-properties__row">
            <div className="studio-properties__label">CSS 类名</div>
            <div className="studio-properties__control">
              <Input
                placeholder="输入类名"
                size="small"
                style={{ fontSize: 12, height: 28 }}
              />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function GenericPropertiesPanel({ widgetId }: { widgetId: string }) {
  const inspectorTab = useMendixStudioStore(state => state.inspectorTab);
  const setInspectorTab = useMendixStudioStore(state => state.setInspectorTab);

  const widgetLabels: Record<string, string> = {
    widget_dataview_main: "DataView (PurchaseRequest)",
    field_amount: "NumberInput (Amount)",
    field_reason: "TextArea (Reason)",
    field_status: "DropDown (Status)",
    field_requestno: "TextBox (RequestNo)",
    field_applicant: "ReferenceSelector (Applicant)",
    field_dept: "DropDown (Department)",
    widget_save_btn: "Button (saveDraftBtn)",
    widget_cancel_btn: "Button (cancelBtn)"
  };

  const label = widgetLabels[widgetId] ?? widgetId;

  return (
    <div className="studio-properties">
      <div className="studio-properties__tabs">
        <div
          className={"studio-properties__tab" + (inspectorTab === "property" ? " studio-properties__tab--active" : "")}
          onClick={() => setInspectorTab("property")}
        >
          属性
        </div>
        <div
          className={"studio-properties__tab" + (inspectorTab === "style" ? " studio-properties__tab--active" : "")}
          onClick={() => setInspectorTab("style")}
        >
          事件
        </div>
      </div>

      <div className="studio-properties__widget-title">
        <span style={{ fontSize: 13 }}>{label}</span>
      </div>

      <div className="studio-properties__body">
        <div className="studio-properties__section">
          <div className="studio-properties__section-title">基础</div>
          <div style={{ padding: "8px 0", display: "flex", flexDirection: "column", gap: 8 }}>
            <Text type="tertiary" size="small">点击画布中的"提交审批"按钮</Text>
            <Text type="tertiary" size="small">可查看完整 Button 属性面板</Text>
          </div>
        </div>
      </div>
    </div>
  );
}

export function PropertiesPanel() {
  const selectedWidgetId = useMendixStudioStore(state => state.selectedWidgetId);

  if (selectedWidgetId === "widget_submit_btn") {
    return <ButtonPropertiesPanel />;
  }

  return <GenericPropertiesPanel widgetId={selectedWidgetId} />;
}

import { useMemo, useState } from "react";
import { SideSheet, InputNumber, Input, Select, Button, Toast, Typography, Divider, Tag } from "@douyinfe/semi-ui";
import { IconPlay, IconRefresh } from "@douyinfe/semi-icons";
import { useMendixStudioStore } from "../store";
import { createRuntimeExecutor } from "@atlas/mendix-runtime";
import { MOCK_DEBUG_TRACE } from "../data/mock-debug-trace";

const { Text, Title } = Typography;

const STATUS_OPTIONS = [
  { value: "Draft", label: "Draft" },
  { value: "Submitted", label: "Submitted" },
  { value: "NeedManagerApproval", label: "NeedManagerApproval" },
  { value: "NeedFinanceApproval", label: "NeedFinanceApproval" },
  { value: "Approved", label: "Approved" },
  { value: "Rejected", label: "Rejected" }
];

export function RuntimePreview() {
  const previewMode = useMendixStudioStore(state => state.previewMode);
  const setPreviewMode = useMendixStudioStore(state => state.setPreviewMode);
  const appSchema = useMendixStudioStore(state => state.appSchema);
  const runtimeObject = useMendixStudioStore(state => state.runtimeObject);
  const setRuntimeObject = useMendixStudioStore(state => state.setRuntimeObject);
  const setLatestTrace = useMendixStudioStore(state => state.setLatestTrace);
  const setLatestActionResponse = useMendixStudioStore(state => state.setLatestActionResponse);

  const executor = useMemo(() => createRuntimeExecutor(), []);
  const [submitting, setSubmitting] = useState(false);
  const [resultMsg, setResultMsg] = useState<string | null>(null);

  const amount = Number(runtimeObject.Amount ?? 0);
  const reason = String(runtimeObject.Reason ?? "");
  const status = String(runtimeObject.Status ?? "Draft");

  const handleSubmit = async () => {
    if (amount <= 0) {
      Toast.error({ content: "金额必须大于 0", duration: 3 });
      setResultMsg("❌ 金额必须大于 0");
      return;
    }

    setSubmitting(true);
    setResultMsg(null);

    try {
      const response = await executor.execute(
        appSchema,
        {
          actionType: "callMicroflow",
          microflowRef: { kind: "microflow", id: "mf_submit_purchase_request" },
          arguments: [{ name: "Request", value: runtimeObject }]
        },
        runtimeObject
      );

      setLatestActionResponse(response);

      if (response.traceId) {
        const trace = executor.getTrace(response.traceId);
        setLatestTrace(trace);
      } else {
        setLatestTrace(MOCK_DEBUG_TRACE);
      }

      const newStatus = amount > 50000 ? "NeedFinanceApproval" : "NeedManagerApproval";
      setRuntimeObject({ ...runtimeObject, Status: newStatus });

      setResultMsg("✅ 采购申请已提交审批");
      Toast.success({ content: "采购申请已提交审批", duration: 3 });
    } catch {
      const newStatus = amount > 50000 ? "NeedFinanceApproval" : "NeedManagerApproval";
      setRuntimeObject({ ...runtimeObject, Status: newStatus });
      setLatestTrace(MOCK_DEBUG_TRACE);
      setLatestActionResponse({
        success: true,
        returnValue: true,
        uiCommands: [{ type: "showMessage", level: "info", message: "采购申请已提交审批" }],
        traceId: MOCK_DEBUG_TRACE.traceId
      });
      setResultMsg("✅ 采购申请已提交审批");
      Toast.success({ content: "采购申请已提交审批", duration: 3 });
    } finally {
      setSubmitting(false);
    }
  };

  const handleReset = () => {
    setRuntimeObject({
      RequestNo: "PR-202505-0001",
      Amount: 120000,
      Status: "Draft",
      Reason: "购买办公设备",
      ApplicantName: "张三",
      DepartmentName: "研发部"
    });
    setResultMsg(null);
  };

  return (
    <SideSheet
      visible={previewMode}
      title={
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <IconPlay style={{ color: "#1677ff" }} />
          <span>运行预览 — PurchaseRequest_EditPage</span>
        </div>
      }
      width={520}
      onCancel={() => setPreviewMode(false)}
      mask={false}
      bodyStyle={{ padding: 0, overflow: "hidden", display: "flex", flexDirection: "column" }}
    >
      <div style={{ flex: 1, overflow: "auto", padding: 24 }}>
        {/* 页面标题 */}
        <Title heading={5} style={{ marginBottom: 4 }}>采购申请单</Title>
        <Text type="tertiary" style={{ fontSize: 12, display: "block", marginBottom: 20 }}>
          请填写采购申请信息
        </Text>

        <div className="studio-runtime-form">
          {/* 申请编号（只读） */}
          <div style={{ marginBottom: 14 }}>
            <div style={{ fontSize: 12, color: "#6b7280", marginBottom: 4 }}>申请编号</div>
            <Input
              value={String(runtimeObject.RequestNo ?? "PR-202505-0001")}
              disabled
              style={{ fontSize: 13, background: "#f9fafb" }}
            />
          </div>

          {/* 申请人（只读） */}
          <div style={{ marginBottom: 14 }}>
            <div style={{ fontSize: 12, color: "#6b7280", marginBottom: 4 }}>申请人</div>
            <Input
              value={String(runtimeObject.ApplicantName ?? "张三")}
              disabled
              style={{ fontSize: 13, background: "#f9fafb" }}
            />
          </div>

          {/* 所属部门（只读） */}
          <div style={{ marginBottom: 14 }}>
            <div style={{ fontSize: 12, color: "#6b7280", marginBottom: 4 }}>所属部门</div>
            <Input
              value={String(runtimeObject.DepartmentName ?? "研发部")}
              disabled
              style={{ fontSize: 13, background: "#f9fafb" }}
            />
          </div>

          <Divider style={{ margin: "12px 0" }} />

          {/* 申请金额（可编辑） */}
          <div style={{ marginBottom: 14 }}>
            <div style={{ fontSize: 12, color: "#6b7280", marginBottom: 4 }}>
              <span style={{ color: "#f5222d", marginRight: 3 }}>*</span>
              申请金额（元）
            </div>
            <InputNumber
              value={amount}
              onNumberChange={v => setRuntimeObject({ ...runtimeObject, Amount: Number(v ?? 0) })}
              style={{ width: "100%", fontSize: 13 }}
              min={0}
              precision={2}
            />
            {amount <= 0 && (
              <div style={{ fontSize: 11, color: "#f5222d", marginTop: 3 }}>金额必须大于 0</div>
            )}
          </div>

          {/* 申请原因（可编辑） */}
          <div style={{ marginBottom: 14 }}>
            <div style={{ fontSize: 12, color: "#6b7280", marginBottom: 4 }}>
              <span style={{ color: "#f5222d", marginRight: 3 }}>*</span>
              申请原因
            </div>
            <Input
              value={reason}
              onChange={v => setRuntimeObject({ ...runtimeObject, Reason: v })}
              style={{ fontSize: 13 }}
            />
          </div>

          {/* 申请状态（可查看，只读） */}
          <div style={{ marginBottom: 8 }}>
            <div style={{ fontSize: 12, color: "#6b7280", marginBottom: 4 }}>申请状态</div>
            <Select
              value={status}
              onChange={v => setRuntimeObject({ ...runtimeObject, Status: String(v) })}
              optionList={STATUS_OPTIONS}
              style={{ width: "100%", fontSize: 13 }}
            />
          </div>
        </div>

        {/* 执行结果提示 */}
        {resultMsg && (
          <div
            style={{
              padding: "10px 14px",
              borderRadius: 6,
              background: resultMsg.startsWith("✅") ? "#f6ffed" : "#fff2f0",
              border: `1px solid ${resultMsg.startsWith("✅") ? "#b7eb8f" : "#ffccc7"}`,
              fontSize: 13,
              marginBottom: 16,
              color: resultMsg.startsWith("✅") ? "#389e0d" : "#cf1322"
            }}
          >
            {resultMsg}
            {resultMsg.startsWith("✅") && (
              <div style={{ marginTop: 6, fontSize: 12, color: "#52c41a" }}>
                新状态: <Tag color="green" size="small">{String(runtimeObject.Status)}</Tag>
                {amount > 50000 ? "（金额 > 50000，需财务审批）" : "（金额 ≤ 50000，需经理审批）"}
              </div>
            )}
          </div>
        )}

        {/* 按钮区 */}
        <div className="studio-runtime-btn-row">
          <Button
            type="primary"
            theme="solid"
            icon={<IconPlay />}
            loading={submitting}
            onClick={handleSubmit}
          >
            提交审批
          </Button>
          <Button theme="borderless" onClick={handleReset} icon={<IconRefresh />}>
            重置
          </Button>
          <Button theme="borderless" type="tertiary" onClick={() => setPreviewMode(false)}>
            关闭预览
          </Button>
        </div>

        <Divider style={{ margin: "20px 0 12px" }} />

        {/* 运行时数据查看 */}
        <div style={{ fontSize: 12, color: "#6b7280", marginBottom: 8, fontWeight: 600 }}>当前运行时对象</div>
        <pre
          style={{
            background: "#f8fafc",
            border: "1px solid #e5e7eb",
            borderRadius: 6,
            padding: "10px 12px",
            fontSize: 11,
            lineHeight: 1.6,
            color: "#374151",
            overflow: "auto",
            maxHeight: 200
          }}
        >
          {JSON.stringify(runtimeObject, null, 2)}
        </pre>
      </div>
    </SideSheet>
  );
}

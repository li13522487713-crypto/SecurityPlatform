import { useMendixStudioStore } from "../store";
import { DISPLAY_TRACE_STEPS, MOCK_DEBUG_TRACE } from "../data/mock-debug-trace";

export function DebugTraceTable() {
  const latestTrace = useMendixStudioStore(state => state.latestTrace);
  const trace = latestTrace ?? MOCK_DEBUG_TRACE;
  const steps = DISPLAY_TRACE_STEPS;

  const totalMs = steps.reduce((sum, s) => sum + s.durationMs, 0);

  const formatTime = (iso: string) => {
    try {
      return new Date(iso).toLocaleString("zh-CN", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
        hour12: false
      }).replace(/\//g, "-");
    } catch {
      return iso;
    }
  };

  return (
    <div className="studio-bottom-half" style={{ borderRight: "none" }}>
      {/* 标题行 */}
      <div className="studio-bottom-half__tabs">
        <div className="studio-bottom-half__tab studio-bottom-half__tab--active">
          调试追踪（最近一次）
        </div>
        <div style={{ flex: 1 }} />
        <div className="studio-bottom-half__tab" style={{ cursor: "pointer" }}>执行历史</div>
      </div>

      {/* 状态信息条 */}
      <div className="studio-bottom-half__info-bar">
        <span className="studio-bottom-half__status-dot studio-bottom-half__status-dot--success" />
        <span style={{ fontWeight: 600, color: "#52c41a" }}>Completed</span>
        <span>Trace ID: <span style={{ fontFamily: "monospace", color: "#1c2a3a" }}>{trace.traceId}</span></span>
        <span>Flow: Microflow / <span style={{ color: "#0958d9" }}>MF_SubmitPurchaseRequest</span></span>
        <span>耗时: <strong>{totalMs}ms</strong></span>
        <span>开始时间: {formatTime(trace.startedAt)}</span>
        <div
          style={{
            marginLeft: "auto",
            cursor: "pointer",
            color: "#9ca3af",
            fontSize: 14,
            lineHeight: 1
          }}
          title="关闭"
        >
          ×
        </div>
      </div>

      {/* 表格 */}
      <div className="studio-bottom-half__body">
        <table className="studio-trace-table">
          <thead>
            <tr>
              <th style={{ width: 28 }}>#</th>
              <th>节点</th>
              <th style={{ width: 110 }}>类型</th>
              <th style={{ width: 70 }}>状态</th>
              <th style={{ width: 50 }}>耗时</th>
              <th>摘要</th>
            </tr>
          </thead>
          <tbody>
            {steps.map(step => (
              <tr key={step.index}>
                <td style={{ color: "#9ca3af", textAlign: "center" }}>{step.index}</td>
                <td style={{ maxWidth: 160, overflow: "hidden", textOverflow: "ellipsis" }}>
                  {step.nodeName}
                </td>
                <td>
                  <span style={{ fontFamily: "monospace", fontSize: 11, color: "#6b7280" }}>
                    {step.nodeType}
                  </span>
                </td>
                <td>
                  <div className={`studio-trace-status studio-trace-status--${step.status}`}>
                    <span
                      style={{
                        width: 6,
                        height: 6,
                        borderRadius: "50%",
                        background: step.status === "success" ? "var(--studio-success-color)" : "var(--studio-error-color)",
                        display: "inline-block",
                        flexShrink: 0
                      }}
                    />
                    <span style={{ fontSize: 11 }}>
                      {step.status === "success" ? "Success" : step.status === "error" ? "Error" : "Running"}
                    </span>
                  </div>
                </td>
                <td style={{ fontFamily: "monospace", fontSize: 11, color: "#6b7280" }}>
                  {step.durationMs}ms
                </td>
                <td style={{ fontSize: 11, color: "#4b5563", maxWidth: 200, overflow: "hidden", textOverflow: "ellipsis" }} title={step.summary}>
                  {step.summary}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

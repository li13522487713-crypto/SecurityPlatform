import { useMendixStudioStore } from "../store";
import { MOCK_VALIDATION_ERRORS } from "../data/mock-debug-trace";

export function ValidationErrorsTable() {
  const validationErrors = useMendixStudioStore(state => state.validationErrors);
  const errors = validationErrors.length > 0 ? validationErrors : MOCK_VALIDATION_ERRORS;

  const errCount = errors.filter(e => e.severity === "error").length;
  const warnCount = errors.filter(e => e.severity === "warning").length;
  const infoCount = errors.filter(e => e.severity === "info").length;

  return (
    <div className="studio-bottom-half">
      {/* Tab 栏 */}
      <div className="studio-bottom-half__tabs">
        <div className="studio-bottom-half__tab studio-bottom-half__tab--active">
          <span>错误</span>
          <span className="studio-bottom-half__tab-count">{errCount}</span>
        </div>
        <div className="studio-bottom-half__tab">
          <span>警告</span>
          <span className="studio-bottom-half__tab-count studio-bottom-half__tab-count--warn">{warnCount}</span>
        </div>
        <div className="studio-bottom-half__tab">
          <span>提示</span>
          <span className="studio-bottom-half__tab-count studio-bottom-half__tab-count--zero">{infoCount}</span>
        </div>
      </div>

      {/* 表格 */}
      <div className="studio-bottom-half__body">
        <table className="studio-errors-table">
          <thead>
            <tr>
              <th style={{ width: 70 }}>严重性</th>
              <th style={{ width: 60 }}>代码</th>
              <th>消息</th>
              <th>位置</th>
            </tr>
          </thead>
          <tbody>
            {errors.map((error, idx) => (
              <tr key={`${error.code}_${idx}`}>
                <td>
                  <span
                    className={
                      "studio-severity-badge" +
                      (error.severity === "error"
                        ? " studio-severity-badge--error"
                        : error.severity === "warning"
                          ? " studio-severity-badge--warning"
                          : " studio-severity-badge--info")
                    }
                  >
                    <span
                      style={{
                        width: 5,
                        height: 5,
                        borderRadius: "50%",
                        background: "currentColor",
                        display: "inline-block",
                        flexShrink: 0
                      }}
                    />
                    {error.severity === "error" ? "Error" : error.severity === "warning" ? "Warning" : "Info"}
                  </span>
                </td>
                <td style={{ fontFamily: "monospace", fontSize: 11, color: "#6b7280" }}>{error.code}</td>
                <td style={{ maxWidth: 240, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }} title={error.message}>
                  {error.message}
                </td>
                <td style={{ fontSize: 11, color: "#9ca3af", maxWidth: 180, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }} title={error.target.path ?? error.target.id}>
                  {error.target.kind}: {error.target.path ?? error.target.id}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <div style={{ padding: "6px 10px", fontSize: 11, color: "#9ca3af", borderTop: "1px solid #f0f2f5" }}>
          共 {errors.length} 条
        </div>
      </div>
    </div>
  );
}

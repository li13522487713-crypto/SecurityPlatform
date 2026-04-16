import { I18n } from "@coze-arch/i18n";

export function WorkflowErrorFallback() {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        minHeight: "100%",
        padding: 24
      }}
    >
      <div
        style={{
          width: "min(560px, 100%)",
          borderRadius: 24,
          border: "1px solid rgba(15, 23, 42, 0.12)",
          background: "#ffffff",
          boxShadow: "0 20px 40px rgba(15, 23, 42, 0.08)",
          padding: 24
        }}
      >
        <h2 style={{ margin: 0, color: "#0f172a" }}>
          {I18n.t("workflow_runtime_fallback_title", {}, "Workflow is temporarily unavailable")}
        </h2>
        <p style={{ margin: "12px 0 0", color: "#475569", lineHeight: 1.6 }}>
          {I18n.t(
            "workflow_runtime_fallback_desc",
            {},
            "The workflow runtime failed to initialize. Reload the page after the host finishes preparing dependencies."
          )}
        </p>
        <div style={{ marginTop: 20 }}>
          <button
            type="button"
            style={{
              border: 0,
              borderRadius: 12,
              background: "#2563eb",
              color: "#ffffff",
              cursor: "pointer",
              padding: "10px 16px"
            }}
            onClick={() => {
              if (typeof window !== "undefined") {
                window.location.reload();
              }
            }}
          >
            {I18n.t("workflow_runtime_fallback_retry", {}, "Reload")}
          </button>
        </div>
      </div>
    </div>
  );
}

import { ValidationErrorsTable } from "./validation-errors-table";
import { DebugTraceTable } from "./debug-trace-table";

export function BottomPanel() {
  return (
    <div className="studio-bottom-panel">
      <ValidationErrorsTable />
      <DebugTraceTable />
    </div>
  );
}

import type { CSSProperties } from "react";
import { Steps } from "@douyinfe/semi-ui";

/**
 * 步骤条：替换历史 `atlas-setup-steps`。
 *
 * - 直接复用 Semi `Steps`（基础类型 `basic`，支持横向流程展示）；
 * - `current` 为 0-based 索引，与现有 `SetupSteps` 在 `status-page.tsx` 中的语义保持一致；
 * - 需要支持失败状态时透传 `status="error"`，由调用方控制何时切到错误。
 */
export interface StepsBarStep {
  title: string;
  description?: string;
}

export interface StepsBarProps {
  steps: ReadonlyArray<StepsBarStep>;
  current: number;
  status?: "wait" | "process" | "finish" | "error";
  size?: "small" | "default";
  testId?: string;
  className?: string;
  style?: CSSProperties;
}

export function StepsBar({
  steps,
  current,
  status,
  size = "default",
  testId,
  className,
  style
}: StepsBarProps) {
  return (
    <div className={className} style={{ marginBottom: 24, ...style }} data-testid={testId}>
      <Steps type="basic" current={current} status={status} size={size}>
        {steps.map((step) => (
          <Steps.Step key={step.title} title={step.title} description={step.description} />
        ))}
      </Steps>
    </div>
  );
}

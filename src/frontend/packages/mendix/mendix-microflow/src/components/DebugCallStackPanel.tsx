import { List, Typography } from "@douyinfe/semi-ui";
import type { DebugCallStackFrame } from "../debug/step-debug-ui";

export interface DebugCallStackPanelProps {
  frames: DebugCallStackFrame[];
  onSelectFrame?: (frame: DebugCallStackFrame, index: number) => void;
}

function resolveActiveFrameIndex(frames: DebugCallStackFrame[]): number {
  if (frames.length === 0) {
    return -1;
  }
  const hasDepth = frames.every(frame => Number.isFinite(frame.depth));
  if (!hasDepth) {
    return frames.length - 1;
  }
  let activeIndex = frames.length - 1;
  let maxDepth = Number.NEGATIVE_INFINITY;
  for (let index = 0; index < frames.length; index += 1) {
    const depth = Number(frames[index].depth);
    if (depth >= maxDepth) {
      maxDepth = depth;
      activeIndex = index;
    }
  }
  return activeIndex;
}

export function DebugCallStackPanel({ frames, onSelectFrame }: DebugCallStackPanelProps) {
  const { Text } = Typography;
  const activeIndex = resolveActiveFrameIndex(frames);
  const renderItem = (item: DebugCallStackFrame, index?: number) => {
    const frame = item as DebugCallStackFrame;
    const frameIndex = index ?? 0;
    const active = frameIndex === activeIndex;
    return (
      <List.Item
        style={{
          cursor: frame.microflowId && onSelectFrame ? "pointer" : "default",
          background: active ? "rgba(96, 165, 250, 0.08)" : "transparent",
        }}
        onClick={() => {
          if (!frame.microflowId || !onSelectFrame) {
            return;
          }
          onSelectFrame(frame, frameIndex);
        }}
      >
        <span
          style={{ display: "inline-block", width: 18 }}
          data-testid={`microflow-debug-callstack-active-${frame.id}`}
        >
          {active ? "▶" : " "}
        </span>
        <Text style={{ color: active ? "#c8d0e8" : "#6a7490" }}>{frame.name}</Text>
      </List.Item>
    );
  };

  return <List dataSource={frames} renderItem={renderItem} />;
}

import { List, Typography } from "@douyinfe/semi-ui";
import type { DebugCallStackFrame } from "../debug/step-debug-ui";

export interface DebugCallStackPanelProps {
  frames: DebugCallStackFrame[];
  onSelectFrame?: (frame: DebugCallStackFrame, index: number) => void;
}

export function DebugCallStackPanel({ frames, onSelectFrame }: DebugCallStackPanelProps) {
  const { Text } = Typography;
  const renderItem = (item: DebugCallStackFrame, index?: number) => {
    const frame = item as DebugCallStackFrame;
    const active = index === 0;
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
          onSelectFrame(frame, index ?? 0);
        }}
      >
        <span style={{ display: "inline-block", width: 18 }}>{active ? "▶" : " "}</span>
        <Text style={{ color: active ? "#c8d0e8" : "#6a7490" }}>{frame.name}</Text>
      </List.Item>
    );
  };

  return <List dataSource={frames} renderItem={renderItem} />;
}

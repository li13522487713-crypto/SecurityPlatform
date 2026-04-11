import { Button, List } from "antd";
import type { CanvasValidationResult } from "../editor/editor-validation";

interface ProblemPanelProps {
  visible: boolean;
  validation: CanvasValidationResult | null;
  onClose: () => void;
  onSelectNode: (nodeKey: string) => void;
}

export function ProblemPanel(props: ProblemPanelProps) {
  if (!props.visible) {
    return null;
  }

  const canvasIssues = props.validation?.canvasIssues ?? [];
  const nodeIssues = props.validation?.nodeResults.filter((item) => item.issues.length > 0) ?? [];

  return (
    <div className="wf-react-problem-panel">
      <div className="wf-react-test-header">
        <div>问题列表</div>
        <Button size="small" onClick={props.onClose}>
          关闭
        </Button>
      </div>
      <List
        size="small"
        dataSource={[
          ...canvasIssues.map((issue) => ({ key: `canvas-${issue}`, label: `画布: ${issue}`, nodeKey: "" })),
          ...nodeIssues.map((item) => ({ key: item.nodeKey, label: `${item.nodeKey}: ${item.issues[0]}`, nodeKey: item.nodeKey }))
        ]}
        renderItem={(item) => (
          <List.Item
            actions={
              item.nodeKey
                ? [
                    <Button key="goto" type="link" size="small" onClick={() => props.onSelectNode(item.nodeKey)}>
                      定位
                    </Button>
                  ]
                : undefined
            }
          >
            {item.label}
          </List.Item>
        )}
      />
    </div>
  );
}

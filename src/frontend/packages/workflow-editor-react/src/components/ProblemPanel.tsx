import { Button, List } from "antd";
import { useTranslation } from "react-i18next";
import type { CanvasValidationResult } from "../editor/editor-validation";

interface ProblemPanelProps {
  visible: boolean;
  validation: CanvasValidationResult | null;
  onClose: () => void;
  onSelectNode: (nodeKey: string) => void;
}

export function ProblemPanel(props: ProblemPanelProps) {
  const { t } = useTranslation();
  if (!props.visible) {
    return null;
  }

  const canvasIssues = props.validation?.canvasIssues ?? [];
  const nodeIssues = props.validation?.nodeResults.filter((item) => item.issues.length > 0) ?? [];

  return (
    <div className="wf-react-problem-panel">
      <div className="wf-react-test-header">
        <div className="wf-react-panel-title">{t("wfUi.problem.title")}</div>
        <Button size="small" onClick={props.onClose}>
          {t("wfUi.problem.close")}
        </Button>
      </div>
      <List
        size="small"
        dataSource={[
          ...canvasIssues.map((issue) => ({ key: `canvas-${issue}`, label: `${t("wfUi.problem.canvas")}: ${issue}`, nodeKey: "" })),
          ...nodeIssues.map((item) => ({ key: item.nodeKey, label: `${item.nodeKey}: ${item.issues[0]}`, nodeKey: item.nodeKey }))
        ]}
        renderItem={(item) => (
          <List.Item
            actions={
              item.nodeKey
                ? [
                    <Button key="goto" type="link" size="small" onClick={() => props.onSelectNode(item.nodeKey)}>
                      {t("wfUi.problem.locate")}
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

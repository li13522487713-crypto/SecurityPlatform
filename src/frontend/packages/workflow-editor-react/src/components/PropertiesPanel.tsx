import { Button, Collapse, Form, Input, Select, Switch } from "antd";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import type { WorkflowNodeCatalogItem } from "../constants/node-catalog";

interface PropertiesPanelProps {
  selectedNode: WorkflowNodeCatalogItem | null;
  visible: boolean;
  onClose: () => void;
}

function renderNodeSpecificFields(nodeType: string) {
  switch (nodeType) {
    case "Entry":
      return (
        <>
          <Form.Item label="会话变量">
            <Input placeholder="USER_INPUT" />
          </Form.Item>
          <Form.Item label="自动保存历史" valuePropName="checked">
            <Switch defaultChecked />
          </Form.Item>
        </>
      );
    case "Exit":
      return (
        <>
          <Form.Item label="终止策略">
            <Select options={[{ value: "return", label: "返回输出" }, { value: "interrupt", label: "中断" }]} />
          </Form.Item>
          <Form.Item label="输出模板">
            <Input.TextArea rows={3} />
          </Form.Item>
        </>
      );
    case "Llm":
      return (
        <>
          <Form.Item label="模型">
            <Select options={[{ value: "qwen-max", label: "qwen-max" }, { value: "llama3", label: "llama3" }]} />
          </Form.Item>
          <Form.Item label="系统提示词">
            <Input.TextArea rows={4} />
          </Form.Item>
        </>
      );
    case "CodeRunner":
      return (
        <>
          <Form.Item label="运行时">
            <Select options={[{ value: "javascript", label: "JavaScript" }, { value: "python", label: "Python" }]} />
          </Form.Item>
          <Form.Item label="代码">
            <Input.TextArea rows={8} />
          </Form.Item>
        </>
      );
    case "Selector":
      return (
        <>
          <Form.Item label="条件表达式">
            <Input.TextArea rows={3} />
          </Form.Item>
          <Form.Item label="默认分支">
            <Select options={[{ value: "true", label: "true" }, { value: "false", label: "false" }]} />
          </Form.Item>
        </>
      );
    default:
      return (
        <>
          <Form.Item label="配置 JSON">
            <Input.TextArea rows={6} />
          </Form.Item>
          <Form.Item label="启用">
            <Switch defaultChecked />
          </Form.Item>
        </>
      );
  }
}

export function PropertiesPanel(props: PropertiesPanelProps) {
  const { t } = useTranslation();
  const items = useMemo(() => {
    if (!props.selectedNode) {
      return [];
    }
    return [
      {
        key: "basic",
        label: "基础配置",
        children: (
          <Form layout="vertical" size="small">
            <Form.Item label={t("wfUi.properties.labelTitle")}>
              <Input defaultValue={t(props.selectedNode.titleKey)} />
            </Form.Item>
            <Form.Item label={t("wfUi.properties.labelType")}>
              <Input value={props.selectedNode.type} readOnly />
            </Form.Item>
            {renderNodeSpecificFields(props.selectedNode.type)}
          </Form>
        )
      },
      {
        key: "advanced",
        label: "高级",
        children: (
          <Form layout="vertical" size="small">
            <Form.Item label="输入映射">
              <Input.TextArea rows={4} placeholder="input.user = {{entry.user}}" />
            </Form.Item>
            <Form.Item label="元信息">
              <Input.TextArea rows={4} placeholder='{"retry":3}' />
            </Form.Item>
          </Form>
        )
      }
    ];
  }, [props.selectedNode, t]);

  if (!props.visible || !props.selectedNode) {
    return null;
  }

  return (
    <div className="wf-react-properties-panel">
      <div className="wf-react-properties-header">
        <div>
          <div className="wf-react-properties-title">{t("wfUi.properties.title")}</div>
          <div className="wf-react-properties-subtitle">{t(props.selectedNode.titleKey)}</div>
        </div>
        <Button size="small" onClick={props.onClose}>
          关闭
        </Button>
      </div>
      <Collapse size="small" bordered={false} items={items} defaultActiveKey={["basic", "advanced"]} />
    </div>
  );
}


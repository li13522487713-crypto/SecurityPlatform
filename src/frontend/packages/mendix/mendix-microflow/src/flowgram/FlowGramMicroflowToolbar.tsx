import { Button, Space } from "@douyinfe/semi-ui";
import { IconMapPin, IconMinus, IconPlus, IconRedo, IconRefresh, IconTreeTriangleDown, IconUndo } from "@douyinfe/semi-icons";
import { WorkflowResetLayoutService, usePlayground, useService } from "@flowgram-adapter/free-layout-editor";

export interface FlowGramMicroflowToolbarProps {
  readonly?: boolean;
  canUndo?: boolean;
  canRedo?: boolean;
  onUndo?: () => void;
  onRedo?: () => void;
  onAutoLayout?: () => void;
  miniMapVisible: boolean;
  onToggleMiniMap: () => void;
}

export function FlowGramMicroflowToolbar(props: FlowGramMicroflowToolbarProps) {
  const playground = usePlayground();
  const resetLayout = useService<WorkflowResetLayoutService>(WorkflowResetLayoutService);
  const fitView = () => {
    const service = resetLayout as WorkflowResetLayoutService & { fitView?: () => void };
    service.fitView?.();
  };

  return (
    <div className="microflow-flowgram-toolbar">
      <Space>
        <Button icon={<IconPlus />} size="small" onClick={() => playground.config.zoomin()} />
        <Button icon={<IconMinus />} size="small" onClick={() => playground.config.zoomout()} />
        <Button icon={<IconRefresh />} size="small" onClick={fitView} />
        <Button icon={<IconUndo />} size="small" disabled={!props.canUndo} onClick={props.onUndo} />
        <Button icon={<IconRedo />} size="small" disabled={!props.canRedo} onClick={props.onRedo} />
        <Button
          icon={<IconMapPin />}
          size="small"
          theme={props.miniMapVisible ? "solid" : "light"}
          onClick={props.onToggleMiniMap}
        />
        <Button
          icon={<IconTreeTriangleDown />}
          size="small"
          disabled={props.readonly}
          onClick={() => {
            props.onAutoLayout?.();
            requestAnimationFrame(fitView);
          }}
        >
          Auto
        </Button>
      </Space>
    </div>
  );
}


import { useCallback, useEffect, useState } from "react";
import { Button, Empty, Spin, Toast } from "@douyinfe/semi-ui";

import { createLocalMicroflowResourceAdapter } from "../adapter/local-microflow-resource-adapter";
import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";
import type { MicroflowResource } from "../resource/resource-types";
import { MendixMicroflowEditorEntry } from "./MendixMicroflowEditorEntry";

export interface MendixMicroflowEditorPageProps {
  resourceId: string;
  workspaceId?: string;
  adapter?: MicroflowResourceAdapter;
  onBack?: () => void;
  readonly?: boolean;
}

export function MendixMicroflowEditorPage({ resourceId, workspaceId, adapter: adapterProp, onBack, readonly }: MendixMicroflowEditorPageProps) {
  const [adapter] = useState(() => adapterProp ?? createLocalMicroflowResourceAdapter({ workspaceId }));
  const [resource, setResource] = useState<MicroflowResource>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error>();

  const load = useCallback(async () => {
    setLoading(true);
    setError(undefined);
    try {
      const loaded = await adapter.getMicroflow(resourceId);
      setResource(loaded);
    } catch (caught) {
      setError(caught instanceof Error ? caught : new Error(String(caught)));
    } finally {
      setLoading(false);
    }
  }, [adapter, resourceId]);

  useEffect(() => {
    void load();
  }, [load]);

  if (loading) {
    return <div style={{ height: "100%", display: "flex", alignItems: "center", justifyContent: "center" }}><Spin /></div>;
  }

  if (error) {
    return (
      <Empty title="加载微流失败" description={error.message} style={{ padding: 80 }}>
        <Button onClick={() => void load()}>重试</Button>
      </Empty>
    );
  }

  if (!resource) {
    return (
      <Empty title="微流不存在" description="资源可能已被删除或当前工作区不可见。" style={{ padding: 80 }}>
        {onBack ? <Button onClick={onBack}>返回资源库</Button> : null}
      </Empty>
    );
  }

  return (
    <MendixMicroflowEditorEntry
      resource={resource}
      adapter={adapter}
      readonly={readonly}
      onBack={onBack}
      onSave={saved => {
        setResource(saved);
        Toast.success("微流已保存");
      }}
      onPublish={published => setResource(published)}
    />
  );
}

export const MicroflowEditorRoutePage = MendixMicroflowEditorPage;

import { useCallback, useEffect, useMemo, useState } from "react";
import { Button, Empty, Spin, Toast } from "@douyinfe/semi-ui";

import { createMicroflowAdapterBundle, type MicroflowAdapterBundle } from "../adapter/microflow-adapter-factory";
import { isMicroflowApiClientError } from "../adapter/http/microflow-api-error";
import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";
import type { MicroflowAdapterFactoryConfig } from "../config/microflow-adapter-config";
import type { MicroflowResource } from "../resource/resource-types";
import { MendixMicroflowEditorEntry } from "./MendixMicroflowEditorEntry";

function getEditorLoadErrorTitle(error: Error): string {
  if (!isMicroflowApiClientError(error)) {
    return "加载微流失败";
  }
  if (error.status === 404) {
    return "微流不存在";
  }
  if (error.status === 403 || error.status === 401) {
    return "无权限访问该微流";
  }
  if (error.status === 409) {
    return "微流版本冲突";
  }
  return "微流服务异常";
}

export interface MendixMicroflowEditorPageProps {
  resourceId: string;
  workspaceId?: string;
  tenantId?: string;
  currentUser?: { id: string; name: string; roles?: string[] };
  adapterBundle?: MicroflowAdapterBundle;
  adapterConfig?: MicroflowAdapterFactoryConfig;
  adapter?: MicroflowResourceAdapter;
  onBack?: () => void;
  readonly?: boolean;
}

export function MendixMicroflowEditorPage({ resourceId, workspaceId, tenantId, currentUser, adapterBundle, adapterConfig, adapter: adapterProp, onBack, readonly }: MendixMicroflowEditorPageProps) {
  const bundle = useMemo(() => adapterBundle ?? createMicroflowAdapterBundle({ ...adapterConfig, workspaceId: adapterConfig?.workspaceId ?? workspaceId, tenantId: adapterConfig?.tenantId ?? tenantId, currentUser: adapterConfig?.currentUser ?? currentUser }), [adapterBundle, adapterConfig, currentUser, tenantId, workspaceId]);
  const adapter = adapterProp ?? bundle.resourceAdapter;
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
      <Empty title={getEditorLoadErrorTitle(error)} description={error.message} style={{ padding: 80 }}>
        <Button onClick={() => void load()}>重试</Button>
        {onBack ? <Button onClick={onBack} style={{ marginLeft: 8 }}>返回资源库</Button> : null}
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
      metadataAdapter={bundle.metadataAdapter}
      runtimeAdapter={bundle.runtimeAdapter}
      validationAdapter={bundle.validationAdapter}
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

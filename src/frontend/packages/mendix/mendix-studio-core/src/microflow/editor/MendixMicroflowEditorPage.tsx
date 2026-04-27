import { useCallback, useEffect, useMemo, useState } from "react";
import { Button, Empty, Spin, Toast } from "@douyinfe/semi-ui";

import { createMicroflowAdapterBundle, type MicroflowAdapterBundle } from "../adapter/microflow-adapter-factory";
import { isForbiddenError, isNotFoundError, isUnauthorizedError, isVersionConflictError } from "../adapter/http/microflow-api-error";
import { MicroflowErrorState } from "../components/error";
import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";
import type { MicroflowAdapterFactoryConfig } from "../config/microflow-adapter-config";
import type { MicroflowResource } from "../resource/resource-types";
import { MendixMicroflowEditorEntry } from "./MendixMicroflowEditorEntry";

function getEditorLoadErrorTitle(error: Error): string {
  if (isNotFoundError(error)) {
    return "微流不存在";
  }
  if (isForbiddenError(error) || isUnauthorizedError(error)) {
    return "无权限访问该微流";
  }
  if (isVersionConflictError(error)) {
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
  const bundleResult = useMemo(() => {
    try {
      return { bundle: adapterBundle ?? createMicroflowAdapterBundle({ ...adapterConfig, workspaceId: adapterConfig?.workspaceId ?? workspaceId, tenantId: adapterConfig?.tenantId ?? tenantId, currentUser: adapterConfig?.currentUser ?? currentUser }) };
    } catch (caught) {
      return { error: caught instanceof Error ? caught : new Error(String(caught)) };
    }
  }, [adapterBundle, adapterConfig, currentUser, tenantId, workspaceId]);
  const bundle = bundleResult.bundle;
  const adapter = adapterProp ?? bundle?.resourceAdapter;
  const [resource, setResource] = useState<MicroflowResource>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error>();

  const load = useCallback(async () => {
    setLoading(true);
    setError(undefined);
    try {
      if (!adapter) {
        throw bundleResult.error ?? new Error("微流服务未配置。");
      }
      const loaded = await adapter.getMicroflow(resourceId);
      setResource(loaded);
    } catch (caught) {
      setError(caught instanceof Error ? caught : new Error(String(caught)));
    } finally {
      setLoading(false);
    }
  }, [adapter, bundleResult.error, resourceId]);

  useEffect(() => {
    void load();
  }, [load]);

  if (loading) {
    return <div style={{ height: "100%", display: "flex", alignItems: "center", justifyContent: "center" }}><Spin /></div>;
  }

  if (error) {
    return (
      <MicroflowErrorState error={error} title={getEditorLoadErrorTitle(error)} onRetry={() => void load()} onBack={onBack} />
    );
  }

  if (!resource) {
    return (
      <Empty title="微流不存在" description="资源可能已被删除或当前工作区不可见。" style={{ padding: 80 }}>
        {onBack ? <Button onClick={onBack}>返回资源库</Button> : null}
      </Empty>
    );
  }

  if (!adapter) {
    return (
      <Empty title="微流服务未配置" description="请配置 HTTP adapter 的 apiBaseUrl 后重试。" style={{ padding: 80 }}>
        {onBack ? <Button onClick={onBack}>返回资源库</Button> : null}
      </Empty>
    );
  }

  return (
    <MendixMicroflowEditorEntry
      resource={resource}
      adapter={adapter}
      metadataAdapter={bundle?.metadataAdapter}
      runtimeAdapter={bundle?.runtimeAdapter}
      validationAdapter={bundle?.validationAdapter}
      adapterMode={bundle?.mode}
      apiBaseUrl={bundle?.apiBaseUrl}
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

import { useCallback, useEffect, useRef, useState } from "react";
import { Button, Empty, Space, Spin, Tag, Typography } from "@douyinfe/semi-ui";

import type { MicroflowAdapterBundle } from "../adapter/microflow-adapter-factory";
import { createMicroflowApiError, getMicroflowApiError, getMicroflowErrorUserMessage, isNotFoundError } from "../adapter/http/microflow-api-error";
import { MicroflowErrorState } from "../components/error";
import type { MicroflowResource } from "../resource/resource-types";
import { MendixMicroflowEditorEntry } from "../editor/MendixMicroflowEditorEntry";
import type { StudioMicroflowDefinitionView } from "./studio-microflow-types";

export interface MicroflowResourceEditorHostProps {
  microflowId: string;
  workspaceId?: string;
  moduleId?: string;
  adapterBundle?: MicroflowAdapterBundle;
  onResourceUpdated?: (resource: MicroflowResource) => void;
  onDirtyChange?: (dirty: boolean) => void;
  onOpenMicroflow?: (microflowId: string) => void;
  onRefreshResourceList?: () => void | Promise<void>;
  onCloseTab?: () => void;
  microflowResourceIndex?: Record<string, StudioMicroflowDefinitionView>;
}

const { Text } = Typography;

function hasUsableSchema(resource: MicroflowResource): boolean {
  return Boolean(
    resource.schema &&
    resource.schema.id &&
    resource.schema.objectCollection &&
    Array.isArray(resource.schema.objectCollection.objects) &&
    Array.isArray(resource.schema.flows)
  );
}

function getLoadErrorTitle(error: unknown): string {
  if (isNotFoundError(error)) {
    return "Microflow no longer exists";
  }
  const apiError = getMicroflowApiError(error);
  if (apiError.code === "MICROFLOW_UNAUTHORIZED") {
    return "登录已失效";
  }
  if (apiError.code === "MICROFLOW_PERMISSION_DENIED") {
    return "无权限访问该微流";
  }
  return "Microflow schema load failed";
}

export function MicroflowResourceEditorHost({
  microflowId,
  workspaceId,
  moduleId,
  adapterBundle,
  onResourceUpdated,
  onDirtyChange,
  onOpenMicroflow,
  onRefreshResourceList,
  onCloseTab,
  microflowResourceIndex
}: MicroflowResourceEditorHostProps) {
  const requestSeqRef = useRef(0);
  const mountedRef = useRef(false);
  const [resource, setResource] = useState<MicroflowResource>();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<unknown>();

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      requestSeqRef.current += 1;
    };
  }, []);

  const load = useCallback(async () => {
    const requestSeq = requestSeqRef.current + 1;
    requestSeqRef.current = requestSeq;
    setResource(undefined);
    setError(undefined);

    if (!microflowId) {
      setError(new Error("Workbench tab 缺少 microflowId，无法加载真实微流 schema。"));
      return;
    }
    if (!workspaceId) {
      setError(new Error("缺少 workspaceId，无法加载真实微流 schema。"));
      return;
    }
    if (!adapterBundle?.resourceAdapter) {
      setError(new Error("缺少 resourceAdapter，无法加载真实微流 schema。"));
      return;
    }
    if (adapterBundle.mode !== "http") {
      setError(new Error("真实 microflow tab 必须使用 HTTP adapter；local/mock adapter 不能作为真实加载或保存路径。"));
      return;
    }
    if (!adapterBundle.resourceAdapter.getMicroflowSchema) {
      setError(new Error("resourceAdapter.getMicroflowSchema 未配置，无法通过真实 schema API 加载微流。"));
      return;
    }
    if (!adapterBundle.metadataAdapter) {
      setError(new Error("缺少 metadataAdapter；目标页不会回退到 mock metadata。"));
      return;
    }

    setLoading(true);
    try {
      const [loadedResource, loadedSchema] = await Promise.all([
        adapterBundle.resourceAdapter.getMicroflow(microflowId),
        adapterBundle.resourceAdapter.getMicroflowSchema(microflowId)
      ]);
      if (!mountedRef.current || requestSeqRef.current !== requestSeq) {
        return;
      }
      if (!loadedResource) {
        throw createMicroflowApiError(`Microflow ${microflowId} was not found.`, 404);
      }
      const nextResource: MicroflowResource = {
        ...loadedResource,
        schema: loadedSchema
      };
      if (!hasUsableSchema(nextResource)) {
        throw new Error("后端未返回可用的 MicroflowAuthoringSchema；不会回退到 sample schema。");
      }
      setResource(nextResource);
      onDirtyChange?.(false);
      onResourceUpdated?.(nextResource);
    } catch (caught) {
      if (!mountedRef.current || requestSeqRef.current !== requestSeq) {
        return;
      }
      setError(caught);
    } finally {
      if (mountedRef.current && requestSeqRef.current === requestSeq) {
        setLoading(false);
      }
    }
  }, [adapterBundle, microflowId, onDirtyChange, onResourceUpdated, workspaceId]);

  useEffect(() => {
    void load();
  }, [load]);

  if (loading) {
    return (
      <div className="studio-embedded-microflow-state">
        <Spin size="large" />
        <Text type="tertiary">Loading microflow resource and schema...</Text>
      </div>
    );
  }

  if (error) {
    return (
      <MicroflowErrorState
        error={error}
        title={getLoadErrorTitle(error)}
        onRetry={() => void load()}
        onBack={isNotFoundError(error) ? onCloseTab : undefined}
      />
    );
  }

  if (!resource) {
    return (
      <Empty
        title="Microflow schema not found"
        description="未加载到当前微流的真实 schema，不会回退到 sampleOrderProcessingMicroflow。"
        style={{ padding: 80 }}
      >
        <Space>
          <Button type="primary" onClick={() => void load()}>重试</Button>
          {onCloseTab ? <Button onClick={onCloseTab}>关闭 Tab</Button> : null}
        </Space>
      </Empty>
    );
  }

  if (!adapterBundle) {
    return (
      <Empty title="Microflow adapter not ready" description="缺少 adapterBundle，无法打开真实微流编辑器。" style={{ padding: 80 }}>
        <Button type="primary" onClick={() => void load()}>重试</Button>
      </Empty>
    );
  }

  return (
    <div style={{ height: "100%", minHeight: 0, display: "flex", flexDirection: "column" }}>
      {!adapterBundle.validationAdapter ? (
        <div style={{ padding: "8px 12px", borderBottom: "1px solid var(--semi-color-border)", background: "var(--semi-color-warning-light-default)" }}>
          <Space>
            <Tag color="orange">Validation adapter missing</Tag>
            <Text size="small">{getMicroflowErrorUserMessage(new Error("validationAdapter 未配置，保存前仅保留编辑器本地校验。"))}</Text>
          </Space>
        </div>
      ) : null}
      <div style={{ flex: 1, minHeight: 0 }}>
        <MendixMicroflowEditorEntry
          key={`${microflowId}:${resource.schemaId}:${resource.version}`}
          resource={resource}
          adapter={adapterBundle.resourceAdapter}
          workspaceId={workspaceId}
          moduleId={moduleId ?? resource.moduleId}
          metadataAdapter={adapterBundle.metadataAdapter}
          validationAdapter={adapterBundle.validationAdapter}
          runtimeAdapter={adapterBundle.runtimeAdapter}
          adapterMode={adapterBundle.mode}
          apiBaseUrl={adapterBundle.apiBaseUrl}
          onDirtyChange={onDirtyChange}
          onOpenMicroflow={onOpenMicroflow}
          onRefreshResourceList={onRefreshResourceList}
          microflowResourceIndex={microflowResourceIndex}
          onSave={saved => {
            if (!mountedRef.current || saved.id !== microflowId) {
              return;
            }
            setResource(saved);
            onDirtyChange?.(false);
            onResourceUpdated?.(saved);
          }}
          onPublish={published => {
            if (!mountedRef.current || published.id !== microflowId) {
              return;
            }
            setResource(published);
            onDirtyChange?.(false);
            onResourceUpdated?.(published);
          }}
        />
      </div>
    </div>
  );
}

export const StudioMicroflowResourceEditorHost = MicroflowResourceEditorHost;

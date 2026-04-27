import { useCallback, useEffect, useRef, useState } from "react";
import { Button, Empty, Spin, Toast, Typography } from "@douyinfe/semi-ui";

import type { MicroflowAdapterBundle } from "../adapter/microflow-adapter-factory";
import { getMicroflowErrorUserMessage, isNotFoundError } from "../adapter/http/microflow-api-error";
import type { MicroflowResource } from "../resource/resource-types";
import { MendixMicroflowEditorEntry } from "../editor/MendixMicroflowEditorEntry";
import type { StudioMicroflowDefinitionView } from "./studio-microflow-types";

export interface StudioEmbeddedMicroflowEditorProps {
  microflowId?: string;
  workspaceId?: string;
  moduleId?: string;
  adapterBundle?: MicroflowAdapterBundle;
  onResourceUpdated?: (resource: MicroflowResource) => void;
  onDirtyChange?: (dirty: boolean) => void;
  onOpenMicroflow?: (microflowId: string) => void;
  onRefreshResourceList?: () => void | Promise<void>;
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

function getErrorTitle(error: Error): string {
  if (isNotFoundError(error)) {
    return "Microflow no longer exists";
  }
  return "Microflow schema load failed";
}

export function StudioEmbeddedMicroflowEditor({
  microflowId,
  workspaceId,
  moduleId,
  adapterBundle,
  onResourceUpdated,
  onDirtyChange,
  onOpenMicroflow,
  onRefreshResourceList,
  microflowResourceIndex
}: StudioEmbeddedMicroflowEditorProps) {
  const requestSeqRef = useRef(0);
  const [resource, setResource] = useState<MicroflowResource>();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<Error>();

  const load = useCallback(async () => {
    const requestSeq = requestSeqRef.current + 1;
    requestSeqRef.current = requestSeq;
    setResource(undefined);
    setError(undefined);

    if (!workspaceId) {
      setError(new Error("缺少 workspaceId，无法加载真实微流 schema。"));
      return;
    }
    if (!microflowId) {
      setError(new Error("缺少 microflowId，无法加载真实微流 schema。"));
      return;
    }
    if (!adapterBundle) {
      setError(new Error("缺少 adapterBundle，无法加载真实微流 schema。"));
      return;
    }
    if (adapterBundle.mode !== "http") {
      setError(new Error("真实 Workbench 编辑器必须使用 HTTP adapter；local/mock adapter 不能作为真实 schema 保存。"));
      return;
    }
    if (!adapterBundle.resourceAdapter) {
      setError(new Error("缺少 resourceAdapter，无法加载真实微流 schema。"));
      return;
    }
    if (!adapterBundle.runtimeAdapter) {
      setError(new Error("缺少 runtimeAdapter，无法调用真实 schema 加载接口。"));
      return;
    }

    setLoading(true);
    try {
      const [loadedResource, loadedSchema] = await Promise.all([
        adapterBundle.resourceAdapter.getMicroflow(microflowId),
        adapterBundle.runtimeAdapter.loadMicroflow(microflowId)
      ]);
      if (requestSeqRef.current !== requestSeq) {
        return;
      }
      if (!loadedResource) {
        throw new Error(`Microflow ${microflowId} was not found.`);
      }
      const nextResource: MicroflowResource = {
        ...loadedResource,
        schema: loadedSchema
      };
      if (!hasUsableSchema(nextResource)) {
        throw new Error("Microflow schema not found or invalid.");
      }
      setResource(nextResource);
      onDirtyChange?.(false);
      onResourceUpdated?.(nextResource);
    } catch (caught) {
      if (requestSeqRef.current !== requestSeq) {
        return;
      }
      setError(caught instanceof Error ? caught : new Error(String(caught)));
    } finally {
      if (requestSeqRef.current === requestSeq) {
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
        <Text type="tertiary">Loading microflow schema...</Text>
      </div>
    );
  }

  if (error) {
    return (
      <Empty
        title={getErrorTitle(error)}
        description={getMicroflowErrorUserMessage(error)}
        style={{ padding: 80 }}
      >
        <Button type="primary" onClick={() => void load()}>
          Retry
        </Button>
      </Empty>
    );
  }

  if (!resource) {
    return (
      <Empty
        title="Microflow schema not found"
        description="未加载到当前微流的真实 schema，不会回退到 sampleOrderProcessingMicroflow。"
        style={{ padding: 80 }}
      >
        <Button type="primary" onClick={() => void load()}>
          Retry
        </Button>
      </Empty>
    );
  }

  if (!adapterBundle) {
    return (
      <Empty
        title="Microflow adapter not ready"
        description="缺少 adapterBundle，无法打开真实微流编辑器。"
        style={{ padding: 80 }}
      >
        <Button type="primary" onClick={() => void load()}>
          Retry
        </Button>
      </Empty>
    );
  }

  return (
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
        setResource(saved);
        onDirtyChange?.(false);
        onResourceUpdated?.(saved);
        Toast.success("微流已保存到后端");
      }}
      onPublish={published => {
        setResource(published);
        onDirtyChange?.(false);
        onResourceUpdated?.(published);
      }}
    />
  );
}

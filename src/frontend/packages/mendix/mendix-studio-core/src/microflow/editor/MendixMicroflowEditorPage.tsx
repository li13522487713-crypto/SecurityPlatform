import { useCallback, useEffect, useMemo, useState } from "react";
import { Button, Empty, Spin, Toast } from "@douyinfe/semi-ui";

import { createMicroflowAdapterBundle, type MicroflowAdapterBundle } from "../adapter/microflow-adapter-factory";
import { isForbiddenError, isNotFoundError, isUnauthorizedError, isVersionConflictError } from "../adapter/http/microflow-api-error";
import { MicroflowErrorState } from "../components/error";
import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";
import type { MicroflowAdapterFactoryConfig } from "../config/microflow-adapter-config";
import type { MicroflowResource } from "../resource/resource-types";
import { MendixMicroflowEditorEntry } from "./MendixMicroflowEditorEntry";
import { getMendixStudioCopy } from "../../i18n/copy";

function getEditorLoadErrorTitle(error: Error): string {
  const copy = getMendixStudioCopy();
  if (isNotFoundError(error)) {
    return copy.editorPage.notFoundTitle;
  }
  if (isForbiddenError(error) || isUnauthorizedError(error)) {
    return copy.editorPage.forbiddenTitle;
  }
  if (isVersionConflictError(error)) {
    return copy.editorPage.conflictTitle;
  }
  return copy.editorPage.serviceErrorTitle;
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
  const copy = getMendixStudioCopy();
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
        throw bundleResult.error ?? new Error(copy.editorPage.serviceNotConfigured);
      }
      const loaded = await adapter.getMicroflow(resourceId);
      setResource(loaded);
    } catch (caught) {
      setError(caught instanceof Error ? caught : new Error(String(caught)));
    } finally {
      setLoading(false);
    }
  }, [adapter, bundleResult.error, copy.editorPage.serviceNotConfigured, resourceId]);

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
      <Empty title={copy.editorPage.emptyTitle} description={copy.editorPage.emptyDescription} style={{ padding: 80 }}>
        {onBack ? <Button onClick={onBack}>{copy.common.backToLibrary}</Button> : null}
      </Empty>
    );
  }

  if (bundle && bundle.mode !== "http") {
    return (
      <Empty title={copy.editorPage.httpOnlyTitle} description={copy.editorPage.httpOnlyDescription} style={{ padding: 80 }}>
        {onBack ? <Button onClick={onBack}>{copy.common.backToLibrary}</Button> : null}
      </Empty>
    );
  }

  if (!adapter) {
    return (
      <Empty title={copy.editorPage.adapterNotConfiguredTitle} description={copy.editorPage.adapterNotConfiguredDescription} style={{ padding: 80 }}>
        {onBack ? <Button onClick={onBack}>{copy.common.backToLibrary}</Button> : null}
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
        Toast.success(copy.editorPage.saveSuccess);
      }}
      onPublish={published => setResource(published)}
    />
  );
}

export const MicroflowEditorRoutePage = MendixMicroflowEditorPage;

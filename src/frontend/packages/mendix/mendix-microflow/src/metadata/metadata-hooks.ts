import { useMicroflowMetadataContext } from "./metadata-provider";
import type { MicroflowMetadataCatalog } from "./metadata-catalog";

/** 完整元数据上下文（catalog / loading / error / reload）。 */
export function useMicroflowMetadata(): ReturnType<typeof useMicroflowMetadataContext> {
  return useMicroflowMetadataContext();
}

export function useMicroflowMetadataCatalog(): MicroflowMetadataCatalog | null {
  return useMicroflowMetadataContext().catalog;
}

export function useEntityCatalog() {
  return useMicroflowMetadataContext().catalog?.entities ?? [];
}

export function useAssociationCatalog() {
  return useMicroflowMetadataContext().catalog?.associations ?? [];
}

export function useEnumerationCatalog() {
  return useMicroflowMetadataContext().catalog?.enumerations ?? [];
}

export function useMicroflowRefCatalog() {
  return useMicroflowMetadataContext().catalog?.microflows ?? [];
}

export function usePageCatalog() {
  return useMicroflowMetadataContext().catalog?.pages ?? [];
}

export function useWorkflowCatalog() {
  return useMicroflowMetadataContext().catalog?.workflows ?? [];
}

export function useMetadataStatus(): Pick<
  ReturnType<typeof useMicroflowMetadataContext>,
  "loading" | "error" | "version" | "reload" | "refresh"
> {
  const { loading, error, version, reload, refresh } = useMicroflowMetadataContext();
  return { loading, error, version, reload, refresh };
}

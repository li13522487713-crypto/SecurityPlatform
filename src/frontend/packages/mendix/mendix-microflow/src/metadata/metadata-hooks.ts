import { useContext } from "react";
import { MicroflowMetadataContext } from "./metadata-provider";

export function useMicroflowMetadata() {
  return useContext(MicroflowMetadataContext);
}

export function useEntityCatalog() {
  return useMicroflowMetadata().entities;
}

export function useEnumerationCatalog() {
  return useMicroflowMetadata().enumerations;
}

export function useMicroflowCatalog() {
  return useMicroflowMetadata().microflows;
}

export function usePageCatalog() {
  return useMicroflowMetadata().pages;
}

export function useWorkflowCatalog() {
  return useMicroflowMetadata().workflows;
}

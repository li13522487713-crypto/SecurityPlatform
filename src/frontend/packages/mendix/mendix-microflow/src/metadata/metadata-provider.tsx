import { createContext, type ReactNode } from "react";
import type { MicroflowMetadataCatalog } from "./metadata-catalog";
import { mockMicroflowMetadataCatalog } from "./mock-metadata";

export const MicroflowMetadataContext = createContext<MicroflowMetadataCatalog>(mockMicroflowMetadataCatalog);

export function MicroflowMetadataProvider({
  catalog = mockMicroflowMetadataCatalog,
  children,
}: {
  catalog?: MicroflowMetadataCatalog;
  children: ReactNode;
}) {
  return (
    <MicroflowMetadataContext.Provider value={catalog}>
      {children}
    </MicroflowMetadataContext.Provider>
  );
}

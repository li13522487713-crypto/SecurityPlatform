import { createContext, useContext, useMemo, type ReactNode } from "react";

interface OrganizationContextValue {
  orgId: string;
}

const OrganizationContext = createContext<OrganizationContextValue | null>(null);

export function OrganizationProvider({ orgId, children }: { orgId: string; children: ReactNode }) {
  const value = useMemo<OrganizationContextValue>(() => ({ orgId }), [orgId]);
  return <OrganizationContext.Provider value={value}>{children}</OrganizationContext.Provider>;
}

export function useOrganizationContext() {
  const context = useContext(OrganizationContext);
  if (!context) {
    throw new Error("OrganizationProvider is missing.");
  }
  return context;
}

export function useOptionalOrganizationContext() {
  return useContext(OrganizationContext);
}

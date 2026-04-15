import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { getWorkspaceById, type WorkspaceDetailDto } from "@/services/api-org-workspaces";

interface WorkspaceContextValue extends WorkspaceDetailDto {
  loading: boolean;
  reload: () => Promise<void>;
}

const WorkspaceContext = createContext<WorkspaceContextValue | null>(null);

export function WorkspaceProvider({
  orgId,
  workspaceId,
  children
}: {
  orgId: string;
  workspaceId: string;
  children: ReactNode;
}) {
  const [loading, setLoading] = useState(true);
  const [detail, setDetail] = useState<WorkspaceDetailDto>({
    id: workspaceId,
    orgId,
    name: "",
    description: "",
    icon: "",
    appInstanceId: "",
    appKey: "",
    roleCode: "Member",
    allowedActions: [],
    createdAt: "",
    lastVisitedAt: undefined
  });

  const reload = async () => {
    setLoading(true);
    try {
      const nextDetail = await getWorkspaceById(orgId, workspaceId);
      setDetail(nextDetail);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void reload();
  }, [orgId, workspaceId]);

  const value = useMemo<WorkspaceContextValue | null>(() => {
    return {
      ...detail,
      loading,
      reload
    };
  }, [detail, loading]);

  return <WorkspaceContext.Provider value={value}>{children}</WorkspaceContext.Provider>;
}

export function useWorkspaceContext() {
  const context = useContext(WorkspaceContext);
  if (!context) {
    throw new Error("WorkspaceProvider is missing.");
  }
  return context;
}

export function useOptionalWorkspaceContext() {
  return useContext(WorkspaceContext);
}

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { getTenantId } from "@atlas/shared-react-core/utils";
import { useShallow } from "zustand/react/shallow";
import type { BotSpace } from "@coze-arch/bot-api/developer_api";
import { useSpaceStore } from "@coze-foundation/space-store-adapter";
import { getWorkspaceByIdOrNull, type WorkspaceDetailDto } from "../services/api-org-workspaces";
import { getConfiguredAppKey } from "../services/api-core";

interface WorkspaceContextValue extends WorkspaceDetailDto {
  loading: boolean;
  reload: () => Promise<void>;
}

const WorkspaceContext = createContext<WorkspaceContextValue | null>(null);

interface CozeSpaceStoreSnapshot {
  spaceList: BotSpace[];
  inited?: boolean;
}

function buildEmptyDetail(workspaceId: string, orgId: string): WorkspaceDetailDto {
  return {
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
  };
}

/**
 * 工作空间上下文 Provider。
 *
 * - 旧 `/org/:orgId/workspaces/:workspaceId/...` 路由：必须传 `orgId`。
 * - 新 PRD 风格 `/workspace/:workspaceId/...` 路由：`orgId` 可省略，将自动回退到当前租户 ID。
 *
 * 数据源：`getWorkspaceByIdOrNull(orgId, workspaceId)`。失败时退化为占位 detail，避免阻断渲染。
 */
export function WorkspaceProvider({
  orgId,
  workspaceId,
  children
}: {
  orgId?: string;
  workspaceId: string;
  children: ReactNode;
}) {
  const resolvedOrgId = useMemo(() => (orgId ?? getTenantId() ?? ""), [orgId]);
  const { spaceList, inited } = useSpaceStore(
    useShallow((state: CozeSpaceStoreSnapshot) => ({
      spaceList: state.spaceList,
      inited: state.inited
    }))
  );
  const [loading, setLoading] = useState(true);
  const [detail, setDetail] = useState<WorkspaceDetailDto>(() => buildEmptyDetail(workspaceId, resolvedOrgId));
  const cozeSpaceDetail = useMemo<WorkspaceDetailDto | null>(() => {
    if (!inited) {
      return null;
    }

    const matchedSpace = spaceList.find((item) => String(item.id ?? "").trim() === workspaceId);
    if (!matchedSpace) {
      return null;
    }

    const fallbackAppKey = getConfiguredAppKey();
    return {
      id: workspaceId,
      orgId: resolvedOrgId,
      name: String(matchedSpace.name ?? "").trim(),
      description: String(matchedSpace.description ?? "").trim(),
      icon: String(matchedSpace.icon_url ?? "").trim(),
      appInstanceId: "",
      appKey: fallbackAppKey,
      roleCode: "Member",
      allowedActions: [],
      createdAt: "",
      lastVisitedAt: undefined
    };
  }, [inited, resolvedOrgId, spaceList, workspaceId]);

  const reload = async () => {
    if (!resolvedOrgId || !workspaceId) {
      setDetail(buildEmptyDetail(workspaceId, resolvedOrgId));
      setLoading(false);
      return;
    }

    if (cozeSpaceDetail) {
      setDetail(cozeSpaceDetail);
      setLoading(false);
      return;
    }

    setLoading(true);
    try {
      const nextDetail = await getWorkspaceByIdOrNull(resolvedOrgId, workspaceId);
      setDetail(nextDetail ?? buildEmptyDetail(workspaceId, resolvedOrgId));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void reload();
  }, [cozeSpaceDetail, resolvedOrgId, workspaceId]);

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

/*
 * Atlas Foundation Bridge —— 宿主侧状态总线
 *
 * 设计目标：
 * 1) Atlas 不依赖 cozelib 自带的 account-adapter / space-store；
 * 2) cozelib 通过 alias 解析到本桥接包后，所有 useUserInfo/useSpace 等 hook
 *    都通过这里读取 Atlas 已经维护的用户信息、租户、空间与主题；
 * 3) 宿主（apps/app-web）在 `WorkflowRuntimeBoundary` 等关键时机调用
 *    `setAtlasFoundationHost(...)` 注入快照，避免 cozelib 在 useUserInfo 取到 null
 *    导致整页崩溃。
 *
 * 注意：本文件是 framework-agnostic 的事件源，不能依赖 React 之外的运行时。
 */

export type AtlasBridgeLoginStatus = "settling" | "not_login" | "logined";

export type AtlasBridgeThemeType = "dark" | "light" | "system";

export interface AtlasBridgeUserInfo {
  userIdStr: string;
  name: string;
  screenName: string;
  email?: string;
  avatarUrl?: string;
  locale?: "zh-CN" | "en-US";
  tenantId?: string;
}

export interface AtlasBridgeSpaceInfo {
  id: string;
  name: string;
  description?: string;
  iconUrl?: string;
  /** 1=Personal / 2=Team；与上游 BotSpace.space_type 对齐。 */
  spaceType: number;
  /** 0=Normal / 1=DevMode；与上游 SpaceMode 对齐。 */
  spaceMode?: number;
  /** 1=Owner / 2=Admin / 3=Member；与上游 RoleType 对齐。 */
  roleType?: number;
}

export interface AtlasBridgeHostState {
  loginStatus: AtlasBridgeLoginStatus;
  theme: AtlasBridgeThemeType;
  user: AtlasBridgeUserInfo | null;
  spaces: AtlasBridgeSpaceInfo[];
  /** 当前激活空间 id；若为空则使用 spaces 第一项做兜底。 */
  activeSpaceId?: string;
}

const DEFAULT_STATE: AtlasBridgeHostState = {
  loginStatus: "settling",
  theme: "light",
  user: null,
  spaces: [],
  activeSpaceId: undefined,
};

type Listener = (state: AtlasBridgeHostState) => void;

let currentState: AtlasBridgeHostState = DEFAULT_STATE;
const listeners = new Set<Listener>();

/**
 * 替换桥接状态。`partial` 仅描述变更字段，未提供的字段保持上一次快照。
 * 当 user / theme / spaces 实际变化时才广播事件，避免 cozelib re-render 风暴。
 */
export function setAtlasFoundationHost(partial: Partial<AtlasBridgeHostState>): void {
  const next: AtlasBridgeHostState = {
    ...currentState,
    ...partial,
  };

  if (areStatesEqual(currentState, next)) {
    return;
  }

  currentState = next;
  for (const listener of listeners) {
    try {
      listener(currentState);
    } catch {
      // 任意监听器异常不应阻断后续广播。
    }
  }
}

/** 读取当前状态快照（同步）。 */
export function getAtlasFoundationHost(): AtlasBridgeHostState {
  return currentState;
}

export function subscribeAtlasFoundationHost(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

/** 测试 / 卸载场景下重置桥接状态。 */
export function resetAtlasFoundationHost(): void {
  currentState = DEFAULT_STATE;
}

function areStatesEqual(a: AtlasBridgeHostState, b: AtlasBridgeHostState): boolean {
  if (a === b) {
    return true;
  }

  if (a.loginStatus !== b.loginStatus || a.theme !== b.theme || a.activeSpaceId !== b.activeSpaceId) {
    return false;
  }

  if (!areUsersEqual(a.user, b.user)) {
    return false;
  }

  return areSpaceListsEqual(a.spaces, b.spaces);
}

function areUsersEqual(a: AtlasBridgeUserInfo | null, b: AtlasBridgeUserInfo | null): boolean {
  if (a === b) {
    return true;
  }
  if (!a || !b) {
    return false;
  }
  return (
    a.userIdStr === b.userIdStr &&
    a.name === b.name &&
    a.screenName === b.screenName &&
    a.email === b.email &&
    a.avatarUrl === b.avatarUrl &&
    a.locale === b.locale &&
    a.tenantId === b.tenantId
  );
}

function areSpaceListsEqual(a: AtlasBridgeSpaceInfo[], b: AtlasBridgeSpaceInfo[]): boolean {
  if (a === b) {
    return true;
  }
  if (a.length !== b.length) {
    return false;
  }
  for (let index = 0; index < a.length; index += 1) {
    const left = a[index];
    const right = b[index];
    if (
      left.id !== right.id ||
      left.name !== right.name ||
      left.description !== right.description ||
      left.iconUrl !== right.iconUrl ||
      left.spaceType !== right.spaceType ||
      left.spaceMode !== right.spaceMode ||
      left.roleType !== right.roleType
    ) {
      return false;
    }
  }
  return true;
}

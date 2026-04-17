/*
 * Atlas Foundation Bridge —— 对外暴露的 cozelib API 实现。
 *
 * 本包通过 rsbuild alias 替换 `@coze-arch/foundation-sdk` 与
 * `@coze-foundation/foundation-sdk`，以避免 cozelib 自带的
 * account-adapter / space-store 在 Atlas 宿主中实例化两套用户态。
 *
 * 所有响应式 API 均订阅 `setAtlasFoundationHost` 注入的快照，
 * 宿主负责在 WorkflowRuntimeBoundary、AuthProvider 等关键点调用注入。
 */

import {
  createElement,
  useCallback,
  useEffect,
  useMemo,
  useState,
  type FC,
  type ReactNode,
} from "react";
import {
  getAtlasFoundationHost,
  subscribeAtlasFoundationHost,
  type AtlasBridgeLoginStatus,
  type AtlasBridgeSpaceInfo,
  type AtlasBridgeThemeType,
  type AtlasBridgeUserInfo,
} from "./atlas-host-bridge";

export {
  setAtlasFoundationHost,
  getAtlasFoundationHost,
  resetAtlasFoundationHost,
  subscribeAtlasFoundationHost,
} from "./atlas-host-bridge";
export type {
  AtlasBridgeHostState,
  AtlasBridgeLoginStatus,
  AtlasBridgeSpaceInfo,
  AtlasBridgeThemeType,
  AtlasBridgeUserInfo,
} from "./atlas-host-bridge";

// ---------- 与 cozelib `@coze-arch/foundation-sdk` 类型对齐的最小接口 ----------

export type ThemeType = AtlasBridgeThemeType;
export type LoginStatus = AtlasBridgeLoginStatus;

export type OAuth2StateType = "login" | "delete_account" | "oauth";

export interface OAuth2RedirectConfig {
  navigatePath?: string;
  type: OAuth2StateType;
  extra?: Record<string, string | undefined>;
  scope?: string;
  optionalScope?: string;
}

export interface UserInfo {
  app_id: number;
  user_id: number;
  user_id_str: string;
  odin_user_type: number;
  name: string;
  screen_name: string;
  avatar_url: string;
  user_verified: boolean;
  email?: string;
  email_collected: boolean;
  expend_attrs?: Record<string, unknown>;
  phone_collected: boolean;
  verified_content: string;
  verified_agency: string;
  is_blocked: number;
  is_blocking: number;
  bg_img_url: string;
  gender: number;
  media_id: number;
  user_auth_info: string;
  industry: string;
  area: string;
  can_be_found_by_phone: number;
  mobile: string;
  birthday: string;
  description: string;
  status: number;
  new_user: number;
  first_login_app: number;
  session_key: string;
  is_recommend_allowed: number;
  recommend_hint_message: string;
  followings_count: number;
  followers_count: number;
  visit_count_recent: number;
  skip_edit_profile: number;
  is_manual_set_user_info: boolean;
  device_id: number;
  country_code: number;
  has_password: number;
  share_to_repost: number;
  user_decoration: string;
  user_privacy_extend: number;
  old_user_id: number;
  old_user_id_str: string;
  sec_user_id: string;
  sec_old_user_id: string;
  vcd_account: number;
  vcd_relation: number;
  can_bind_visitor_account: boolean;
  is_visitor_account: boolean;
  is_only_bind_ins: boolean;
  user_device_record_status: number;
  is_kids_mode: number;
  source: string;
  is_employee: boolean;
  passport_enterprise_user_type: number;
  need_device_create: number;
  need_ttwid_migration: number;
  user_auth_status: number;
  user_safe_mobile_2fa: string;
  safe_mobile_country_code: number;
  lite_user_info_string: string;
  lite_user_info_demotion: number;
  app_user_info: { user_unique_name?: string };
  need_check_bind_status: boolean;
  locale?: "zh-CN" | "en-US";
}

export interface UserConnectItem {
  platform: string;
  profile_image_url: string;
  expired_time: number;
  expires_in: number;
  platform_screen_name: string;
  user_id: number;
  platform_uid: string;
  sec_platform_uid: string;
  platform_app_id: number;
  modify_time: number;
  access_token: string;
  open_id: string;
}

export interface UserAuthInfo {
  platform?: string;
  third_id?: string;
}

export interface UserLabel {
  label_id?: string;
  label_name?: string;
}

export interface BackButtonProps {
  onClickBack: () => void;
}

export interface NavBtnProps {
  navKey: string;
  icon?: ReactNode;
  label: string | ReactNode;
  suffix?: string | ReactNode;
  onlyShowInDefault?: boolean;
  onClick: (event: React.MouseEvent) => void;
}

export interface BotSpace {
  id?: string;
  name?: string;
  description?: string;
  icon_url?: string;
  space_type?: number;
  role_type?: number;
  space_mode?: number;
}

export interface MenuItem {
  label: string;
  icon?: ReactNode;
  url?: string;
  renderType: "link" | "popover" | "menu" | "comp" | "test-new-link";
  comp?: ReactNode;
}

// ---------- 同步 API ----------

export function getLoginStatus(): LoginStatus {
  return getAtlasFoundationHost().loginStatus;
}

export function getIsSettled(): boolean {
  return getLoginStatus() !== "settling";
}

export function getIsLogined(): boolean {
  return getLoginStatus() === "logined";
}

export function getUserInfo(): UserInfo | null {
  const snapshot = getAtlasFoundationHost().user;
  return snapshot ? toLegacyUserInfo(snapshot) : null;
}

export async function getUserAuthInfos(): Promise<void> {
  // Atlas 宿主目前不维护三方授权，保持空实现以满足 cozelib 调用契约。
  return;
}

export async function refreshUserInfo(): Promise<void> {
  // 真正的刷新动作由 Atlas AuthContext.ensureProfile 触发，
  // 这里做空实现避免 cozelib 强制依赖刷新返回值。
  return;
}

export async function logoutOnly(): Promise<void> {
  // 实际登出由 Atlas AuthContext.logout 处理，桥接层不直接吊起 cozelib 登出。
  return;
}

export async function uploadAvatar(_file: File): Promise<{ web_uri: string }> {
  // Atlas 暂未对接 cozelib 头像上传通道，返回空 URI 供 cozelib 兜底。
  return { web_uri: "" };
}

export function subscribeUserAuthInfos(
  callback: (state: UserAuthInfo[], prev: UserAuthInfo[]) => void,
): () => void {
  let prev: UserAuthInfo[] = [];
  const dispose = subscribeAtlasFoundationHost(() => {
    callback([], prev);
    prev = [];
  });
  return dispose;
}

// ---------- React Hooks ----------

function useAtlasFoundationSnapshot() {
  const [snapshot, setSnapshot] = useState(getAtlasFoundationHost);
  useEffect(() => {
    return subscribeAtlasFoundationHost((next) => {
      setSnapshot(next);
    });
  }, []);
  return snapshot;
}

export function useLoginStatus(): LoginStatus {
  return useAtlasFoundationSnapshot().loginStatus;
}

export function useIsSettled(): boolean {
  return useLoginStatus() !== "settling";
}

export function useIsLogined(): boolean {
  return useLoginStatus() === "logined";
}

export function useUserInfo(): UserInfo | null {
  const snapshot = useAtlasFoundationSnapshot();
  return useMemo(() => (snapshot.user ? toLegacyUserInfo(snapshot.user) : null), [snapshot.user]);
}

export function useUserAuthInfo(): UserAuthInfo[] {
  return [];
}

export function useUserLabel(): UserLabel | null {
  return null;
}

export function useCurrentTheme(): ThemeType {
  return useAtlasFoundationSnapshot().theme;
}

export function useSpace(spaceId: string): BotSpace | undefined {
  const snapshot = useAtlasFoundationSnapshot();
  return useMemo(() => resolveSpace(snapshot.spaces, spaceId, snapshot.activeSpaceId), [
    snapshot.spaces,
    snapshot.activeSpaceId,
    spaceId,
  ]);
}

// ---------- 简易布局组件（Semi 主题外的最小渲染兜底） ----------

export const SideSheetMenu: FC = () => null;

export const BackButton: FC<BackButtonProps> = ({ onClickBack }) => {
  const handleClick = useCallback(() => onClickBack(), [onClickBack]);
  return createElement(
    "button",
    {
      type: "button",
      onClick: handleClick,
      className: "atlas-foundation-back-button",
      "aria-label": "back",
    },
    "←",
  );
};

// ---------- 工具函数 ----------

function toLegacyUserInfo(snapshot: AtlasBridgeUserInfo): UserInfo {
  const numericId = parseLegacyId(snapshot.userIdStr);
  return {
    app_id: 0,
    user_id: numericId,
    user_id_str: snapshot.userIdStr,
    odin_user_type: 0,
    name: snapshot.name,
    screen_name: snapshot.screenName,
    avatar_url: snapshot.avatarUrl ?? "",
    user_verified: false,
    email: snapshot.email,
    email_collected: Boolean(snapshot.email),
    phone_collected: false,
    verified_content: "",
    verified_agency: "",
    is_blocked: 0,
    is_blocking: 0,
    bg_img_url: "",
    gender: 0,
    media_id: 0,
    user_auth_info: "",
    industry: "",
    area: "",
    can_be_found_by_phone: 0,
    mobile: "",
    birthday: "",
    description: "",
    status: 0,
    new_user: 0,
    first_login_app: 0,
    session_key: "",
    is_recommend_allowed: 0,
    recommend_hint_message: "",
    followings_count: 0,
    followers_count: 0,
    visit_count_recent: 0,
    skip_edit_profile: 1,
    is_manual_set_user_info: true,
    device_id: 0,
    country_code: 0,
    has_password: 1,
    share_to_repost: 0,
    user_decoration: "",
    user_privacy_extend: 0,
    old_user_id: 0,
    old_user_id_str: "",
    sec_user_id: "",
    sec_old_user_id: "",
    vcd_account: 0,
    vcd_relation: 0,
    can_bind_visitor_account: false,
    is_visitor_account: false,
    is_only_bind_ins: false,
    user_device_record_status: 0,
    is_kids_mode: 0,
    source: "atlas",
    is_employee: true,
    passport_enterprise_user_type: 0,
    need_device_create: 0,
    need_ttwid_migration: 0,
    user_auth_status: 1,
    user_safe_mobile_2fa: "",
    safe_mobile_country_code: 0,
    lite_user_info_string: "",
    lite_user_info_demotion: 0,
    app_user_info: { user_unique_name: snapshot.screenName },
    need_check_bind_status: false,
    locale: snapshot.locale,
  };
}

function parseLegacyId(value: string): number {
  if (!value) {
    return 0;
  }
  const numeric = Number(value);
  return Number.isFinite(numeric) ? numeric : 0;
}

function resolveSpace(
  spaces: AtlasBridgeSpaceInfo[],
  spaceId: string,
  activeSpaceId?: string,
): BotSpace | undefined {
  const lookup = (id: string | undefined): AtlasBridgeSpaceInfo | undefined => {
    if (!id) {
      return undefined;
    }
    return spaces.find((item) => item.id === id);
  };

  const matched =
    lookup(spaceId) ?? lookup(activeSpaceId) ?? (spaces.length > 0 ? spaces[0] : undefined);
  if (!matched) {
    return undefined;
  }

  // 兜底默认值（风险 5）：cozelib 在很多分支会基于 space_type/role_type/space_mode 做严格相等判断，
  // 字段缺失会让上游 store 把当前空间标记为不可用。Atlas Workspace 与上游 BotSpace 字段不完全对齐时，
  // 这里统一兜底到 Team / Normal / Member。
  return {
    id: matched.id,
    name: matched.name && matched.name.length > 0 ? matched.name : matched.id,
    description: matched.description ?? "",
    icon_url: matched.iconUrl ?? "",
    space_type: typeof matched.spaceType === "number" && matched.spaceType > 0 ? matched.spaceType : 2,
    space_mode: typeof matched.spaceMode === "number" ? matched.spaceMode : 0,
    role_type: typeof matched.roleType === "number" && matched.roleType > 0 ? matched.roleType : 3,
  };
}

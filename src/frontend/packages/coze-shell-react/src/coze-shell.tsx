import { useState, type ReactNode } from "react";
import { Avatar, Button, Dropdown, Space, Typography, Tag, Badge } from "@douyinfe/semi-ui";
import { IconChevronLeft, IconGlobe, IconTreeTriangleDown, IconExit, IconPlus, IconMinus, IconUser, IconHome, IconSetting, IconMail, IconChevronRight, IconGridSquare, IconBell } from "@douyinfe/semi-icons";
import type {
  CozeHeaderAction,
  CozeNavItem,
  CozeNavSection,
} from "./types";
import "./styles.css";

interface CozeShellProps {
  appKey: string;
  backPath?: string;
  workspaceLabel: string;
  activePath: string;
  navSections: CozeNavSection[];
  headerTitle: string;
  headerSubtitle?: string;
  localeLabel: string;
  userName: string;
  extraActions?: CozeHeaderAction[];
  profileLabel?: string;
  logoutLabel?: string;
  sidebarTop?: ReactNode;
  onNavigate: (path: string) => void;
  onToggleLocale: () => void;
  onOpenProfile?: () => void;
  onLogout: () => void;
  children: ReactNode;
}

function isActiveNavItem(item: CozeNavItem, activePath: string): boolean {
  if (activePath === item.path || activePath.startsWith(`${item.path}/`)) {
    return true;
  }

  const [activePathname] = activePath.split("?");
  const [itemPathname] = item.path.split("?");
  return activePathname === itemPathname && activePath.includes(item.path);
}

export function CozeShell({
  appKey,
  backPath,
  workspaceLabel,
  activePath,
  navSections,
  headerTitle,
  headerSubtitle,
  localeLabel,
  userName,
  extraActions,
  profileLabel = "个人中心",
  logoutLabel = "退出登录",
  sidebarTop,
  onNavigate,
  onToggleLocale,
  onOpenProfile,
  onLogout,
  children,
}: CozeShellProps) {
  const [expandedSections, setExpandedSections] = useState<Record<string, boolean>>({});
  const [collapsed, setCollapsed] = useState(false);

  return (
    <div className={`coze-shell${collapsed ? " coze-shell--sidebar-collapsed" : ""}`}>
      <aside className="coze-shell__sidebar bg-white border-r border-[#f3f4f6]" data-testid="app-sidebar">
        <div className="coze-shell__sidebar-toolbar pb-[16px]">
          <div className="flex items-center gap-[8px]">
            <div className="relative rounded-[10px] shrink-0 size-[28px]" style={{ backgroundImage: "linear-gradient(135deg, rgb(43, 127, 255) 0%, rgb(20, 71, 230) 100%)" }}>
              <div className="bg-clip-padding border-0 border-[transparent] border-solid content-stretch flex items-center justify-center px-[7px] relative size-full">
                <span className="text-white text-[12px] font-bold">
                  {(workspaceLabel || appKey).slice(0, 1).toUpperCase()}
                </span>
              </div>
            </div>
            {!collapsed && (
              <span className="font-['Inter:Semi_Bold',sans-serif] font-semibold text-[#101828] text-[15px]">
                扣子
              </span>
            )}
          </div>
          
          <button
            type="button"
            className="coze-shell__collapse !bg-transparent !text-gray-400 hover:!text-gray-600"
            onClick={() => setCollapsed(current => !current)}
            data-testid="app-sidebar-toggle"
            title={collapsed ? "展开侧边栏" : "收起侧边栏"}
          >
            {collapsed ? <IconPlus size="small" /> : <IconMinus size="small" />}
          </button>
        </div>

        {!collapsed && sidebarTop && (
          <div className="px-[12px] pb-[16px]">
            {sidebarTop}
          </div>
        )}

        {!collapsed && extraActions && extraActions.length > 0 && (
          <div className="flex flex-col px-[12px] mb-[16px]">
            <button
              type="button"
              className="border-none rounded-[10px] py-[8px] flex items-center justify-center gap-[6px] cursor-pointer bg-[#f3f4f6] hover:bg-[#e5e6eb] transition-colors text-[#1f2329] font-medium w-full"
              onClick={extraActions[0].onClick}
              data-testid={extraActions[0].testId}
            >
              {extraActions[0].icon || <IconPlus size="small" />}
              <span className="text-[13px]">{extraActions[0].label}</span>
            </button>
          </div>
        )}

        {!collapsed && (
          <div className="border-t border-[#f3f4f6] mx-[12px] mb-[8px]" />
        )}

        <div className="coze-shell__sidebar-nav">
          {navSections.map((section, idx) => {
            const overflowItems = section.overflowItems ?? [];
            const activeOverflow = overflowItems.some(item => isActiveNavItem(item, activePath));
            const expanded = expandedSections[section.key] || activeOverflow;

            return (
              <section key={section.key} className={`coze-shell__section ${idx !== 0 ? 'border-t border-[#f3f4f6] mt-[8px] pt-[8px] mx-[4px]' : ''}`}>
                <div className="coze-shell__section-items">
                  {section.items.map(item => {
                    const active = isActiveNavItem(item, activePath);
                    return (
                      <button
                        key={item.key}
                        type="button"
                        className={`coze-shell__secondary-item !bg-transparent !border-transparent hover:!bg-gray-50 ${active ? "!bg-blue-50 !text-blue-600" : "!text-[#4a5565]"}`}
                        onClick={() => onNavigate(item.path)}
                        data-testid={item.testId}
                        title={item.label}
                      >
                        {item.icon ? <span className={`coze-shell__secondary-item-icon ${active ? "text-blue-600" : "text-[#4a5565]"}`}>{item.icon}</span> : null}
                        <span className="coze-shell__secondary-item-label font-medium">{item.label}</span>
                        {item.badge ? <Tag size="small" color="light-blue">{item.badge}</Tag> : null}
                      </button>
                    );
                  })}

                  {overflowItems.length > 0 ? (
                    <>
                      <button
                        type="button"
                        className={`coze-shell__secondary-item coze-shell__secondary-item--more${expanded ? " is-expanded" : ""} !bg-transparent !border-transparent hover:!bg-gray-50 !text-[#4a5565]`}
                        data-testid={section.overflowTestId}
                        title={section.overflowLabel ?? "更多"}
                        onClick={() => {
                          setExpandedSections(current => ({
                            ...current,
                            [section.key]: !expanded
                          }));
                        }}
                      >
                        <span className="coze-shell__secondary-item-icon text-[#4a5565]">
                          {expanded ? <IconMinus /> : <IconPlus />}
                        </span>
                        <span className="coze-shell__secondary-item-label font-medium">{section.overflowLabel ?? "更多"}</span>
                      </button>

                      {expanded ? (
                        <div className="coze-shell__section-overflow">
                          {overflowItems.map(item => {
                            const active = isActiveNavItem(item, activePath);
                            return (
                              <button
                                key={item.key}
                                type="button"
                                className={`coze-shell__secondary-item coze-shell__secondary-item--overflow !bg-transparent !border-transparent hover:!bg-gray-50 ${active ? "!bg-blue-50 !text-blue-600" : "!text-[#4a5565]"}`}
                                onClick={() => onNavigate(item.path)}
                                data-testid={item.testId}
                                title={item.label}
                              >
                                {item.icon ? <span className={`coze-shell__secondary-item-icon ${active ? "text-blue-600" : "text-[#4a5565]"}`}>{item.icon}</span> : null}
                                <span className="coze-shell__secondary-item-label font-medium">{item.label}</span>
                                {item.badge ? <Tag size="small" color="light-blue">{item.badge}</Tag> : null}
                              </button>
                            );
                          })}
                        </div>
                      ) : null}
                    </>
                  ) : null}
                </div>
              </section>
            );
          })}
        </div>

        {!collapsed && (
          <div className="coze-shell__sidebar-footer">
            <div className="border-t border-[#f3f4f6] mx-[12px] mt-[8px]" />
            <div className="coze-shell__sidebar-plan pt-[16px] flex flex-col gap-[12px] px-[12px]">
              <div className="flex items-center justify-between">
                <span className="text-[#6a7282] text-[11px]">总积分: 500</span>
                <span className="bg-blue-500 text-white text-[10px] px-[6px] py-[2px] rounded-[4px]">专业版</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-[#99a1af] text-[10px]">2200-01-01 到期</span>
              </div>
            </div>
            
            <Dropdown
              position="topLeft"
              trigger="click"
              render={
                <div className="w-[240px] bg-white rounded-[12px] shadow-[0_4px_24px_rgba(0,0,0,0.1)] py-[8px] flex flex-col" data-testid="coze-shell-user-menu">
                  <div className="flex items-center gap-[12px] px-[16px] py-[8px]">
                    <div className="relative">
                      <div className="bg-blue-500 rounded-full size-[36px] flex items-center justify-center text-white">
                        <IconUser />
                      </div>
                      <div className="absolute -bottom-1 -right-1 bg-white rounded-full p-[2px]">
                        <div className="bg-blue-500 text-white rounded-full size-[12px] flex items-center justify-center text-[8px]">✓</div>
                      </div>
                    </div>
                    <div className="flex flex-col min-w-0">
                      <span className="text-[#1f2329] text-[14px] font-medium truncate">{userName}</span>
                      <span className="text-[#86909c] text-[12px] truncate">@user8151658094</span>
                    </div>
                  </div>
                  <div className="h-[1px] bg-[#f3f4f6] mx-[16px] my-[4px]" />
                  <button type="button" className="flex items-center gap-[12px] px-[16px] py-[10px] mx-[8px] rounded-[8px] bg-transparent border-none cursor-pointer hover:bg-gray-50 text-[#1f2329] text-[14px]" onClick={onOpenProfile}>
                    <IconHome size="large" className="text-[#4a5565]" />
                    <span>个人主页</span>
                  </button>
                  <button type="button" className="flex items-center gap-[12px] px-[16px] py-[10px] mx-[8px] rounded-[8px] bg-transparent border-none cursor-pointer hover:bg-gray-50 text-[#1f2329] text-[14px]">
                    <IconSetting size="large" className="text-[#4a5565]" />
                    <span>账号设置</span>
                  </button>
                  <button type="button" className="flex items-center gap-[12px] px-[16px] py-[10px] mx-[8px] rounded-[8px] bg-transparent hover:bg-gray-50 border-none cursor-pointer text-[#1f2329] text-[14px]">
                    <IconMail size="large" className="text-[#4a5565]" />
                    <span className="flex-1 text-left">联系我们</span>
                    <IconChevronRight className="text-[#86909c]" />
                  </button>
                  <div className="h-[1px] bg-[#f3f4f6] mx-[16px] my-[4px]" />
                  <button type="button" className="flex items-center gap-[12px] px-[16px] py-[10px] mx-[8px] rounded-[8px] bg-transparent border-none cursor-pointer hover:bg-gray-50 text-[#1f2329] text-[14px]" onClick={onLogout}>
                    <IconExit size="large" className="text-[#4a5565]" />
                    <span>退出登录</span>
                  </button>
                </div>
              }
            >
              <div className="coze-shell__sidebar-user mx-[12px] my-[12px] flex items-center justify-between bg-gray-100 rounded-[10px] px-[10px] py-[8px] cursor-pointer hover:bg-gray-200 transition-colors border border-transparent">
                <div className="flex items-center gap-[8px] min-w-0">
                  <div className="bg-blue-500 rounded-full size-[24px] flex items-center justify-center text-white shrink-0">
                    <IconUser size="small" />
                  </div>
                  <span className="text-[#364153] text-[13px] font-medium truncate">{userName}</span>
                </div>
                <div className="flex items-center gap-[8px] shrink-0 text-gray-500">
                  <IconGridSquare size="small" />
                  <div className="relative">
                    <IconBell size="small" />
                    <div className="absolute -top-[6px] -right-[8px] bg-red-500 text-white text-[10px] leading-none px-[4px] py-[2px] rounded-full transform scale-75">38</div>
                  </div>
                </div>
              </div>
            </Dropdown>
          </div>
        )}
      </aside>

      <div className="coze-shell__content bg-[#f9fafb]">
        <header className="coze-shell__header !bg-transparent !border-none !pb-0">
          <div className="coze-shell__header-copy flex flex-row items-center gap-[12px]">
            <span className="text-[#1e2939] text-[15px] font-semibold">
              选择你要继续深入的工作空间
            </span>
          </div>

          <Space spacing={12}>
            <Button
              theme="borderless"
              icon={<IconGlobe />}
              onClick={onToggleLocale}
              data-testid="app-shell-toggle-locale"
              className="!text-blue-500 font-bold"
            >
              {localeLabel}
            </Button>
          </Space>
        </header>

        <main className="coze-shell__main !pt-[16px]">{children}</main>
      </div>
    </div>
  );
}

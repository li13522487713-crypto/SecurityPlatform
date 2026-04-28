import { useState } from "react";
import { Empty } from "@douyinfe/semi-ui";
import { IconEdit, IconShareStroked, IconSetting, IconUser, IconChevronDown } from "@douyinfe/semi-icons";
import { useNavigate } from "react-router-dom";
import { meSettingsPath } from "@atlas/app-shell-shared";
import { useAppI18n } from "../i18n";
import { useAuth } from "../auth-context";

export function MeProfilePage() {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const auth = useAuth();
  const profile = auth.profile;
  const [activeTab, setActiveTab] = useState("works");
  const [activeSubTab, setActiveSubTab] = useState("projects");

  return (
    <div className="min-h-[calc(100vh-60px)] w-full bg-gradient-to-b from-[#f0f4ff] to-white flex flex-col" data-testid="coze-me-profile-page">
      <div className="max-w-[1200px] w-full mx-auto px-[40px] pt-[60px] flex flex-col">
        
        {/* Header Section */}
        <section className="flex items-start justify-between w-full mb-[40px]">
          <div className="flex items-center gap-[24px]">
            <div className="size-[100px] rounded-full bg-blue-500 flex items-center justify-center text-white shrink-0">
              <IconUser style={{ fontSize: 50 }} />
            </div>
            <div className="flex flex-col gap-[12px]">
              <div className="flex items-baseline gap-[12px]">
                <h1 className="text-[24px] font-bold text-[#1f2329] m-0 leading-none">
                  {profile?.displayName || profile?.username || "RootUser"}
                </h1>
                <span className="text-[#86909c] text-[14px]">
                  @{profile?.username || "user8151658094"}
                </span>
              </div>
              
              <div className="flex items-center gap-[6px] text-[#4a5565] text-[14px] cursor-pointer hover:opacity-80 transition-opacity">
                <span>{t("cozeMeProfileSignaturePlaceholder")}</span>
                <IconEdit size="small" />
              </div>
              
              <div className="flex items-center gap-[24px] mt-[4px]">
                <div className="flex items-center gap-[6px] text-[14px]">
                  <span className="text-[#86909c]">关注</span>
                  <span className="text-[#1f2329] font-medium">0</span>
                </div>
                <div className="flex items-center gap-[6px] text-[14px]">
                  <span className="text-[#86909c]">粉丝</span>
                  <span className="text-[#1f2329] font-medium">0</span>
                </div>
                <div className="flex items-center gap-[6px] text-[14px]">
                  <span className="text-[#86909c]">获赞</span>
                  <span className="text-[#1f2329] font-medium">0</span>
                </div>
              </div>
            </div>
          </div>
          
          <div className="flex items-center gap-[12px]">
            <button type="button" className="size-[36px] rounded-[10px] bg-white border border-[#e5e6eb] flex items-center justify-center cursor-pointer hover:bg-gray-50 transition-colors shadow-sm">
              <IconShareStroked className="text-[#4a5565]" />
            </button>
            <button type="button" className="size-[36px] rounded-[10px] bg-white border border-[#e5e6eb] flex items-center justify-center cursor-pointer hover:bg-gray-50 transition-colors shadow-sm" onClick={() => navigate(meSettingsPath("account"))}>
              <IconSetting className="text-[#4a5565]" />
            </button>
          </div>
        </section>

        {/* Tabs Section */}
        <section className="w-full">
          <div className="flex items-center gap-[32px] border-b border-[#e5e6eb] px-[8px]">
            {[
              { key: "works", label: "作品 (0)" },
              { key: "likes", label: "喜欢" },
              { key: "favorites", label: "收藏" },
              { key: "chat_history", label: "对话历史" },
              { key: "visit_history", label: "访问历史" }
            ].map(tab => (
              <div 
                key={tab.key}
                className={`pb-[12px] cursor-pointer text-[15px] font-medium relative ${activeTab === tab.key ? "text-[#1677ff]" : "text-[#4a5565] hover:text-[#1f2329]"}`}
                onClick={() => setActiveTab(tab.key)}
              >
                {tab.label}
                {activeTab === tab.key && (
                  <div className="absolute bottom-0 left-0 w-full h-[3px] bg-[#1677ff] rounded-t-[3px]" />
                )}
              </div>
            ))}
          </div>
        </section>

        {/* Content Section */}
        <section className="w-full flex-1 flex flex-col pt-[24px] pb-[60px]">
          {activeTab === "works" && (
            <div className="flex flex-col h-full w-full">
              <div className="flex items-center justify-between w-full mb-[80px]">
                <div className="flex items-center gap-[8px]">
                  {[
                    { key: "projects", label: "项目" },
                    { key: "plugins", label: "插件" },
                    { key: "templates", label: "模板" }
                  ].map(subTab => (
                    <div 
                      key={subTab.key}
                      className={`px-[16px] py-[6px] rounded-[8px] cursor-pointer text-[13px] transition-colors ${activeSubTab === subTab.key ? "bg-[#f2f3f5] text-[#1f2329] font-medium" : "text-[#4a5565] hover:bg-gray-50"}`}
                      onClick={() => setActiveSubTab(subTab.key)}
                    >
                      {subTab.label}
                    </div>
                  ))}
                </div>
                
                <button type="button" className="flex items-center gap-[6px] px-[12px] py-[6px] bg-white border border-[#e5e6eb] rounded-[8px] text-[#4a5565] text-[13px] cursor-pointer hover:bg-gray-50 transition-colors shadow-sm">
                  <span>日期筛选</span>
                  <IconChevronDown size="small" />
                </button>
              </div>
              
              <div className="flex-1 flex flex-col items-center justify-center">
                <Empty
                  image={<img src="https://lf3-static.bytednsdoc.com/obj/eden-cn/ptlz_zlp/ljhwZthlaukjlkulzlp/empty-state.png" alt="暂无内容" style={{ width: 160, opacity: 0.6 }} />}
                  title="暂无内容"
                  style={{ marginTop: 0 }}
                />
              </div>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}

import { useEffect, useMemo, useState } from "react";
import { Avatar, Button, Empty, Form, Spin, TabPane, Tabs, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { IconClose, IconEdit, IconUser } from "@douyinfe/semi-icons";
import { useNavigate, useParams } from "react-router-dom";
import { meSettingsPath, type MeSettingsTab } from "@atlas/app-shell-shared";
import { useAppI18n } from "../i18n";
import { useAuth } from "../auth-context";
import {
  getMeGeneralSettings,
  listMeDataSources,
  listMePublishChannels,
  updateMeGeneralSettings,
  type MeDataSourceItem,
  type MeGeneralSettings,
  type MePublishChannelItem
} from "../../services/mock";

const TAB_KEYS: MeSettingsTab[] = ["account", "general", "channels", "datasource"];

export function MeSettingsPage() {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const auth = useAuth();
  const params = useParams<{ tab?: string }>();
  const activeTab = useMemo<MeSettingsTab>(() => {
    const value = params.tab as MeSettingsTab | undefined;
    return value && TAB_KEYS.includes(value) ? value : "account";
  }, [params.tab]);

  return (
    <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center backdrop-blur-sm" data-testid="coze-me-settings-page">
      <div className="bg-white rounded-[16px] w-[860px] h-[600px] shadow-2xl flex flex-col overflow-hidden relative">
        <button 
          className="absolute top-[20px] right-[24px] text-gray-500 hover:bg-gray-100 p-2 rounded-md z-10 cursor-pointer border-none bg-transparent" 
          onClick={() => navigate(-1)}
        >
          <IconClose size="large" />
        </button>
        
        <div className="flex h-full">
          {/* Sidebar */}
          <div className="w-[200px] bg-[#f7f8fa] flex flex-col p-[24px] border-r border-gray-100">
            <h2 className="text-[18px] font-bold mb-[24px] text-[#1f2329]">设置</h2>
            
            <div className="flex flex-col gap-[6px]">
              {[
                { key: "account", label: "账号" },
                { key: "general", label: "通用设置" },
                { key: "channels", label: "发布渠道" },
                { key: "datasource", label: "数据源" }
              ].map(tab => (
                <div
                  key={tab.key}
                  className={`px-[16px] py-[10px] rounded-[8px] cursor-pointer text-[14px] transition-colors ${activeTab === tab.key ? "bg-white font-medium shadow-sm text-[#1f2329]" : "text-[#4a5565] hover:bg-gray-200"}`}
                  onClick={() => navigate(meSettingsPath(tab.key as MeSettingsTab))}
                >
                  {tab.label}
                </div>
              ))}
            </div>
            
            <div className="mt-auto">
              <div className="px-[8px] py-[4px] bg-[#eef3ff] text-blue-600 text-[11px] rounded-[4px] inline-block font-bold">专业版</div>
            </div>
          </div>
          
          {/* Main Content */}
          <div className="flex-1 p-[32px] overflow-y-auto relative flex flex-col">
            <h3 className="text-[20px] font-bold mb-[32px] text-[#1f2329]">
              {activeTab === 'account' ? '账号' : activeTab === 'general' ? '通用设置' : activeTab === 'channels' ? '发布渠道' : '数据源'}
            </h3>
            
            {activeTab === "account" && <AccountPanel />}
            {activeTab === "general" && <GeneralPanel />}
            {activeTab === "channels" && <ChannelsPanel />}
            {activeTab === "datasource" && <DataSourcePanel />}
            
            {activeTab === "account" && (
              <div className="mt-auto pt-6 border-t border-gray-100 text-gray-300 text-xs">
                UID: {auth.profile?.id || "895451392122240"}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

function AccountPanel() {
  const { t } = useAppI18n();
  const auth = useAuth();
  const profile = auth.profile;
  
  return (
    <div className="flex flex-col w-full max-w-[500px]">
      <div className="mb-[32px]">
        <div className="size-[80px] rounded-full bg-blue-500 flex items-center justify-center text-white shrink-0">
          <IconUser style={{ fontSize: 40 }} />
        </div>
      </div>
      
      <div className="flex flex-col gap-[24px]">
        <div className="flex items-center">
          <div className="w-[100px] text-[#4a5565] text-[14px]">用户名</div>
          <div className="flex items-center gap-[8px] text-[#1f2329] text-[14px]">
            <span>{profile?.username || "user8151658094"}</span>
            <IconEdit size="small" className="text-gray-400 cursor-pointer hover:text-blue-500" />
          </div>
        </div>
        
        <div className="flex items-center">
          <div className="w-[100px] text-[#4a5565] text-[14px]">用户昵称</div>
          <div className="flex items-center gap-[8px] text-[#1f2329] text-[14px]">
            <span>{profile?.displayName || "RootUser_2103671511"}</span>
            <IconEdit size="small" className="text-gray-400 cursor-pointer hover:text-blue-500" />
          </div>
        </div>
        
        <div className="flex items-center">
          <div className="w-[100px] text-[#4a5565] text-[14px]">火山成员名</div>
          <div className="text-[#1f2329] text-[14px]">
            <span>{profile?.displayName || "RootUser_2103671511"}</span>
          </div>
        </div>
        
        <div className="flex items-center">
          <div className="w-[100px] text-[#4a5565] text-[14px]">个性签名</div>
          <div className="flex items-center gap-[8px] text-[#1f2329] text-[14px]">
            <span>这个用户很懒，什么都没有留下</span>
            <IconEdit size="small" className="text-gray-400 cursor-pointer hover:text-blue-500" />
          </div>
        </div>
        
        <div className="flex items-center">
          <div className="w-[100px] text-[#4a5565] text-[14px]">手机号</div>
          <div className="text-blue-500 hover:text-blue-600 cursor-pointer text-[14px] font-medium">
            查看与编辑
          </div>
        </div>
      </div>
      
      <div className="mt-[48px]">
        <button type="button" className="px-[16px] py-[8px] bg-red-50 text-red-500 hover:bg-red-100 rounded-[8px] border-none cursor-pointer text-[14px] font-medium transition-colors">
          删除账号
        </button>
      </div>
    </div>
  );
}

function GeneralPanel() {
  const { t } = useAppI18n();
  const [values, setValues] = useState<MeGeneralSettings | null>(null);

  useEffect(() => {
    void getMeGeneralSettings().then(setValues);
  }, []);

  if (!values) {
    return <div className="coze-page__loading"><Spin /></div>;
  }

  return (
    <Form
      labelPosition="top"
      labelWidth="100%"
      initValues={values}
      onValueChange={next => setValues({ ...values, ...next })}
    >
      <Form.Select
        field="locale"
        label={t("cozeMeSettingsGeneralLocale")}
        optionList={[
          { label: "中文", value: "zh-CN" },
          { label: "English", value: "en-US" }
        ]}
      />
      <Form.Select
        field="theme"
        label={t("cozeMeSettingsGeneralTheme")}
        optionList={[
          { label: "Light", value: "light" },
          { label: "Dark", value: "dark" },
          { label: "System", value: "system" }
        ]}
      />
      <Form.Input field="defaultWorkspaceId" label={t("cozeMeSettingsGeneralDefaultWorkspace")} />
      <Button
        theme="solid"
        type="primary"
        onClick={() => {
          void updateMeGeneralSettings(values).then(() => Toast.success(t("cozeCreateSuccess")));
        }}
      >
        {t("homeEnter")}
      </Button>
    </Form>
  );
}

function ChannelsPanel() {
  const { t } = useAppI18n();
  const [items, setItems] = useState<MePublishChannelItem[]>([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    listMePublishChannels()
      .then(list => {
        if (!cancelled) {
          setItems(list);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);
  if (loading) {
    return <div className="coze-page__loading"><Spin /></div>;
  }
  if (items.length === 0) {
    return <Empty description={t("cozeMeSettingsChannelsEmpty")} />;
  }
  return (
    <ul className="coze-list">
      {items.map(item => (
        <li key={item.id} className="coze-list__item">
          <strong>{item.name}</strong>
          <Tag color={item.bound ? "green" : "grey"}>
            {item.bound ? t("cozeSettingsChannelStatusActive") : t("cozeSettingsChannelStatusPending")}
          </Tag>
        </li>
      ))}
    </ul>
  );
}

function DataSourcePanel() {
  const { t } = useAppI18n();
  const [items, setItems] = useState<MeDataSourceItem[]>([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    listMeDataSources()
      .then(list => {
        if (!cancelled) {
          setItems(list);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);
  if (loading) {
    return <div className="coze-page__loading"><Spin /></div>;
  }
  if (items.length === 0) {
    return <Empty description={t("cozeMeSettingsDataSourceEmpty")} />;
  }
  return (
    <ul className="coze-list">
      {items.map(item => (
        <li key={item.id} className="coze-list__item">
          <strong>{item.name}</strong>
          <Tag color={item.bound ? "green" : "grey"}>{item.type}</Tag>
        </li>
      ))}
    </ul>
  );
}

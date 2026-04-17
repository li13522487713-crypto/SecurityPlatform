import { useEffect, useMemo, useState } from "react";
import { Avatar, Button, Empty, Form, Spin, TabPane, Tabs, Tag, Toast, Typography } from "@douyinfe/semi-ui";
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
  const params = useParams<{ tab?: string }>();
  const activeTab = useMemo<MeSettingsTab>(() => {
    const value = params.tab as MeSettingsTab | undefined;
    return value && TAB_KEYS.includes(value) ? value : "account";
  }, [params.tab]);

  return (
    <div className="coze-page coze-me-settings-page" data-testid="coze-me-settings-page">
      <header className="coze-page__header">
        <Typography.Title heading={3} style={{ margin: 0 }}>{t("cozeMeSettingsTitle")}</Typography.Title>
      </header>

      <Tabs
        type="line"
        activeKey={activeTab}
        onChange={key => navigate(meSettingsPath(key as MeSettingsTab))}
      >
        <TabPane tab={t("cozeMeSettingsTabAccount")} itemKey="account">
          {activeTab === "account" ? <AccountPanel /> : null}
        </TabPane>
        <TabPane tab={t("cozeMeSettingsTabGeneral")} itemKey="general">
          {activeTab === "general" ? <GeneralPanel /> : null}
        </TabPane>
        <TabPane tab={t("cozeMeSettingsTabChannels")} itemKey="channels">
          {activeTab === "channels" ? <ChannelsPanel /> : null}
        </TabPane>
        <TabPane tab={t("cozeMeSettingsTabDataSource")} itemKey="datasource">
          {activeTab === "datasource" ? <DataSourcePanel /> : null}
        </TabPane>
      </Tabs>
    </div>
  );
}

function AccountPanel() {
  const { t } = useAppI18n();
  const auth = useAuth();
  const profile = auth.profile;
  return (
    <div className="coze-account-panel">
      <Avatar size="large" color="light-blue">
        {(profile?.displayName || profile?.username || "A").slice(0, 1).toUpperCase()}
      </Avatar>
      <ul className="coze-account-panel__list">
        <li>
          <Typography.Text type="tertiary">{t("cozeMeSettingsAccountUsernameLabel")}</Typography.Text>
          <strong>{profile?.username ?? "-"}</strong>
        </li>
        <li>
          <Typography.Text type="tertiary">{t("cozeMeSettingsAccountNicknameLabel")}</Typography.Text>
          <strong>{profile?.displayName ?? "-"}</strong>
        </li>
        <li>
          <Typography.Text type="tertiary">{t("cozeMeProfileUidLabel")}</Typography.Text>
          <strong>{profile?.id ?? "-"}</strong>
        </li>
      </ul>
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

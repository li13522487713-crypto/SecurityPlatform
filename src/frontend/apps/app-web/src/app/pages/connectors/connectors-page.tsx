import { useNavigate } from "react-router";
import {
  ConnectorProvidersPage,
  type ConnectorProvidersPageLabels,
  type ConnectorProviderEditDrawerLabels,
  type ConnectorOAuthConfigFormLabels,
} from "@atlas/external-connectors-react";
import { connectorApi } from "../../../services/api-connectors";
import { useAppI18n } from "../../i18n";

export default function ConnectorsPage() {
  const { t } = useAppI18n();
  const navigate = useNavigate();

  const providersLabels: ConnectorProvidersPageLabels = {
    title: t("connector_providersPage_title"),
    enable: t("connector_providersPage_enable"),
    disable: t("connector_providersPage_disable"),
    delete: t("connector_providersPage_delete"),
    edit: t("connector_providersPage_edit"),
    add: t("connector_providersPage_add"),
    refresh: t("connector_providersPage_refresh"),
    empty: t("connector_providersPage_empty"),
    statusOn: t("connector_providersPage_statusOn"),
    statusOff: t("connector_providersPage_statusOff"),
    columnProvider: t("connector_providersPage_columnProvider"),
    columnCode: t("connector_providersPage_columnCode"),
    columnName: t("connector_providersPage_columnName"),
    columnEnabled: t("connector_providersPage_columnEnabled"),
    columnUpdatedAt: t("connector_providersPage_columnUpdatedAt"),
    columnActions: t("connector_providersPage_columnActions"),
    confirmDelete: t("connector_providersPage_confirmDelete"),
    confirmDeleteTitle: t("connector_providersPage_confirmDeleteTitle"),
    loadingText: t("connector_providersPage_loadingText"),
  };

  const drawerLabels: ConnectorProviderEditDrawerLabels = {
    titleCreate: t("connector_providerDrawer_titleCreate"),
    titleEdit: t("connector_providerDrawer_titleEdit"),
    providerType: t("connector_providerDrawer_providerType"),
    code: t("connector_providerDrawer_code"),
    displayName: t("connector_providerDrawer_displayName"),
    providerTenantId: t("connector_providerDrawer_providerTenantId"),
    appId: t("connector_providerDrawer_appId"),
    secretJson: t("connector_providerDrawer_secretJson"),
    secretJsonHelp: t("connector_providerDrawer_secretJsonHelp"),
    rotateSecret: t("connector_providerDrawer_rotateSecret"),
    rotateConfirm: t("connector_providerDrawer_rotateConfirm"),
    rotateConfirmTitle: t("connector_providerDrawer_rotateConfirmTitle"),
    rotateBeforeFillError: t("connector_providerDrawer_rotateBeforeFillError"),
    submitCreate: t("connector_providerDrawer_submitCreate"),
    submitUpdate: t("connector_providerDrawer_submitUpdate"),
    cancel: t("connector_providerDrawer_cancel"),
    closeDrawer: t("connector_providerDrawer_closeDrawer"),
    codePlaceholder: t("connector_providerDrawer_codePlaceholder"),
    displayNamePlaceholder: t("connector_providerDrawer_displayNamePlaceholder"),
    providerTenantIdPlaceholder: t("connector_providerDrawer_providerTenantIdPlaceholder"),
    appIdPlaceholder: t("connector_providerDrawer_appIdPlaceholder"),
    providerTypeWeCom: t("connector_providerDrawer_providerTypeWeCom"),
    providerTypeFeishu: t("connector_providerDrawer_providerTypeFeishu"),
    providerTypeDingTalk: t("connector_providerDrawer_providerTypeDingTalk"),
    providerTypeCustomOidc: t("connector_providerDrawer_providerTypeCustomOidc"),
  };

  const oauthFormLabels: ConnectorOAuthConfigFormLabels = {
    callbackBaseUrl: t("connector_oauthForm_callbackBaseUrl"),
    trustedDomains: t("connector_oauthForm_trustedDomains"),
    visibilityScope: t("connector_oauthForm_visibilityScope"),
    syncCron: t("connector_oauthForm_syncCron"),
    agentId: t("connector_oauthForm_agentId"),
    callbackHelp: t("connector_oauthForm_callbackHelp"),
    trustedDomainsHelp: t("connector_oauthForm_trustedDomainsHelp"),
    syncCronHelp: t("connector_oauthForm_syncCronHelp"),
    trustedDomainsPlaceholder: t("connector_oauthForm_trustedDomainsPlaceholder"),
    visibilityScopePlaceholder: t("connector_oauthForm_visibilityScopePlaceholder"),
    syncCronPlaceholder: t("connector_oauthForm_syncCronPlaceholder"),
    agentIdPlaceholder: t("connector_oauthForm_agentIdPlaceholder"),
  };

  return (
    <div style={{ padding: 24 }}>
      <ConnectorProvidersPage
        api={connectorApi}
        labels={providersLabels}
        drawerLabels={drawerLabels}
        oauthFormLabels={oauthFormLabels}
        onRowClick={(item) => navigate(`./${item.id}`)}
      />
    </div>
  );
}

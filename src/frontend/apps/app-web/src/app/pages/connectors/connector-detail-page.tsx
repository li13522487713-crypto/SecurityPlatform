import { useParams } from "react-router";
import {
  ConnectorApprovalMappingPage,
  ConnectorBindingsPage,
  ConnectorDirectorySyncPage,
  type ConnectorApprovalMappingPageLabels,
  type ConnectorBindingsPageLabels,
  type ConnectorDirectorySyncPageLabels,
  type ConnectorTemplateMappingDesignerLabels,
  type IdentityBindingConflictCenterLabels,
} from "@atlas/external-connectors-react";
import { connectorApi } from "../../../services/api-connectors";
import { useAppI18n } from "../../i18n";

export default function ConnectorDetailPage() {
  const { t } = useAppI18n();
  const params = useParams<{ providerId: string }>();
  const providerId = Number(params.providerId);
  if (!providerId || Number.isNaN(providerId)) {
    return <p style={{ padding: 24 }}>{t("connectorMissingProviderId")}</p>;
  }

  const bindingsLabels: ConnectorBindingsPageLabels = {
    title: t("connector_bindingsPage_title"),
    statusFilter: t("connector_bindingsPage_statusFilter"),
    all: t("connector_bindingsPage_all"),
    columnLocalUser: t("connector_bindingsPage_columnLocalUser"),
    columnExternalUser: t("connector_bindingsPage_columnExternalUser"),
    columnStatus: t("connector_bindingsPage_columnStatus"),
    columnStrategy: t("connector_bindingsPage_columnStrategy"),
    columnLastLogin: t("connector_bindingsPage_columnLastLogin"),
    totalSuffix: t("connector_bindingsPage_totalSuffix"),
    conflictsHint: t("connector_bindingsPage_conflictsHint"),
    loadingText: t("connector_bindingsPage_loadingText"),
    dashPlaceholder: t("connector_bindingsPage_dashPlaceholder"),
  };

  const conflictCenterLabels: IdentityBindingConflictCenterLabels = {
    title: t("connector_conflictCenter_title"),
    manualBindHeader: t("connector_conflictCenter_manualBindHeader"),
    localUserId: t("connector_conflictCenter_localUserId"),
    externalUserId: t("connector_conflictCenter_externalUserId"),
    mobile: t("connector_conflictCenter_mobile"),
    email: t("connector_conflictCenter_email"),
    submitManualBind: t("connector_conflictCenter_submitManualBind"),
    noConflicts: t("connector_conflictCenter_noConflicts"),
    columnId: t("connector_conflictCenter_columnId"),
    columnLocalUser: t("connector_conflictCenter_columnLocalUser"),
    columnExternalUser: t("connector_conflictCenter_columnExternalUser"),
    columnStatus: t("connector_conflictCenter_columnStatus"),
    columnAction: t("connector_conflictCenter_columnAction"),
    resolutionLabel: t("connector_conflictCenter_resolutionLabel"),
    resolutionKeepCurrent: t("connector_conflictCenter_resolutionKeepCurrent"),
    resolutionSwitchToLocalUser: t("connector_conflictCenter_resolutionSwitchToLocalUser"),
    resolutionRevoke: t("connector_conflictCenter_resolutionRevoke"),
    newLocalUserIdLabel: t("connector_conflictCenter_newLocalUserIdLabel"),
    apply: t("connector_conflictCenter_apply"),
    revoke: t("connector_conflictCenter_revoke"),
    revokeConfirm: t("connector_conflictCenter_revokeConfirm"),
    revokeConfirmTitle: t("connector_conflictCenter_revokeConfirmTitle"),
    requiredFieldsMissing: t("connector_conflictCenter_requiredFieldsMissing"),
  };

  const directorySyncLabels: ConnectorDirectorySyncPageLabels = {
    title: t("connector_directorySync_title"),
    fullSync: t("connector_directorySync_fullSync"),
    incrementalSync: t("connector_directorySync_incrementalSync"),
    syncing: t("connector_directorySync_syncing"),
    jobsHeader: t("connector_directorySync_jobsHeader"),
    diffsHeader: t("connector_directorySync_diffsHeader"),
    retry: t("connector_directorySync_retry"),
    expandDiffs: t("connector_directorySync_expandDiffs"),
    collapseDiffs: t("connector_directorySync_collapseDiffs"),
    noDiffs: t("connector_directorySync_noDiffs"),
    incrementalKind: t("connector_directorySync_incrementalKind"),
    incrementalEntityId: t("connector_directorySync_incrementalEntityId"),
    incrementalSubmit: t("connector_directorySync_incrementalSubmit"),
    incrementalEntityIdRequired: t("connector_directorySync_incrementalEntityIdRequired"),
    incrementalEntityIdPlaceholder: t("connector_directorySync_incrementalEntityIdPlaceholder"),
    jobsColumnJobId: t("connector_directorySync_jobsColumnJobId"),
    jobsColumnMode: t("connector_directorySync_jobsColumnMode"),
    jobsColumnStatus: t("connector_directorySync_jobsColumnStatus"),
    jobsColumnTriggerSource: t("connector_directorySync_jobsColumnTriggerSource"),
    jobsColumnUserStats: t("connector_directorySync_jobsColumnUserStats"),
    jobsColumnDepartmentStats: t("connector_directorySync_jobsColumnDepartmentStats"),
    jobsColumnStartedAt: t("connector_directorySync_jobsColumnStartedAt"),
    jobsColumnFinishedAt: t("connector_directorySync_jobsColumnFinishedAt"),
    jobsColumnAction: t("connector_directorySync_jobsColumnAction"),
    diffsColumnDiffId: t("connector_directorySync_diffsColumnDiffId"),
    diffsColumnType: t("connector_directorySync_diffsColumnType"),
    diffsColumnEntityId: t("connector_directorySync_diffsColumnEntityId"),
    diffsColumnSummary: t("connector_directorySync_diffsColumnSummary"),
    diffsColumnOccurredAt: t("connector_directorySync_diffsColumnOccurredAt"),
    loadingText: t("connector_directorySync_loadingText"),
    loadingDiffsText: t("connector_directorySync_loadingDiffsText"),
    dashPlaceholder: t("connector_directorySync_dashPlaceholder"),
  };

  const approvalMappingLabels: ConnectorApprovalMappingPageLabels = {
    title: t("connector_approvalMapping_title"),
    templatesHeader: t("connector_approvalMapping_templatesHeader"),
    mappingsHeader: t("connector_approvalMapping_mappingsHeader"),
    designerHeader: t("connector_approvalMapping_designerHeader"),
    designerEmpty: t("connector_approvalMapping_designerEmpty"),
    refresh: t("connector_approvalMapping_refresh"),
    startMapping: t("connector_approvalMapping_startMapping"),
    columnTemplateId: t("connector_approvalMapping_columnTemplateId"),
    columnTemplateName: t("connector_approvalMapping_columnTemplateName"),
    columnControls: t("connector_approvalMapping_columnControls"),
    columnFetchedAt: t("connector_approvalMapping_columnFetchedAt"),
    columnTemplateActions: t("connector_approvalMapping_columnTemplateActions"),
    columnFlowId: t("connector_approvalMapping_columnFlowId"),
    columnExternalTpl: t("connector_approvalMapping_columnExternalTpl"),
    columnIntegrationMode: t("connector_approvalMapping_columnIntegrationMode"),
    columnEnabled: t("connector_approvalMapping_columnEnabled"),
    columnUpdatedAt: t("connector_approvalMapping_columnUpdatedAt"),
    enabled: t("connector_approvalMapping_enabled"),
    disabled: t("connector_approvalMapping_disabled"),
    integrationModeExternalLed: t("connector_approvalMapping_integrationModeExternalLed"),
    integrationModeLocalLed: t("connector_approvalMapping_integrationModeLocalLed"),
    integrationModeHybrid: t("connector_approvalMapping_integrationModeHybrid"),
    loadingText: t("connector_approvalMapping_loadingText"),
  };

  const designerLabels: ConnectorTemplateMappingDesignerLabels = {
    title: t("connector_mappingDesigner_title"),
    templateLabelPrefix: t("connector_mappingDesigner_templateLabelPrefix"),
    localField: t("connector_mappingDesigner_localField"),
    externalControl: t("connector_mappingDesigner_externalControl"),
    valueType: t("connector_mappingDesigner_valueType"),
    integrationMode: t("connector_mappingDesigner_integrationMode"),
    enabled: t("connector_mappingDesigner_enabled"),
    enumMapping: t("connector_mappingDesigner_enumMapping"),
    enumMappingPlaceholder: t("connector_mappingDesigner_enumMappingPlaceholder"),
    addRow: t("connector_mappingDesigner_addRow"),
    removeRow: t("connector_mappingDesigner_removeRow"),
    save: t("connector_mappingDesigner_save"),
    delete: t("connector_mappingDesigner_delete"),
    noTemplate: t("connector_mappingDesigner_noTemplate"),
    unmappedRequired: t("connector_mappingDesigner_unmappedRequired"),
    selectLocalFieldPlaceholder: t("connector_mappingDesigner_selectLocalFieldPlaceholder"),
    selectExternalControlPlaceholder: t("connector_mappingDesigner_selectExternalControlPlaceholder"),
    integrationModeExternalLed: t("connector_mappingDesigner_integrationModeExternalLed"),
    integrationModeLocalLed: t("connector_mappingDesigner_integrationModeLocalLed"),
    integrationModeHybrid: t("connector_mappingDesigner_integrationModeHybrid"),
  };

  return (
    <div style={{ padding: 24, display: "grid", gap: 24 }}>
      <ConnectorBindingsPage
        api={connectorApi}
        providerId={providerId}
        labels={bindingsLabels}
        conflictCenterLabels={conflictCenterLabels}
      />
      <ConnectorDirectorySyncPage
        api={connectorApi}
        providerId={providerId}
        labels={directorySyncLabels}
      />
      <ConnectorApprovalMappingPage
        api={connectorApi}
        providerId={providerId}
        labels={approvalMappingLabels}
        designerLabels={designerLabels}
      />
    </div>
  );
}

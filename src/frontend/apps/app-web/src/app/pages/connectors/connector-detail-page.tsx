import { useParams } from "react-router";
import {
  ConnectorApprovalMappingPage,
  ConnectorBindingsPage,
  ConnectorDirectorySyncPage,
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
  return (
    <div style={{ padding: 24, display: "grid", gap: 24 }}>
      <ConnectorBindingsPage
        api={connectorApi}
        providerId={providerId}
        labels={{ title: t("connectorBindingsTitle") }}
      />
      <ConnectorDirectorySyncPage
        api={connectorApi}
        providerId={providerId}
        labels={{
          title: t("connectorSyncTitle"),
          fullSync: t("connectorSyncFullTrigger"),
          incrementalSync: t("connectorSyncIncrementalTitle"),
          jobsHeader: t("connectorSyncJobsHeader"),
          diffsHeader: t("connectorSyncDiffsHeader"),
          retry: t("connectorSyncRetry"),
          expandDiffs: t("connectorSyncExpandDiffs"),
          collapseDiffs: t("connectorSyncCollapseDiffs"),
          noDiffs: t("connectorSyncNoDiffs"),
          incrementalKind: t("connectorSyncIncrementalKind"),
          incrementalEntityId: t("connectorSyncIncrementalEntityId"),
          incrementalSubmit: t("connectorSyncIncrementalSubmit"),
        }}
      />
      <ConnectorApprovalMappingPage
        api={connectorApi}
        providerId={providerId}
        labels={{
          title: t("connectorMappingTitle"),
          designerEmpty: t("connectorMappingDesignerEmpty"),
          refresh: t("connectorRefresh"),
        }}
      />
    </div>
  );
}

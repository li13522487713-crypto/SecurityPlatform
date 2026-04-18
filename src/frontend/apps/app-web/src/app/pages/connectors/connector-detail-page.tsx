import { useParams } from "react-router";
import {
  ConnectorApprovalMappingPage,
  ConnectorBindingsPage,
  ConnectorDirectorySyncPage,
} from "@atlas/external-connectors-react";
import { connectorApi } from "../../../services/api-connectors";

export default function ConnectorDetailPage() {
  const params = useParams<{ providerId: string }>();
  const providerId = Number(params.providerId);
  if (!providerId || Number.isNaN(providerId)) {
    return <p style={{ padding: 24 }}>缺少 providerId 参数</p>;
  }
  return (
    <div style={{ padding: 24, display: "grid", gap: 24 }}>
      <ConnectorBindingsPage api={connectorApi} providerId={providerId} />
      <ConnectorDirectorySyncPage api={connectorApi} providerId={providerId} />
      <ConnectorApprovalMappingPage api={connectorApi} providerId={providerId} />
    </div>
  );
}

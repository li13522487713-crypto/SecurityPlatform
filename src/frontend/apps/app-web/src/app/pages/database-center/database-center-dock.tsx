import { useEffect, useState } from "react";
import { Button, Space, Spin, Tabs, Toast, Typography } from "@douyinfe/semi-ui";
import type {
  DatabaseCenterObjectSummary,
  DatabaseCenterSchemaStructure,
  DatabaseCenterSourceDetail,
  DatabaseCenterSourceSummary
} from "../../../services/api-database-center";
import { getTableDdl, getViewDdl } from "../../../services/api-database-structure";
import { SqlCodeEditor } from "../database-structure/sql-code-editor";
import type { DatabaseCenterLabels } from "./database-center-labels";

const { Text } = Typography;

interface DatabaseCenterDockProps {
  labels: DatabaseCenterLabels;
  source: DatabaseCenterSourceSummary | DatabaseCenterSourceDetail | null;
  selectedObject: DatabaseCenterObjectSummary | null;
  structure: DatabaseCenterSchemaStructure | null;
}

export function DatabaseCenterDock({ labels, source, selectedObject, structure }: DatabaseCenterDockProps) {
  const [ddl, setDdl] = useState("");
  const [loadingDdl, setLoadingDdl] = useState(false);

  useEffect(() => {
    setDdl("");
    if (source?.id && selectedObject && (selectedObject.objectType === "table" || selectedObject.objectType === "view")) {
      void loadDdl();
    }
  }, [selectedObject?.id, selectedObject?.name, selectedObject?.objectType, selectedObject?.schema, source?.id]);

  return (
    <div className="database-center-dock">
      <Tabs size="small" tabBarStyle={{ paddingLeft: 12 }}>
        <Tabs.TabPane tab={labels.details} itemKey="details">
          <div style={{ padding: 12 }}>
            <Text type="tertiary">
              {source ? `${source.name} / ${source.driverCode} / ${source.provisionState ?? "Pending"} / objects: ${structure?.objects.length ?? 0}` : labels.noSource}
            </Text>
          </div>
        </Tabs.TabPane>
        <Tabs.TabPane tab={labels.ddl} itemKey="ddl">
          <div style={{ padding: 12 }}>
            <Spin spinning={loadingDdl}>
              <Space vertical align="start" style={{ width: "100%" }}>
                <Button size="small" disabled={!selectedObject} onClick={() => void loadDdl()}>{labels.refresh}</Button>
                <SqlCodeEditor value={ddl} readOnly height={110} />
              </Space>
            </Spin>
          </div>
        </Tabs.TabPane>
      </Tabs>
    </div>
  );

  async function loadDdl() {
    if (!source?.id || !selectedObject) return;
    if (selectedObject.objectType !== "table" && selectedObject.objectType !== "view") {
      setDdl("");
      return;
    }

    setLoadingDdl(true);
    try {
      const result = selectedObject.objectType === "table"
        ? await getTableDdl(source.id, selectedObject.name, selectedObject.schema ?? undefined)
        : await getViewDdl(source.id, selectedObject.name, selectedObject.schema ?? undefined);
      setDdl(result.ddl);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : labels.loadFailed);
    } finally {
      setLoadingDdl(false);
    }
  }
}

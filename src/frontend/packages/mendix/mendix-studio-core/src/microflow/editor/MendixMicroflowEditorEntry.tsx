import { useMemo, useState } from "react";
import { Button, Space, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { IconArrowLeft } from "@douyinfe/semi-icons";
import { MicroflowEditor, type MicroflowApiClient, type MicroflowSchema } from "@atlas/microflow";
import type { MicroflowMetadataAdapter, MicroflowMetadataCatalog } from "@atlas/microflow/metadata";

import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";
import type { MicroflowValidationAdapter } from "../adapter/microflow-validation-adapter";
import { PublishMicroflowModal } from "../publish/PublishMicroflowModal";
import { MicroflowReferencesDrawer } from "../references/MicroflowReferencesDrawer";
import { MicroflowVersionsDrawer } from "../versions/MicroflowVersionsDrawer";
import type { MicroflowResource } from "../resource/resource-types";
import { formatMicroflowDate, microflowPublishStatusLabel, microflowStatusColor, microflowStatusLabel } from "../resource/resource-utils";
import { createMicroflowEditorApiClient } from "./editor-save-bridge";

const { Text } = Typography;

export interface MendixMicroflowEditorEntryProps {
  resource: MicroflowResource;
  adapter: MicroflowResourceAdapter;
  metadataAdapter?: MicroflowMetadataAdapter;
  metadataCatalog?: MicroflowMetadataCatalog;
  runtimeAdapter?: MicroflowApiClient;
  validationAdapter?: MicroflowValidationAdapter;
  onSave?: (resource: MicroflowResource) => void;
  onPublish?: (resource: MicroflowResource) => void;
  onBack?: () => void;
  readonly?: boolean;
}

export function MendixMicroflowEditorEntry({ resource, adapter, metadataAdapter, metadataCatalog, runtimeAdapter, validationAdapter, onSave, onPublish, onBack, readonly }: MendixMicroflowEditorEntryProps) {
  const [schema, setSchema] = useState<MicroflowSchema>(resource.schema);
  const [publishOpen, setPublishOpen] = useState(false);
  const [versionsOpen, setVersionsOpen] = useState(false);
  const [referencesOpen, setReferencesOpen] = useState(false);
  const [currentResource, setCurrentResource] = useState(resource);
  const apiClient = useMemo(() => createMicroflowEditorApiClient(adapter, resource, runtimeAdapter), [adapter, resource, runtimeAdapter]);
  const effectiveReadonly = readonly || currentResource.archived || !(currentResource.permissions?.canEdit ?? true);

  return (
    <div style={{ height: "100%", minHeight: 720, overflow: "hidden", background: "var(--semi-color-bg-0)" }}>
      <MicroflowEditor
        schema={schema}
        apiClient={apiClient}
        metadataAdapter={metadataAdapter}
        metadataCatalog={metadataCatalog}
        readonly={effectiveReadonly}
        onSchemaChange={setSchema}
        onSaveComplete={() => {
          void adapter.getMicroflow(currentResource.id).then(saved => {
            if (saved) {
              setCurrentResource(saved);
              onSave?.(saved);
            }
          });
        }}
        onPublish={() => setPublishOpen(true)}
        toolbarPrefix={
          <Space align="center">
            {onBack ? (
              <Button icon={<IconArrowLeft />} theme="borderless" onClick={onBack}>
                返回资源库
              </Button>
            ) : null}
            <Text strong>{currentResource.displayName || currentResource.name}</Text>
            <Tag color={microflowStatusColor(currentResource.status)}>{microflowStatusLabel(currentResource.status)}</Tag>
            <Tag>{currentResource.version}</Tag>
            <Tag color={currentResource.publishStatus === "changedAfterPublish" ? "orange" : currentResource.publishStatus === "published" ? "green" : "grey"}>{microflowPublishStatusLabel(currentResource.publishStatus)}</Tag>
            <Tag color="blue">Latest {currentResource.latestPublishedVersion ?? "-"}</Tag>
            {currentResource.archived ? <Tag color="grey">只读归档</Tag> : null}
            <Text type="tertiary">{currentResource.updatedBy || currentResource.ownerName || "-"} · {formatMicroflowDate(currentResource.updatedAt)}</Text>
          </Space>
        }
        toolbarSuffix={
          <Space>
            <Button size="small" disabled={currentResource.archived} onClick={() => setPublishOpen(true)}>发布</Button>
            <Button size="small" onClick={() => setVersionsOpen(true)}>版本</Button>
            <Button size="small" onClick={() => setReferencesOpen(true)}>引用</Button>
          </Space>
        }
      />
      <PublishMicroflowModal
        visible={publishOpen}
        resource={currentResource}
        adapter={adapter}
        validationAdapter={validationAdapter}
        onClose={() => setPublishOpen(false)}
        onPublished={published => {
          setCurrentResource(published);
          setSchema(published.schema);
          onPublish?.(published);
          Toast.success("微流发布成功");
        }}
        onViewProblems={issues => Toast.warning(`当前有 ${issues.length} 个校验问题，请查看编辑器问题面板。`)}
        onViewReferences={() => {
          setPublishOpen(false);
          setReferencesOpen(true);
        }}
      />
      <MicroflowVersionsDrawer
        visible={versionsOpen}
        resource={currentResource}
        adapter={adapter}
        onClose={() => setVersionsOpen(false)}
        onResourceChange={next => {
          setCurrentResource(next);
          setSchema(next.schema);
          onSave?.(next);
        }}
        onCreated={() => undefined}
      />
      <MicroflowReferencesDrawer visible={referencesOpen} resource={currentResource} adapter={adapter} onClose={() => setReferencesOpen(false)} />
    </div>
  );
}

export const MicroflowEditorEntry = MendixMicroflowEditorEntry;

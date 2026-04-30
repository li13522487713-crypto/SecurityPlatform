import { Card, Empty, Space, Tag, Typography } from "@douyinfe/semi-ui";

import type { StudioWorkbenchTab } from "../store";
import type { MicroflowModuleAsset } from "../microflow/resource";
import { getMendixStudioCopy } from "../i18n/copy";

const { Text, Title } = Typography;

interface ResourceReadonlyWorkbenchProps {
  tab: StudioWorkbenchTab;
  modules: MicroflowModuleAsset[];
}

function findModule(modules: MicroflowModuleAsset[], moduleId?: string) {
  return modules.find(module => module.moduleId === moduleId);
}

function DetailRow({ label, value }: { label: string; value?: string | number }) {
  return (
    <Space style={{ width: "100%", justifyContent: "space-between" }}>
      <Text type="tertiary">{label}</Text>
      <Text>{value ?? "-"}</Text>
    </Space>
  );
}

export function ResourceReadonlyWorkbench({ tab, modules }: ResourceReadonlyWorkbenchProps) {
  const copy = getMendixStudioCopy();
  const module = findModule(modules, tab.moduleId);
  const page = tab.kind === "page"
    ? module?.pages?.find(item => item.id === tab.resourceId)
    : undefined;
  const workflow = tab.kind === "workflow"
    ? module?.workflows?.find(item => item.id === tab.resourceId)
    : undefined;
  const titleByKind = {
    page: copy.readonlyResource.pageTitle,
    workflow: copy.readonlyResource.workflowTitle,
    domainModel: copy.readonlyResource.domainModelTitle,
    security: copy.readonlyResource.securityTitle,
    navigation: tab.title,
    other: tab.title,
    microflow: tab.title,
  };
  const title = titleByKind[tab.kind] ?? tab.title;

  if (!module) {
    return (
      <div className="studio-readonly-resource">
        <Empty title={copy.readonlyResource.emptyTitle} description={copy.readonlyResource.emptyDescription} />
      </div>
    );
  }

  return (
    <div className="studio-readonly-resource" data-testid={`readonly-resource-${tab.id}`}>
      <Card style={{ width: "min(760px, calc(100% - 48px))", borderRadius: 8 }}>
        <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
          <Space style={{ width: "100%", justifyContent: "space-between" }}>
            <div>
              <Title heading={5} style={{ margin: 0 }}>{tab.title}</Title>
              <Text type="tertiary">{title}</Text>
            </div>
            <Tag color="blue">{copy.readonlyResource.readonlyBadge}</Tag>
          </Space>

          <DetailRow label={copy.readonlyResource.moduleLabel} value={module.qualifiedName || module.name} />
          <DetailRow label={copy.readonlyResource.qualifiedNameLabel} value={tab.qualifiedName} />
          {page ? (
            <>
              <DetailRow label={copy.readonlyResource.descriptionLabel} value={page.description} />
              <DetailRow label={copy.readonlyResource.parametersLabel} value={page.parameterCount} />
            </>
          ) : null}
          {workflow ? (
            <>
              <DetailRow label={copy.readonlyResource.descriptionLabel} value={workflow.description} />
              <DetailRow label={copy.readonlyResource.contextEntityLabel} value={workflow.contextEntityQualifiedName} />
            </>
          ) : null}
          {tab.kind === "domainModel" ? (
            <>
              <DetailRow label={copy.readonlyResource.entitiesLabel} value={module.entities?.length ?? 0} />
              <DetailRow label={copy.readonlyResource.attributesLabel} value={(module.entities ?? []).reduce((total, entity) => total + entity.attributeCount, 0)} />
              <DetailRow label={copy.readonlyResource.associationsLabel} value={(module.entities ?? []).reduce((total, entity) => total + entity.associationCount, 0)} />
            </>
          ) : null}
          {tab.kind === "security" ? (
            <DetailRow label={copy.readonlyResource.entityAccessLabel} value={module.security?.entityAccessCount ?? 0} />
          ) : null}
        </Space>
      </Card>
    </div>
  );
}

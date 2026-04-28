import { useEffect, useState } from "react";
import {
  Banner,
  Button,
  Empty,
  Space,
  TabPane,
  Tabs,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import { IconArrowLeft, IconUpload } from "@douyinfe/semi-icons";
import type {
  KnowledgeBaseDto,
  KnowledgeDetailPageProps,
  KnowledgeDetailTabKey
} from "../types";
import { getLibraryCopy } from "../copy";
import { mapKnowledgeType } from "../utils";
import { KnowledgeStateBadge } from "./knowledge-state-badge";
import {
  BindingsTab,
  DocumentsTab,
  JobsTab,
  OverviewTab,
  PermissionsTab,
  RetrievalTab,
  SlicesTab,
  VersionsTab
} from "./knowledge-detail";

const TAB_KEYS: KnowledgeDetailTabKey[] = [
  "overview",
  "documents",
  "slices",
  "retrieval",
  "bindings",
  "jobs",
  "permissions",
  "versions"
];

function isTabKey(value: string): value is KnowledgeDetailTabKey {
  return (TAB_KEYS as string[]).includes(value);
}

export function KnowledgeDetailPage({
  api,
  locale,
  appKey,
  knowledgeBaseId,
  initialTab,
  onTabChange,
  onNavigate,
  resourceReferencesSlot
}: KnowledgeDetailPageProps) {
  const copy = getLibraryCopy(locale);
  const [knowledge, setKnowledge] = useState<KnowledgeBaseDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [activeTab, setActiveTab] = useState<KnowledgeDetailTabKey>(initialTab ?? "overview");
  const [selectedDocumentId, setSelectedDocumentId] = useState<number | null>(null);

  async function refresh() {
    setLoading(true);
    try {
      const dto = await api.getKnowledgeBase(knowledgeBaseId);
      setKnowledge(dto);
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
    return undefined;
  }, [knowledgeBaseId]);

  // 订阅 mock scheduler，文档/任务变化时刷新概览（计数变更）
  useEffect(() => {
    if (!api.subscribeJobs) return undefined;
    return api.subscribeJobs(knowledgeBaseId, () => {
      void refresh();
    });
  }, [api, knowledgeBaseId]);

  useEffect(() => {
    if (initialTab && isTabKey(initialTab)) {
      setActiveTab(initialTab);
    }
  }, [initialTab]);

  function handleTabChange(next: string): void {
    if (!isTabKey(next)) return;
    setActiveTab(next);
    onTabChange?.(next);
  }

  if (!loading && !knowledge) {
    return (
      <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
        <div className="semi-card-body">
          <Empty description={copy.detailEmpty} />
        </div>
      </div>
    );
  }

  if (!knowledge) {
    return null;
  }

  return (
    <div className="atlas-library-page" data-testid="app-knowledge-detail-page">
      <div className="atlas-page-header">
        <Space spacing={8}>
          <Button
            icon={<IconArrowLeft />}
            onClick={() => onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases`)}
          >
            {copy.backToLibrary}
          </Button>
          <div>
            <Typography.Title heading={3} style={{ margin: 0 }}>
              {knowledge.name}
            </Typography.Title>
            <Space spacing={6} style={{ marginTop: 4 }}>
              <Typography.Text type="tertiary">
                {knowledge.kind ?? copy.typeLabels[knowledge.type]}
              </Typography.Text>
              <KnowledgeStateBadge locale={locale} lifecycle={knowledge.lifecycleStatus ?? "Ready"} />
              {knowledge.versionLabel ? (
                <Tag color="violet" size="small">{knowledge.versionLabel}</Tag>
              ) : null}
            </Space>
          </div>
        </Space>
        <Button
          type="primary"
          icon={<IconUpload />}
          onClick={() => onNavigate(
            `/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases/${knowledgeBaseId}/upload?type=${mapKnowledgeType(knowledge.type)}`
          )}
        >
          {copy.upload}
        </Button>
      </div>

      {(knowledge.failedJobCount ?? 0) > 0 ? (
        <Banner
          type="warning"
          description={`${knowledge.failedJobCount} ${copy.detailTabAlertsCount} · ${copy.jobsTitle}`}
        />
      ) : null}

      <Tabs activeKey={activeTab} onChange={handleTabChange} type="line">
        <TabPane tab={copy.detailTabOverview} itemKey="overview">
          <OverviewTab locale={locale} knowledge={knowledge} resourceReferencesSlot={resourceReferencesSlot} />
        </TabPane>
        <TabPane tab={copy.detailTabDocuments} itemKey="documents">
          <DocumentsTab
            api={api}
            locale={locale}
            knowledge={knowledge}
            appKey={appKey}
            onSelectDocument={(documentId) => {
              setSelectedDocumentId(documentId);
              setActiveTab("slices");
              onTabChange?.("slices");
            }}
            onNavigate={onNavigate}
          />
        </TabPane>
        <TabPane tab={copy.detailTabSlices} itemKey="slices">
          <SlicesTab
            api={api}
            locale={locale}
            knowledge={knowledge}
            selectedDocumentId={selectedDocumentId}
            onSelectDocument={setSelectedDocumentId}
          />
        </TabPane>
        <TabPane tab={copy.detailTabRetrieval} itemKey="retrieval">
          <RetrievalTab api={api} locale={locale} knowledge={knowledge} />
        </TabPane>
        <TabPane tab={`${copy.detailTabBindings} (${knowledge.bindingCount ?? 0})`} itemKey="bindings">
          <BindingsTab api={api} locale={locale} knowledge={knowledge} />
        </TabPane>
        <TabPane tab={`${copy.detailTabJobs} (${(knowledge.pendingJobCount ?? 0) + (knowledge.failedJobCount ?? 0)})`} itemKey="jobs">
          <JobsTab api={api} locale={locale} knowledge={knowledge} />
        </TabPane>
        <TabPane tab={copy.detailTabPermissions} itemKey="permissions">
          <PermissionsTab api={api} locale={locale} knowledge={knowledge} />
        </TabPane>
        <TabPane tab={copy.detailTabVersions} itemKey="versions">
          <VersionsTab api={api} locale={locale} knowledge={knowledge} />
        </TabPane>
      </Tabs>
    </div>
  );
}

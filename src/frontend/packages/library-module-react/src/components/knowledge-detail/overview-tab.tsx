import { Banner, Space, Tag, Typography } from "@douyinfe/semi-ui";
import { formatDateTime } from "../../utils";
import type { KnowledgeBaseDto, SupportedLocale } from "../../types";
import { getLibraryCopy } from "../../copy";
import { KnowledgeStateBadge } from "../knowledge-state-badge";

export interface OverviewTabProps {
  locale: SupportedLocale;
  knowledge: KnowledgeBaseDto;
  resourceReferencesSlot?: React.ReactNode;
}

export function OverviewTab({ locale, knowledge, resourceReferencesSlot }: OverviewTabProps) {
  const copy = getLibraryCopy(locale);
  return (
    <div className="atlas-knowledge-grid">
      <div className="atlas-summary-card semi-card semi-card-bordered semi-card-shadow">
        <div className="semi-card-body">
          <Typography.Title heading={5}>{copy.summary}</Typography.Title>
          <div className="atlas-summary-grid">
            <div className="atlas-summary-tile">
              <span>{copy.knowledgeBase}</span>
              <strong>{knowledge.name}</strong>
            </div>
            <div className="atlas-summary-tile">
              <span>{copy.resourceType}</span>
              <strong>
                {knowledge.kind ?? copy.typeLabels[knowledge.type]}
              </strong>
            </div>
            <div className="atlas-summary-tile">
              <span>{copy.documents}</span>
              <strong>{knowledge.documentCount}</strong>
            </div>
            <div className="atlas-summary-tile">
              <span>{copy.chunks}</span>
              <strong>{knowledge.chunkCount}</strong>
            </div>
            <div className="atlas-summary-tile">
              <span>{copy.detailTabBindings}</span>
              <strong>{knowledge.bindingCount ?? 0}</strong>
            </div>
            <div className="atlas-summary-tile">
              <span>{copy.detailTabAlertsCount}</span>
              <strong>{(knowledge.failedJobCount ?? 0) + (knowledge.pendingJobCount ?? 0)}</strong>
            </div>
            <div className="atlas-summary-tile">
              <span>{copy.detailTabVersions}</span>
              <strong>{knowledge.versionLabel ?? "v0"}</strong>
            </div>
            <div className="atlas-summary-tile">
              <span>{copy.updatedAt}</span>
              <strong>{formatDateTime(knowledge.updatedAt ?? knowledge.createdAt)}</strong>
            </div>
          </div>

          <Space wrap spacing={6} style={{ marginTop: 12 }}>
            <KnowledgeStateBadge locale={locale} lifecycle={knowledge.lifecycleStatus ?? "Ready"} />
            {(knowledge.tags ?? []).map(tag => (
              <Tag key={tag} color="blue" size="small">{tag}</Tag>
            ))}
          </Space>

          <Banner
            type="info"
            description={copy.uploadProcessingHint}
            style={{ marginTop: 12 }}
          />

          {knowledge.description ? (
            <div style={{ marginTop: 12 }}>
              <Typography.Text strong>{copy.noDescription}</Typography.Text>
              <Typography.Paragraph>{knowledge.description}</Typography.Paragraph>
            </div>
          ) : null}
        </div>
      </div>

      {resourceReferencesSlot ? (
        <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
          <div className="semi-card-body">{resourceReferencesSlot}</div>
        </div>
      ) : null}
    </div>
  );
}

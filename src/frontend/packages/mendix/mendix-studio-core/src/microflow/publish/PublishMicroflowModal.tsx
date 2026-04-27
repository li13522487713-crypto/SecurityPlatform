import { useEffect, useMemo, useState } from "react";
import { Button, Checkbox, Input, Modal, Space, Spin, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type { MicroflowValidationIssue } from "@atlas/microflow";

import { getMicroflowApiError, getMicroflowErrorUserMessage, isPublishBlockedError, isValidationFailedError, isVersionConflictError } from "../adapter/http/microflow-api-error";
import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";
import type { MicroflowValidationAdapter } from "../adapter/microflow-validation-adapter";
import { MicroflowErrorState } from "../components/error";
import { nextMicroflowVersion } from "../resource/resource-utils";
import type { MicroflowResource } from "../resource/resource-types";
import { MicroflowReferenceImpactTag } from "../references/MicroflowReferenceImpactTag";
import { getReferenceKindLabel, getReferenceTypeLabel } from "../references/microflow-reference-utils";
import { validatePublishVersion } from "./microflow-publish-utils";
import type { MicroflowPublishImpactAnalysis } from "./microflow-publish-types";
import { PublishBreakingChanges } from "./PublishBreakingChanges";
import { PublishImpactSummary } from "./PublishImpactSummary";
import { PublishValidationSummary } from "./PublishValidationSummary";

const { Text } = Typography;

export interface PublishMicroflowModalProps {
  visible: boolean;
  resource?: MicroflowResource;
  adapter: MicroflowResourceAdapter;
  validationAdapter?: MicroflowValidationAdapter;
  onClose: () => void;
  onPublished: (resource: MicroflowResource) => void;
  onViewProblems?: (issues: MicroflowValidationIssue[]) => void;
  onViewReferences?: () => void;
}

export function PublishMicroflowModal({ visible, resource, adapter, validationAdapter, onClose, onPublished, onViewProblems, onViewReferences }: PublishMicroflowModalProps) {
  const [version, setVersion] = useState("");
  const [description, setDescription] = useState("");
  const [issues, setIssues] = useState<MicroflowValidationIssue[]>([]);
  const [impact, setImpact] = useState<MicroflowPublishImpactAnalysis>();
  const [loading, setLoading] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [apiError, setApiError] = useState<unknown>();
  const [confirmPublish, setConfirmPublish] = useState(false);
  const [confirmBreakingChanges, setConfirmBreakingChanges] = useState(false);
  const [versionMessage, setVersionMessage] = useState<string>();

  const validationSummary = useMemo(() => ({
    errorCount: issues.filter(issue => issue.severity === "error").length,
    warningCount: issues.filter(issue => issue.severity === "warning").length,
    infoCount: issues.filter(issue => issue.severity === "info").length
  }), [issues]);

  useEffect(() => {
    if (!visible || !resource) {
      return;
    }
    const nextVersion = nextMicroflowVersion(resource.latestPublishedVersion || resource.version || "0.0.0");
    setVersion(nextVersion);
    setDescription("");
    setConfirmPublish(false);
    setConfirmBreakingChanges(false);
    setApiError(undefined);
  }, [resource, visible]);

  useEffect(() => {
    if (!visible || !resource || !version.trim()) {
      return;
    }
    setLoading(true);
    setApiError(undefined);
    Promise.all([
      validationAdapter
        ? validationAdapter.validate({ resourceId: resource.id, schema: resource.schema, mode: "publish" })
        : Promise.resolve({
            issues: [{
              id: `MICROFLOW_VALIDATION_ADAPTER_MISSING:${resource.id}`,
              code: "MICROFLOW_VALIDATION_ADAPTER_MISSING",
              severity: "error",
              source: "root",
              fieldPath: "validation",
              message: "发布前校验适配器未配置，已阻止发布。",
            } satisfies MicroflowValidationIssue],
            summary: { errorCount: 1, warningCount: 0, infoCount: 0 },
          }),
      adapter.getMicroflowVersions(resource.id),
      adapter.analyzeMicroflowPublishImpact(resource.id, {
        version: version.trim(),
        includeBreakingChanges: true,
        includeReferences: true,
      })
    ])
      .then(([validation, versions, nextImpact]) => {
        setIssues(validation.issues);
        const versionResult = validatePublishVersion(version, versions);
        setVersionMessage(versionResult.message ?? versionResult.warning);
        setImpact(nextImpact);
      })
      .catch(error => {
        setApiError(error);
        const normalized = getMicroflowApiError(error);
        if (normalized.validationIssues?.length) {
          setIssues(normalized.validationIssues);
        }
        Toast.error(getMicroflowErrorUserMessage(error));
      })
      .finally(() => setLoading(false));
  }, [adapter, description, resource, validationAdapter, version, visible]);

  const hasHighImpact = (impact?.summary.highImpactCount ?? 0) > 0;
  const canPublish = Boolean(resource && version.trim() && validationSummary.errorCount === 0 && confirmPublish && (!hasHighImpact || confirmBreakingChanges) && !versionMessage?.includes("需符合") && !versionMessage?.includes("必填") && !versionMessage?.includes("已存在"));

  async function handlePublish() {
    if (!resource || !canPublish) {
      return;
    }
    setPublishing(true);
    setApiError(undefined);
    try {
      const result = await adapter.publishMicroflow(resource.id, {
        version: version.trim(),
        description: description.trim(),
        confirmBreakingChanges
      });
      Toast.success(`微流已发布为 ${result.version.version}`);
      onPublished(result.resource);
      onClose();
    } catch (error) {
      setApiError(error);
      const normalized = getMicroflowApiError(error);
      if (normalized.validationIssues?.length) {
        setIssues(normalized.validationIssues);
        onViewProblems?.(normalized.validationIssues);
      }
      if (isPublishBlockedError(error) || isValidationFailedError(error) || isVersionConflictError(error)) {
        Toast.warning(getMicroflowErrorUserMessage(error));
      } else {
        Toast.error(getMicroflowErrorUserMessage(error));
      }
    } finally {
      setPublishing(false);
    }
  }

  return (
    <Modal
      visible={visible}
      title="发布微流"
      width={760}
      onCancel={onClose}
      footer={(
        <Space>
          <Button onClick={onClose}>取消</Button>
          <Button type="primary" theme="solid" disabled={!canPublish} loading={publishing} onClick={() => void handlePublish()}>发布</Button>
        </Space>
      )}
    >
      {!resource ? (
        <Spin />
      ) : (
        <Space vertical align="start" spacing={14} style={{ width: "100%" }}>
          <Space wrap>
            <Text strong>{resource.displayName || resource.name}</Text>
            <Tag>当前 {resource.version}</Tag>
            <Tag color="blue">目标 {version || "-"}</Tag>
          </Space>
          <Input value={version} onChange={setVersion} placeholder="1.0.0" prefix="版本号" />
          {versionMessage ? <Text type={versionMessage.includes("不推荐") ? "warning" : "danger"}>{versionMessage}</Text> : null}
          <Input.TextArea value={description} onChange={setDescription} placeholder="发布说明" rows={3} />
          {loading ? <Spin /> : null}
          {apiError ? <MicroflowErrorState error={apiError} title="发布检查失败" compact /> : null}
          <PublishValidationSummary summary={validationSummary} onViewProblems={onViewProblems ? () => onViewProblems(issues) : undefined} />
          <PublishImpactSummary impact={impact} />
          <PublishBreakingChanges changes={impact?.breakingChanges ?? []} />
          <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
            <Text strong>引用预览</Text>
            {(impact?.references ?? []).slice(0, 5).map(reference => (
              <Space key={reference.id} wrap>
                <Text>{reference.sourceName}</Text>
                <Tag>{getReferenceTypeLabel(reference.sourceType)}</Tag>
                <Tag>{getReferenceKindLabel(reference.referenceKind)}</Tag>
                <MicroflowReferenceImpactTag level={reference.impactLevel} />
              </Space>
            ))}
            {(impact?.references.length ?? 0) > 5 && onViewReferences ? <Button size="small" onClick={onViewReferences}>查看全部引用</Button> : null}
          </Space>
          {hasHighImpact ? (
            <Checkbox checked={confirmBreakingChanges} onChange={event => setConfirmBreakingChanges(Boolean(event.target.checked))}>
              我已确认高影响破坏性变更，并接受引用方需要同步调整。
            </Checkbox>
          ) : null}
          <Checkbox checked={confirmPublish} onChange={event => setConfirmPublish(Boolean(event.target.checked))}>
            我确认发布该版本。
          </Checkbox>
        </Space>
      )}
    </Modal>
  );
}

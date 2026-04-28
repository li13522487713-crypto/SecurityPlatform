import { useCallback, useEffect, useMemo, useRef, useState } from "react";
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
  dirty?: boolean;
  onSaveBeforePublish?: () => Promise<MicroflowResource>;
}

function isSampleMicroflow(resource: MicroflowResource): boolean {
  return resource.id === "sampleOrderProcessingMicroflow" || resource.name === "sampleOrderProcessingMicroflow";
}

function isBlockPublishIssue(issue: MicroflowValidationIssue): boolean {
  return issue.blockPublish ?? issue.severity === "error";
}

export function PublishMicroflowModal({ visible, resource, adapter, validationAdapter, onClose, onPublished, onViewProblems, onViewReferences, dirty = false, onSaveBeforePublish }: PublishMicroflowModalProps) {
  const [version, setVersion] = useState("");
  const [description, setDescription] = useState("");
  const [issues, setIssues] = useState<MicroflowValidationIssue[]>([]);
  const [impact, setImpact] = useState<MicroflowPublishImpactAnalysis>();
  const [loading, setLoading] = useState(false);
  const [savingBeforePublish, setSavingBeforePublish] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [apiError, setApiError] = useState<unknown>();
  const [confirmPublish, setConfirmPublish] = useState(false);
  const [confirmBreakingChanges, setConfirmBreakingChanges] = useState(false);
  const [versionMessage, setVersionMessage] = useState<string>();
  const [validatedResourceId, setValidatedResourceId] = useState<string>();
  const [validatedSchemaId, setValidatedSchemaId] = useState<string>();
  const requestSeqRef = useRef(0);

  const validationSummary = useMemo(() => ({
    errorCount: issues.filter(isBlockPublishIssue).length,
    warningCount: issues.filter(issue => issue.severity === "warning").length,
    infoCount: issues.filter(issue => issue.severity === "info").length
  }), [issues]);

  const runPrePublishChecks = useCallback(async (target: MicroflowResource, targetVersion: string) => {
    const requestSeq = requestSeqRef.current + 1;
    requestSeqRef.current = requestSeq;
    setLoading(true);
    setApiError(undefined);
    setImpact(undefined);
    setValidatedResourceId(undefined);
    setValidatedSchemaId(undefined);
    try {
      const [validation, versions, nextImpact] = await Promise.all([
        validationAdapter
          ? validationAdapter.validate({ resourceId: target.id, schema: target.schema, mode: "publish", includeWarnings: true, includeInfo: true })
          : Promise.resolve({
              issues: [{
                id: `MICROFLOW_VALIDATION_ADAPTER_MISSING:${target.id}`,
                code: "MICROFLOW_VALIDATION_ADAPTER_MISSING",
                severity: "error",
                source: "root",
                fieldPath: "validation",
                message: "发布前校验适配器未配置，已阻止发布。",
                blockPublish: true,
              } satisfies MicroflowValidationIssue],
              summary: { errorCount: 1, warningCount: 0, infoCount: 0 },
            }),
        adapter.getMicroflowVersions(target.id),
        adapter.analyzeMicroflowPublishImpact(target.id, {
          version: targetVersion.trim(),
          includeBreakingChanges: true,
          includeReferences: true,
        })
      ]);
      if (requestSeqRef.current !== requestSeq) {
        return validation.issues;
      }
      setIssues(validation.issues);
      const blockIssues = validation.issues.filter(isBlockPublishIssue);
      if (blockIssues.length > 0) {
        onViewProblems?.(blockIssues);
      }
      const versionResult = validatePublishVersion(targetVersion, versions);
      setVersionMessage(versionResult.message ?? versionResult.warning);
      setImpact(nextImpact);
      setValidatedResourceId(target.id);
      setValidatedSchemaId(target.schemaId);
      return validation.issues;
    } catch (error) {
      if (requestSeqRef.current !== requestSeq) {
        return [];
      }
      setApiError(error);
      const normalized = getMicroflowApiError(error);
      if (normalized.validationIssues?.length) {
        setIssues(normalized.validationIssues);
      }
      Toast.error(getMicroflowErrorUserMessage(error));
      throw error;
    } finally {
      if (requestSeqRef.current === requestSeq) {
        setLoading(false);
      }
    }
  }, [adapter, onViewProblems, validationAdapter]);

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
    setIssues([]);
    setImpact(undefined);
    setValidatedResourceId(undefined);
    setValidatedSchemaId(undefined);
  }, [resource, visible]);

  useEffect(() => {
    if (!visible || !resource || !version.trim()) {
      return;
    }
    void runPrePublishChecks(resource, version);
  }, [resource, runPrePublishChecks, version, visible]);

  const hasHighImpact = (impact?.summary.highImpactCount ?? 0) > 0;
  const schemaValidated = Boolean(resource && validatedResourceId === resource.id && validatedSchemaId === resource.schemaId);
  const sampleBlocked = Boolean(resource && isSampleMicroflow(resource));
  const canPublish = Boolean(
    resource
      && version.trim()
      && validationSummary.errorCount === 0
      && confirmPublish
      && schemaValidated
      && !loading
      && !savingBeforePublish
      && !sampleBlocked
      && (!dirty || Boolean(onSaveBeforePublish))
      && (!hasHighImpact || confirmBreakingChanges)
      && !versionMessage?.includes("需符合")
      && !versionMessage?.includes("必填")
      && !versionMessage?.includes("已存在")
  );

  async function handleValidate() {
    if (!resource || !version.trim()) {
      return;
    }
    await runPrePublishChecks(resource, version);
  }

  async function handlePublish(saveFirst: boolean) {
    if (!resource || !canPublish) {
      if (validationSummary.errorCount > 0) {
        onViewProblems?.(issues);
      }
      return;
    }
    let publishResource = resource;
    setPublishing(true);
    setApiError(undefined);
    try {
      if (saveFirst) {
        if (!onSaveBeforePublish) {
          throw new Error("当前微流有未保存更改，但未配置 Save & Publish 保存入口。");
        }
        setSavingBeforePublish(true);
        publishResource = await onSaveBeforePublish();
        setSavingBeforePublish(false);
      }
      const latestIssues = await runPrePublishChecks(publishResource, version);
      const blockIssues = latestIssues.filter(isBlockPublishIssue);
      if (blockIssues.length > 0) {
        onViewProblems?.(blockIssues);
        Toast.warning("发布被校验错误阻止，请先处理 Problems。");
        return;
      }
      const result = await adapter.publishMicroflow(publishResource.id, {
        version: version.trim(),
        description: description.trim(),
        releaseNote: description.trim(),
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
      setSavingBeforePublish(false);
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
          <Button disabled={!resource || loading || publishing} onClick={() => void handleValidate()}>Validate</Button>
          <Button
            type="primary"
            theme="solid"
            disabled={!canPublish}
            loading={publishing || savingBeforePublish}
            onClick={() => void handlePublish(dirty)}
          >
            {dirty ? "Save & Publish" : "Publish"}
          </Button>
        </Space>
      )}
    >
      {!resource ? (
        <Spin />
      ) : (
        <Space vertical align="start" spacing={14} style={{ width: "100%" }}>
          <Space wrap>
            <Text strong>{resource.displayName || resource.name}</Text>
            <Tag>{resource.qualifiedName}</Tag>
            <Tag>id {resource.id}</Tag>
            <Tag>模块 {resource.moduleName || resource.moduleId}</Tag>
            <Tag color={resource.status === "published" ? "green" : resource.status === "archived" ? "grey" : "blue"}>{resource.status}</Tag>
            <Tag color={resource.publishStatus === "changedAfterPublish" ? "orange" : resource.publishStatus === "published" ? "green" : "grey"}>{resource.publishStatus ?? "neverPublished"}</Tag>
            <Tag>当前 {resource.version}</Tag>
            <Tag>schema {resource.schemaId}</Tag>
            <Tag color={dirty ? "orange" : "green"}>{dirty ? "dirty" : "saved"}</Tag>
            <Tag color="blue">目标 {version || "-"}</Tag>
            <Tag color="blue">最新发布 {resource.latestPublishedVersion ?? "-"}</Tag>
          </Space>
          <Input value={version} onChange={setVersion} placeholder="1.0.0" prefix="版本号" />
          {versionMessage ? <Text type={versionMessage.includes("不推荐") ? "warning" : "danger"}>{versionMessage}</Text> : null}
          <Input.TextArea value={description} onChange={setDescription} placeholder="Version notes / release notes" rows={3} />
          {dirty ? <Text type="warning">当前微流有未保存更改。本轮默认使用 Save & Publish：先保存当前 schema，保存成功后重新校验并调用真实 publish API。</Text> : null}
          {sampleBlocked ? <Text type="danger">sampleOrderProcessingMicroflow 禁止发布，请从 App Explorer 打开真实微流资源。</Text> : null}
          {!schemaValidated ? <Text type="warning">当前 schema 尚未完成本轮发布前校验，Publish 将保持禁用。</Text> : null}
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

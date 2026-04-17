/**
 * Coze 上游 Thrift 生成的 `workflow_api` 类型名仍含 `WorkflowV2`（与 HTTP `api/v2/workflows` 无必然对应）。
 * 本文件提供 Atlas 侧语义别名，供业务代码选用；请勿手改 `auto-generated/**`，以免被 idl2ts 覆盖。
 */
export type {
  WorkflowV2 as CozeDagWorkflow,
  WorkflowV2Data as CozeDagWorkflowData,
  CreateWorkflowV2Data as CreateCozeDagWorkflowData,
  CreateWorkflowV2Request as CreateCozeDagWorkflowRequest,
  CreateWorkflowV2Response as CreateCozeDagWorkflowResponse,
  SaveWorkflowV2Data as SaveCozeDagWorkflowData,
  SaveWorkflowV2Request as SaveCozeDagWorkflowRequest,
  SaveWorkflowV2Response as SaveCozeDagWorkflowResponse,
  PublishWorkflowV2Data as PublishCozeDagWorkflowData,
  PublishWorkflowV2Request as PublishCozeDagWorkflowRequest,
  PublishWorkflowV2Response as PublishCozeDagWorkflowResponse,
  QueryWorkflowV2Request as QueryCozeDagWorkflowRequest,
  QueryWorkflowV2Response as QueryCozeDagWorkflowResponse,
  DeleteWorkflowV2Data as DeleteCozeDagWorkflowData,
  DeleteWorkflowV2Request as DeleteCozeDagWorkflowRequest,
  DeleteWorkflowV2Response as DeleteCozeDagWorkflowResponse,
  CopyWorkflowV2Data as CopyCozeDagWorkflowData,
  CopyWorkflowV2Request as CopyCozeDagWorkflowRequest,
  CopyWorkflowV2Response as CopyCozeDagWorkflowResponse,
} from './auto-generated/workflow_api/namespaces/workflow';

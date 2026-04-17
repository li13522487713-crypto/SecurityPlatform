using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.Coze;

// Coze PRD Phase III - 持久化演进记录：
// - M4.3：InMemoryWorkspaceTestsetService → WorkspaceTestsetService（EvaluationDataset + EvaluationCase）
// - M4.4：InMemoryWorkspaceTaskService   → WorkspaceTaskService       （EvaluationTask）
// - M5.1：InMemoryWorkspaceEvaluationService → WorkspaceEvaluationService（EvaluationTask + EvaluationResult + WorkspaceId）
// 所有 in-memory 实现历史代码已删除，避免误注册造成数据漂移。

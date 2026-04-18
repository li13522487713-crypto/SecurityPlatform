namespace Atlas.Application.AiPlatform.Models;

public enum KnowledgePermissionScope
{
    Space = 0,
    Project = 1,
    KnowledgeBase = 2,
    Document = 3
}

public enum KnowledgePermissionSubjectType
{
    User = 0,
    Role = 1,
    Group = 2
}

public enum KnowledgePermissionAction
{
    View = 0,
    Edit = 1,
    Delete = 2,
    Publish = 3,
    Manage = 4,
    Retrieve = 5
}

public sealed record KnowledgePermissionDto(
    long Id,
    KnowledgePermissionScope Scope,
    string ScopeId,
    KnowledgePermissionSubjectType SubjectType,
    string SubjectId,
    string SubjectName,
    IReadOnlyList<KnowledgePermissionAction> Actions,
    string GrantedBy,
    DateTime GrantedAt,
    long? KnowledgeBaseId = null,
    long? DocumentId = null);

public sealed record KnowledgePermissionGrantRequest(
    KnowledgePermissionScope Scope,
    string ScopeId,
    KnowledgePermissionSubjectType SubjectType,
    string SubjectId,
    string SubjectName,
    IReadOnlyList<KnowledgePermissionAction> Actions,
    long? KnowledgeBaseId = null,
    long? DocumentId = null);

$ErrorActionPreference = 'Stop'
$controllerDirs = @(
    (Join-Path $PSScriptRoot '..\src\backend\Atlas.PlatformHost\Controllers'),
    (Join-Path $PSScriptRoot '..\src\backend\Atlas.AppHost\Controllers')
)

foreach ($dir in $controllerDirs) {
    if (-not (Test-Path $dir)) {
        continue
    }

    Get-ChildItem $dir -Filter *.cs -Recurse | ForEach-Object {
        $c = [IO.File]::ReadAllText($_.FullName)
        $orig = $c
        $c = $c -replace 'Fail\(ErrorCodes\.Unauthorized, "未登录", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "知识库不存在", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "KnowledgeBaseNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.ValidationError, "请提供 fileId 或上传文件", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.ValidationError, ApiResponseLocalizer.T(HttpContext, "KnowledgeBaseImportRequiresFileOrId"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "变量不存在", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AiVariableNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "用户不存在", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "UserNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\("NOT_FOUND", "模板不存在", HttpContext\.TraceIdentifier\)', 'Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "ComponentTemplateNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "参数不存在", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "SystemConfigNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "工作流不存在", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowDefNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "执行实例不存在", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "数据库不存在", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AiDatabaseNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.ValidationError, "BotId 必须大于 0", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.ValidationError, ApiResponseLocalizer.T(HttpContext, "AiDatabaseBotIdInvalid"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "版本不存在或不属于此工作流", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowVersionNotInWorkflow"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "节点执行记录不存在", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "WorkflowNodeExecutionNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\("NOT_FOUND", "插件不存在", HttpContext\.TraceIdentifier\)', 'Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "PluginMarketItemNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "文件不存在", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "FileRecordNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.ValidationError, "证书内容不能为空", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.ValidationError, ApiResponseLocalizer.T(HttpContext, "LicenseCertificateRequired"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\("NOT_FOUND", "连接器不存在", HttpContext\.TraceIdentifier\)', 'Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "ApiConnectorNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "Prompt 模板不存在", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "PromptTemplateNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\("NOT_FOUND", "任务不存在", HttpContext\.TraceIdentifier\)', 'Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "ApprovalTaskNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\("NOT_FOUND", "代理设置不存在", HttpContext\.TraceIdentifier\)', 'Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "ApprovalAgentConfigNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "会话不存在", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "ConversationNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "Agent 不存在", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AgentNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\("NOT_FOUND", "订阅不存在", HttpContext\.TraceIdentifier\)', 'Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "WebhookSubscriptionNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\("NOT_FOUND", "租户不存在", HttpContext\.TraceIdentifier\)', 'Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "TenantNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "应用不存在", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "LowCodeAppNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "应用部门不存在。", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AppOrgDepartmentNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "应用职位不存在。", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AppOrgPositionNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(ErrorCodes\.NotFound, "应用项目不存在。", HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AppOrgProjectNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(\s*ErrorCodes\.NotFound,\s*"应用级权限不存在。",\s*HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AppScopedPermissionNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\(\s*ErrorCodes\.NotFound,\s*"应用角色不存在。",\s*HttpContext\.TraceIdentifier\)', 'Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AppOrgRoleNotFound"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\("FEATURE_DISABLED", "SSO 未启用", HttpContext\.TraceIdentifier\)', 'Fail("FEATURE_DISABLED", ApiResponseLocalizer.T(HttpContext, "SsoFeatureDisabled"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\("UNAUTHORIZED", "需要 X-Api-Key", HttpContext\.TraceIdentifier\)', 'Fail("UNAUTHORIZED", ApiResponseLocalizer.T(HttpContext, "IntegrationApiKeyRequired"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\("VALIDATION_ERROR", "X-Tenant-Id 格式无效", HttpContext\.TraceIdentifier\)', 'Fail("VALIDATION_ERROR", ApiResponseLocalizer.T(HttpContext, "IntegrationTenantIdInvalid"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\("UNAUTHORIZED", "API Key 无效或无权限", HttpContext\.TraceIdentifier\)', 'Fail("UNAUTHORIZED", ApiResponseLocalizer.T(HttpContext, "IntegrationApiKeyInvalid"), HttpContext.TraceIdentifier)'
        $c = $c -replace 'Fail\("NOT_FOUND", "审批实例不存在", HttpContext\.TraceIdentifier\)', 'Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "ApprovalInstanceNotFoundShort"), HttpContext.TraceIdentifier)'
        if ($c -ne $orig) {
            [IO.File]::WriteAllText($_.FullName, $c)
            Write-Host $_.FullName
        }
    }
}

# 页面运行时执行实现指南

## 概述

本文档描述低代码平台已发布页面的运行时渲染架构和实现计划。

## 当前状态

**已完成**：
- ✅ LowCodeApp 实体（应用定义）
- ✅ LowCodePage 实体（页面定义）
- ✅ LowCodeAppsController（应用和页面CRUD API）
- ✅ AppBuilderPage.vue（前端应用构建器）
- ✅ X6ApprovalDesigner.vue（审批流程设计器）

**待实现（TODO）**：
- ⏳ PageRuntime Service（页面运行时服务）
- ⏳ 动态路由注册机制
- ⏳ 已发布页面的公开访问端点
- ⏳ 前端运行时渲染器组件

## 架构设计

### 1. 运行时访问流程

```
用户访问 → 公开运行时端点 → 验证权限 → 渲染页面Schema → 返回HTML/JSON
   ↓
/runtime/apps/{appKey}/pages/{pageKey}
   ↓
PageRuntimeService
   ↓
1. 查询应用和页面（必须已发布）
2. 检查权限（PermissionCode）
3. 获取SchemaJson
4. 返回给前端amis-renderer渲染
```

### 2. 数据模型

#### RuntimePageContext
```csharp
public class RuntimePageContext
{
    public string AppKey { get; set; }
    public string PageKey { get; set; }
    public long UserId { get; set; }
    public TenantId TenantId { get; set; }
    public Dictionary<string, string> RouteParams { get; set; }  // 路由参数
    public Dictionary<string, string> QueryParams { get; set; }  // 查询参数
}
```

#### RuntimePageResult
```csharp
public class RuntimePageResult
{
    public long AppId { get; set; }
    public string AppName { get; set; }
    public long PageId { get; set; }
    public string PageName { get; set; }
    public string PageType { get; set; }
    public JsonElement SchemaJson { get; set; }  // amis schema
    public Dictionary<string, object> InitialData { get; set; }  // 初始数据
}
```

### 3. 服务层设计

#### IPageRuntimeService
```csharp
public interface IPageRuntimeService
{
    /// <summary>
    /// 渲染已发布页面
    /// </summary>
    Task<RuntimePageResult?> RenderPageAsync(
        RuntimePageContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// 获取应用的菜单结构（用于导航）
    /// </summary>
    Task<List<MenuItem>> GetAppMenuAsync(
        string appKey,
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// 验证用户是否有权限访问页面
    /// </summary>
    Task<bool> CanAccessPageAsync(
        string appKey,
        string pageKey,
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);
}
```

### 4. 实现步骤

#### Phase 1: 后端运行时服务（优先级：P0）
```
文件位置：
- src/backend/Atlas.Application/LowCode/Abstractions/IPageRuntimeService.cs
- src/backend/Atlas.Infrastructure/Services/LowCode/PageRuntimeService.cs
- src/backend/Atlas.WebApi/Controllers/PageRuntimeController.cs

实现内容：
1. 定义服务接口
2. 实现页面查询逻辑（仅返回已发布页面）
3. 实现权限验证逻辑
4. 创建公开运行时端点
```

#### Phase 2: 前端运行时渲染器（优先级：P0）
```
文件位置：
- src/frontend/Atlas.WebApp/src/pages/runtime/AppRuntimePage.vue
- src/frontend/Atlas.WebApp/src/components/amis/AmisRuntimeRenderer.vue

实现内容：
1. 动态路由配置
2. amis schema动态渲染
3. 运行时数据加载
4. 权限控制集成
```

#### Phase 3: 高级功能（优先级：P1-P2）
```
实现内容：
1. 页面参数传递（路由参数、查询参数）
2. 页面间跳转和通信
3. 运行时数据缓存
4. 页面访问日志和统计
5. 页面预览模式（未发布页面的预览）
```

## API端点设计

### PageRuntimeController

```csharp
[ApiController]
[Route("runtime")]
public class PageRuntimeController : ControllerBase
{
    private readonly IPageRuntimeService _pageRuntimeService;

    /// <summary>
    /// GET /runtime/apps/{appKey}/pages/{pageKey}
    /// 渲染已发布页面
    /// </summary>
    [HttpGet("apps/{appKey}/pages/{pageKey}")]
    [AllowAnonymous]  // 公开访问，内部检查权限
    public async Task<ActionResult<ApiResponse<RuntimePageResult>>> RenderPage(
        [FromRoute] string appKey,
        [FromRoute] string pageKey,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        if (tenantId.IsEmpty)
        {
            return BadRequest(ApiResponse<RuntimePageResult>.Fail(
                ErrorCodes.ValidationError,
                "缺少租户标识",
                HttpContext.TraceIdentifier));
        }

        // 尝试获取当前用户（如果已登录）
        var userId = _currentUserAccessor.TryGetCurrentUser()?.UserId ?? 0;

        var context = new RuntimePageContext
        {
            AppKey = appKey,
            PageKey = pageKey,
            TenantId = tenantId,
            UserId = userId,
            RouteParams = new Dictionary<string, string>(),
            QueryParams = HttpContext.Request.Query
                .ToDictionary(q => q.Key, q => q.Value.ToString())
        };

        var result = await _pageRuntimeService.RenderPageAsync(context, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<RuntimePageResult>.Fail(
                ErrorCodes.NotFound,
                "页面不存在或未发布",
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<RuntimePageResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// GET /runtime/apps/{appKey}/menu
    /// 获取应用菜单结构
    /// </summary>
    [HttpGet("apps/{appKey}/menu")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<MenuItem>>>> GetAppMenu(
        [FromRoute] string appKey,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.TryGetCurrentUser()?.UserId ?? 0;

        var menu = await _pageRuntimeService.GetAppMenuAsync(
            appKey, tenantId, userId, cancellationToken);

        return Ok(ApiResponse<List<MenuItem>>.Ok(menu, HttpContext.TraceIdentifier));
    }
}
```

### 菜单项数据结构
```csharp
public class MenuItem
{
    public long PageId { get; set; }
    public string PageKey { get; set; }
    public string PageName { get; set; }
    public string Icon { get; set; }
    public string RoutePath { get; set; }
    public int SortOrder { get; set; }
    public List<MenuItem> Children { get; set; } = new();  // 支持子菜单
}
```

## 服务实现示例

### PageRuntimeService.RenderPageAsync
```csharp
public async Task<RuntimePageResult?> RenderPageAsync(
    RuntimePageContext context,
    CancellationToken cancellationToken)
{
    // 1. 根据AppKey查询应用
    var app = await _appRepository.FindByKeyAsync(
        context.TenantId, context.AppKey, cancellationToken);
    if (app == null || app.Status != LowCodeAppStatus.Published)
    {
        return null;  // 应用不存在或未发布
    }

    // 2. 根据PageKey查询页面
    var page = await _pageRepository.FindByKeyAsync(
        context.TenantId, app.Id, context.PageKey, cancellationToken);
    if (page == null || !page.IsPublished)
    {
        return null;  // 页面不存在或未发布
    }

    // 3. 权限验证
    if (!string.IsNullOrEmpty(page.PermissionCode))
    {
        var hasPermission = await _permissionService.HasPermissionAsync(
            context.UserId, page.PermissionCode, cancellationToken);
        if (!hasPermission)
        {
            throw new BusinessException("无权限访问此页面", ErrorCodes.Forbidden);
        }
    }

    // 4. 处理路由参数（如 /employees/:id）
    var schemaJson = ResolveSchemaParams(page.SchemaJson, context.RouteParams);

    // 5. 准备初始数据
    var initialData = await LoadInitialDataAsync(page, context, cancellationToken);

    // 6. 返回结果
    return new RuntimePageResult
    {
        AppId = app.Id,
        AppName = app.Name,
        PageId = page.Id,
        PageName = page.Name,
        PageType = page.PageType.ToString(),
        SchemaJson = schemaJson,
        InitialData = initialData
    };
}

private JsonElement ResolveSchemaParams(
    JsonElement schemaJson,
    Dictionary<string, string> routeParams)
{
    // 将Schema中的占位符替换为实际参数值
    // 例如：将 "${id}" 替换为实际的ID值
    var schemaString = schemaJson.GetRawText();

    foreach (var param in routeParams)
    {
        schemaString = schemaString.Replace(
            $"${{{param.Key}}}",
            param.Value);
    }

    return JsonSerializer.Deserialize<JsonElement>(schemaString);
}

private async Task<Dictionary<string, object>> LoadInitialDataAsync(
    LowCodePage page,
    RuntimePageContext context,
    CancellationToken cancellationToken)
{
    var initialData = new Dictionary<string, object>();

    // 如果页面绑定了数据表，加载初始数据
    if (!string.IsNullOrEmpty(page.DataTableKey))
    {
        // 从DynamicTable加载数据
        var data = await _dynamicTableService.QueryAsync(
            context.TenantId,
            page.DataTableKey,
            new PagedRequest { PageIndex = 1, PageSize = 10 },
            cancellationToken);

        initialData["items"] = data.Items;
        initialData["total"] = data.Total;
    }

    return initialData;
}
```

### GetAppMenuAsync
```csharp
public async Task<List<MenuItem>> GetAppMenuAsync(
    string appKey,
    TenantId tenantId,
    long userId,
    CancellationToken cancellationToken)
{
    // 1. 查询应用
    var app = await _appRepository.FindByKeyAsync(tenantId, appKey, cancellationToken);
    if (app == null || app.Status != LowCodeAppStatus.Published)
    {
        return new List<MenuItem>();
    }

    // 2. 查询所有已发布的页面
    var pages = await _pageRepository.GetPublishedPagesAsync(
        tenantId, app.Id, cancellationToken);

    // 3. 过滤用户有权限的页面
    var menuItems = new List<MenuItem>();
    foreach (var page in pages.OrderBy(p => p.SortOrder))
    {
        // 检查权限
        if (!string.IsNullOrEmpty(page.PermissionCode))
        {
            var hasPermission = await _permissionService.HasPermissionAsync(
                userId, page.PermissionCode, cancellationToken);
            if (!hasPermission)
            {
                continue;  // 无权限，跳过
            }
        }

        menuItems.Add(new MenuItem
        {
            PageId = page.Id,
            PageKey = page.PageKey,
            PageName = page.Name,
            Icon = page.Icon ?? "file",
            RoutePath = page.RoutePath,
            SortOrder = page.SortOrder
        });
    }

    // 4. 构建树形菜单（如果有父子关系）
    return BuildMenuTree(menuItems);
}

private List<MenuItem> BuildMenuTree(List<MenuItem> flatMenu)
{
    // 简化版本：假设没有子菜单，直接返回平铺列表
    // 复杂版本：根据ParentPageId构建树形结构
    return flatMenu;
}
```

## 前端运行时渲染器

### AppRuntimePage.vue
```vue
<template>
  <div class="app-runtime-page">
    <aside v-if="showMenu" class="app-sidebar">
      <h2>{{ appName }}</h2>
      <nav class="app-menu">
        <router-link
          v-for="item in menu"
          :key="item.pageId"
          :to="`/runtime/apps/${appKey}/pages/${item.pageKey}`"
          class="menu-item"
        >
          <span class="menu-icon">{{ item.icon }}</span>
          <span class="menu-text">{{ item.pageName }}</span>
        </router-link>
      </nav>
    </aside>

    <main class="app-content">
      <AmisRuntimeRenderer
        v-if="pageSchema"
        :schema="pageSchema"
        :initial-data="initialData"
        @action="handleAction"
      />
      <div v-else-if="loading" class="loading">
        加载中...
      </div>
      <div v-else class="error">
        页面不存在或未发布
      </div>
    </main>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import { useRoute } from 'vue-router';
import { getRuntimePage, getRuntimeMenu } from '@/services/lowcode';
import AmisRuntimeRenderer from '@/components/amis/AmisRuntimeRenderer.vue';

const route = useRoute();
const appKey = ref(route.params.appKey as string);
const pageKey = ref(route.params.pageKey as string);

const loading = ref(false);
const appName = ref('');
const menu = ref<any[]>([]);
const pageSchema = ref<any>(null);
const initialData = ref<any>({});
const showMenu = ref(true);

const loadPage = async () => {
  loading.value = true;
  try {
    const result = await getRuntimePage(appKey.value, pageKey.value);
    appName.value = result.appName;
    pageSchema.value = result.schemaJson;
    initialData.value = result.initialData;
  } catch (error) {
    console.error('Failed to load page:', error);
    pageSchema.value = null;
  } finally {
    loading.value = false;
  }
};

const loadMenu = async () => {
  try {
    menu.value = await getRuntimeMenu(appKey.value);
  } catch (error) {
    console.error('Failed to load menu:', error);
  }
};

const handleAction = (action: any) => {
  console.log('Page action:', action);
  // 处理页面事件（如按钮点击、表单提交等）
};

onMounted(() => {
  loadPage();
  loadMenu();
});

watch(() => route.params.pageKey, () => {
  pageKey.value = route.params.pageKey as string;
  loadPage();
});
</script>

<style scoped>
.app-runtime-page {
  display: flex;
  height: 100vh;
}

.app-sidebar {
  width: 240px;
  background: #001529;
  color: white;
  padding: 20px;
}

.app-content {
  flex: 1;
  overflow: auto;
  padding: 20px;
}

.menu-item {
  display: flex;
  align-items: center;
  padding: 12px;
  color: rgba(255, 255, 255, 0.65);
  text-decoration: none;
  transition: all 0.3s;
}

.menu-item:hover,
.menu-item.router-link-active {
  color: white;
  background: rgba(255, 255, 255, 0.1);
}

.loading,
.error {
  text-align: center;
  padding: 40px;
  color: #999;
}
</style>
```

### 前端API服务
```typescript
// src/frontend/Atlas.WebApp/src/services/lowcode.ts

export async function getRuntimePage(appKey: string, pageKey: string) {
  return requestApi<RuntimePageResult>(
    `/runtime/apps/${appKey}/pages/${pageKey}`
  );
}

export async function getRuntimeMenu(appKey: string) {
  return requestApi<MenuItem[]>(`/runtime/apps/${appKey}/menu`);
}

export interface RuntimePageResult {
  appId: number;
  appName: string;
  pageId: number;
  pageName: string;
  pageType: string;
  schemaJson: any;
  initialData: Record<string, any>;
}

export interface MenuItem {
  pageId: number;
  pageKey: string;
  pageName: string;
  icon: string;
  routePath: string;
  sortOrder: number;
  children?: MenuItem[];
}
```

### 动态路由配置
```typescript
// src/frontend/Atlas.WebApp/src/router/index.ts

{
  path: '/runtime/apps/:appKey',
  component: () => import('@/layouts/RuntimeLayout.vue'),
  children: [
    {
      path: 'pages/:pageKey',
      name: 'RuntimePage',
      component: () => import('@/pages/runtime/AppRuntimePage.vue'),
      meta: {
        title: '运行时页面',
        requiresAuth: false  // 公开访问，内部检查权限
      }
    }
  ]
}
```

## 实施优先级

| 优先级 | 功能 | 时间估算 | 依赖 |
|-------|------|---------|------|
| P0 | PageRuntimeController API端点 | 1天 | 无 |
| P0 | PageRuntimeService基础实现 | 2天 | 无 |
| P0 | 前端AppRuntimePage组件 | 2天 | amis-renderer |
| P0 | 动态路由配置 | 1天 | P0后端完成 |
| P1 | 权限验证集成 | 1天 | P0完成 |
| P1 | 菜单导航功能 | 1天 | P0完成 |
| P2 | 页面参数传递 | 2天 | P0+P1完成 |
| P2 | 页面预览模式 | 1天 | P0+P1完成 |

**总计**: 约11天（2周）

## TODO清单

- [ ] 创建 PageRuntimeController
- [ ] 实现 IPageRuntimeService
- [ ] 权限验证逻辑
- [ ] 创建 AppRuntimePage.vue
- [ ] 配置动态路由
- [ ] 实现菜单导航
- [ ] 页面参数传递
- [ ] 测试文件 PageRuntime.http
- [ ] 编写单元测试
- [ ] 更新文档

## 参考资料

- AMIS页面Schema: https://aisuda.bce.baidu.com/amis/zh-CN/components/page
- Vue Router动态路由: https://router.vuejs.org/guide/essentials/dynamic-matching.html
- SPA权限控制模式: https://developer.mozilla.org/en-US/docs/Web/Security/Same-origin_policy

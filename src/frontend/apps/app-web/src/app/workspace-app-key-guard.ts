/**
 * 判断当前 `/w/:workspaceId/*` 路由是否依赖 workspace.appKey。
 *
 * 约束边界：
 * - 仅 Agent / App 的详情与发布入口依赖 appKey
 * - 一级菜单页（home/develop/library/manage/publish-center/settings）不依赖 appKey
 */
export function requiresWorkspaceAppKey(pathname: string): boolean {
  if (!pathname.startsWith("/w/")) {
    return false;
  }

  const normalizedPath = pathname.replace(/\/+$/, "");

  return (
    /^\/w\/[^/]+\/agents\/[^/]+(?:\/publish)?$/i.test(normalizedPath) ||
    /^\/w\/[^/]+\/apps\/[^/]+(?:\/publish|\/workflows\/[^/]+|\/chatflows\/[^/]+)?$/i.test(normalizedPath)
  );
}

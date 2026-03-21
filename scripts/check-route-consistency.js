#!/usr/bin/env node
/**
 * 菜单/路由一致性静态检查工具
 *
 * 对比三个路径来源并输出差异报告：
 * 1. 后端种子菜单路径（DatabaseInitializerHostedService.cs）
 * 2. 前端静态路由路径（router/index.ts）
 * 3. 前端 fallback map 路径（dynamic-router.ts）
 *
 * 用法：node scripts/check-route-consistency.js
 */

import { readFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dirname, '..');

function extractSeedPaths() {
  const filePath = join(ROOT, 'src/backend/Atlas.Infrastructure/Services/DatabaseInitializerHostedService.cs');
  const content = readFileSync(filePath, 'utf-8');
  const paths = new Set();

  // 匹配种子菜单中的路径定义（路径是菜单数据的第一个字符串参数区域之后的第二个字符串）
  // menuSeeds 中的格式: ("菜单名", "/path", "/parentPath", ...)
  const menuLineRegex = /\("([^"]+)",\s*"(\/[^"]*)",\s*"(\/[^"]*|null)"/g;
  let match;
  while ((match = menuLineRegex.exec(content)) !== null) {
    const path = match[2];
    // 排除按钮路径（带冒号的是权限标识符，如 /settings/auth/menus:create）
    if (!path.includes(':') && path.startsWith('/')) {
      paths.add(path);
    }
  }

  // 额外提取路由白名单数组中的路径
  const allowedPathsRegex = /"(\/[^"]+)"/g;
  // 找到 allowedMenuPaths 或 hiddenDetailRoutes 的上下文
  const allowedSectionMatch = content.match(/hiddenDetailRoutes\s*=\s*new\s*\[\s*([\s\S]*?)\s*\]/);
  if (allowedSectionMatch) {
    const section = allowedSectionMatch[1];
    let m;
    while ((m = allowedPathsRegex.exec(section)) !== null) {
      paths.add(m[1]);
    }
  }

  return paths;
}

function extractStaticRouterPaths() {
  const filePath = join(ROOT, 'src/frontend/Atlas.WebApp/src/router/index.ts');
  const content = readFileSync(filePath, 'utf-8');
  const paths = new Set();

  // 匹配 path: "/..." 的静态路由定义
  const pathRegex = /\bpath:\s*["'](\/?[^"']+)["']/g;
  let match;
  while ((match = pathRegex.exec(content)) !== null) {
    const p = match[1];
    if (p.startsWith('/') && !p.startsWith('/:pathMatch')) {
      paths.add(p);
    }
  }

  return paths;
}

function extractFallbackMapPaths() {
  const filePath = join(ROOT, 'src/frontend/Atlas.WebApp/src/utils/dynamic-router.ts');
  const content = readFileSync(filePath, 'utf-8');
  const paths = new Set();

  // 匹配 pathComponentFallbackMap 中的键
  const mapMatch = content.match(/const pathComponentFallbackMap[^=]*=\s*\{([\s\S]*?)\};/);
  if (mapMatch) {
    const mapContent = mapMatch[1];
    const keyRegex = /"(\/[^"]+)":\s*"/g;
    let m;
    while ((m = keyRegex.exec(mapContent)) !== null) {
      paths.add(m[1]);
    }
  }

  return paths;
}

// 执行检查
console.log('=== 菜单/路由一致性检查报告 ===\n');

const seedPaths = extractSeedPaths();
const staticPaths = extractStaticRouterPaths();
const fallbackPaths = extractFallbackMapPaths();

console.log(`种子菜单路径数：${seedPaths.size}`);
console.log(`前端静态路由路径数：${staticPaths.size}`);
console.log(`fallback map 路径数：${fallbackPaths.size}\n`);

// 检查1：种子路径在静态路由中但不在 fallback map 中的（可能需要 fallback）
const seedNotInFallback = [...seedPaths].filter(p => !fallbackPaths.has(p) && !p.includes(':'));
if (seedNotInFallback.length > 0) {
  console.log(`[WARN] 种子菜单路径不在 fallback map 中（${seedNotInFallback.length} 个）：`);
  seedNotInFallback.forEach(p => console.log(`  - ${p}`));
  console.log();
}

// 检查2：静态路由中的 Deprecated 路由（带有 Deprecated 关键字的）
const deprecatedRoutes = [...staticPaths].filter(p => {
  // 在文件中查找对应路由定义是否包含 Deprecated
  const content = readFileSync(join(ROOT, 'src/frontend/Atlas.WebApp/src/router/index.ts'), 'utf-8');
  const escapedPath = p.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const regex = new RegExp(`path:\\s*["']${escapedPath}["'][^}]+Deprecated`);
  return regex.test(content);
});
if (deprecatedRoutes.length > 0) {
  console.log(`[INFO] 已标记为 Deprecated 的静态路由（${deprecatedRoutes.length} 个）：`);
  deprecatedRoutes.forEach(p => console.log(`  - ${p}`));
  console.log();
}

// 检查3：fallback map 路径不在种子菜单中（孤立的 fallback 映射）
const fallbackNotInSeed = [...fallbackPaths].filter(p => !seedPaths.has(p) && !p.includes(':'));
if (fallbackNotInSeed.length > 0) {
  console.log(`[INFO] fallback map 中的路径不在种子菜单中（${fallbackNotInSeed.length} 个，可能是应用路由或遗留路由）：`);
  fallbackNotInSeed.forEach(p => console.log(`  - ${p}`));
  console.log();
}

// 检查4：静态路由与动态路由潜在冲突（同路径在静态路由 + 种子菜单中都出现）
const potentialConflicts = [...seedPaths].filter(p => staticPaths.has(p));
if (potentialConflicts.length > 0) {
  console.log(`[WARN] 种子菜单路径与前端静态路由可能冲突（${potentialConflicts.length} 个，case-04 已有 path 去重保护）：`);
  potentialConflicts.forEach(p => console.log(`  - ${p}`));
  console.log();
}

console.log('=== 检查完成 ===');
if (seedNotInFallback.length === 0 && potentialConflicts.length === 0) {
  console.log('✓ 无高优先级一致性问题');
} else {
  console.log(`发现 ${seedNotInFallback.length} 个 WARN 项，${potentialConflicts.length} 个潜在冲突，请人工确认。`);
}

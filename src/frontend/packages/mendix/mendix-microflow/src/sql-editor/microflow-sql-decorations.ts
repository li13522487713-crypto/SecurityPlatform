/**
 * 微流 SQL 占位符装饰工具
 * 
 * 负责扫描 SQL 文本中的微流变量占位符并提供高亮装饰信息：
 * - $.variableName   → 紫色（局部变量）
 * - $global.varName  → 橙色（全局变量）
 * - $currentUser.field → 青色（当前用户字段）
 * 
 * 不依赖 Monaco 或 CodeMirror，纯逻辑层，可被任意编辑器桥接。
 */

export type MicroflowPlaceholderKind = "local" | "global" | "currentUser";

export interface MicroflowSqlPlaceholder {
  /** 占位符全文（含 $ 符号） */
  raw: string;
  /** 变量名（不含前缀） */
  name: string;
  /** 作用域类型 */
  kind: MicroflowPlaceholderKind;
  /** 在 SQL 字符串中的起始字符位置 */
  start: number;
  /** 在 SQL 字符串中的结束字符位置（exclusive） */
  end: number;
  /** 行号（0-indexed） */
  line: number;
  /** 列号（0-indexed） */
  column: number;
}

/**
 * 局部变量：$.varName 或 $.var.nested
 * 全局变量：$global.varName
 * 当前用户：$currentUser.field
 */
const PLACEHOLDER_RE = /\$(?:global|currentUser)?\.\w+(?:\.\w+)*/g;

function classifyKind(match: string): MicroflowPlaceholderKind {
  if (match.startsWith("$global.")) return "global";
  if (match.startsWith("$currentUser.")) return "currentUser";
  return "local";
}

function extractName(match: string, kind: MicroflowPlaceholderKind): string {
  if (kind === "global") return match.slice("$global.".length);
  if (kind === "currentUser") return match.slice("$currentUser.".length);
  return match.slice("$.".length);
}

/**
 * 扫描 SQL 字符串，返回所有微流占位符装饰信息。
 */
export function scanMicroflowSqlPlaceholders(sql: string): MicroflowSqlPlaceholder[] {
  const results: MicroflowSqlPlaceholder[] = [];
  const lines = sql.split("\n");
  let lineStart = 0;

  PLACEHOLDER_RE.lastIndex = 0;
  let match: RegExpExecArray | null;
  while ((match = PLACEHOLDER_RE.exec(sql)) !== null) {
    const start = match.index;
    const raw = match[0];
    const end = start + raw.length;

    // 找到该字符在哪一行
    let line = 0;
    let col = start - lineStart;
    let accumulated = 0;
    for (let i = 0; i < lines.length; i++) {
      const lineEnd = accumulated + (lines[i]?.length ?? 0);
      if (start <= lineEnd) {
        line = i;
        col = start - accumulated;
        break;
      }
      accumulated += (lines[i]?.length ?? 0) + 1; // +1 for \n
    }

    const kind = classifyKind(raw);
    results.push({
      raw,
      name: extractName(raw, kind),
      kind,
      start,
      end,
      line,
      column: col,
    });
  }

  lineStart = 0;
  return results;
}

/**
 * 获取占位符的 CSS 颜色变量（与 Semi UI 主题兼容）
 */
export function getPlaceholderColor(kind: MicroflowPlaceholderKind): string {
  switch (kind) {
    case "local": return "var(--semi-color-primary)";           // 紫色/蓝色
    case "global": return "var(--semi-color-warning)";          // 橙色
    case "currentUser": return "var(--semi-color-success)";     // 绿色/青色
  }
}

/**
 * 根据占位符位置生成 <mark> 标签 HTML（用于只读预览高亮）。
 * 注意：此函数输出不转义 SQL 内容以外的部分，调用方需自行处理 XSS。
 */
export function highlightMicroflowSqlPlaceholders(sql: string): string {
  const placeholders = scanMicroflowSqlPlaceholders(sql);
  if (placeholders.length === 0) {
    return sql;
  }

  const parts: string[] = [];
  let cursor = 0;
  for (const ph of placeholders) {
    parts.push(sql.slice(cursor, ph.start));
    const color = getPlaceholderColor(ph.kind);
    parts.push(`<mark style="background:${color}20;color:${color};padding:0 2px;border-radius:2px">${ph.raw}</mark>`);
    cursor = ph.end;
  }
  parts.push(sql.slice(cursor));
  return parts.join("");
}

/**
 * 为 Monaco Editor completionItemProvider 生成候选项。
 * 接受 variableNames 列表（分 kind），输出 CompletionItem 格式（与 Monaco API 兼容但不直接依赖 monaco types）。
 */
export function buildSqlPlaceholderCompletionItems(
  localVariables: string[],
  globalVariables: string[],
  currentUserFields: string[] = ["id", "name", "email", "phone", "roles"],
): Array<{ label: string; insertText: string; detail: string; kind: number }> {
  const VARIABLE_KIND = 6; // Monaco CompletionItemKind.Variable
  const items: Array<{ label: string; insertText: string; detail: string; kind: number }> = [];

  for (const v of localVariables) {
    items.push({ label: `$.${v}`, insertText: `$.${v}`, detail: "局部变量", kind: VARIABLE_KIND });
  }
  for (const v of globalVariables) {
    items.push({ label: `$global.${v}`, insertText: `$global.${v}`, detail: "全局变量", kind: VARIABLE_KIND });
  }
  for (const f of currentUserFields) {
    items.push({ label: `$currentUser.${f}`, insertText: `$currentUser.${f}`, detail: "当前用户", kind: VARIABLE_KIND });
  }
  return items;
}

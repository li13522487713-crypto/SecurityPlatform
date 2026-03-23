/**
 * 【数据展示 III-3.2 列表类】
 * Table / List / Cards 三类展示组件 Schema 工厂函数
 */
import type { AmisSchema } from "@/types/amis";

/** Table Schema 选项 */
export interface TableSchemaOptions {
  columns: Array<{
    name: string;
    label: string;
    type?: string;
    width?: number | string;
    fixed?: "left" | "right";
    [key: string]: unknown;
  }>;
  source?: string;
  title?: string;
  showHeader?: boolean;
  bordered?: boolean;
  striped?: boolean;
  resizable?: boolean;
  affixHeader?: boolean;
  className?: string;
}

/**
 * 创建 Table Schema（静态数据展示，非 CRUD）
 */
export function tableSchema(opts: TableSchemaOptions): AmisSchema {
  return {
    type: "table",
    columns: opts.columns,
    ...(opts.source ? { source: opts.source } : {}),
    ...(opts.title ? { title: opts.title } : {}),
    ...(opts.showHeader !== false ? {} : { showHeader: false }),
    ...(opts.bordered ? { bordered: true } : {}),
    ...(opts.striped ? { striped: true } : {}),
    ...(opts.resizable ? { resizable: true } : {}),
    ...(opts.affixHeader ? { affixHeader: true } : {}),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

/** List Schema 选项 */
export interface ListSchemaOptions {
  listItem: {
    title?: string;
    subTitle?: string;
    desc?: string;
    avatar?: string;
    body?: AmisSchema[];
    actions?: AmisSchema[];
  };
  source?: string;
  title?: string;
  placeholder?: string;
  className?: string;
}

/**
 * 创建 List Schema（列表视图）
 */
export function listSchema(opts: ListSchemaOptions): AmisSchema {
  return {
    type: "list",
    listItem: opts.listItem,
    ...(opts.source ? { source: opts.source } : {}),
    ...(opts.title ? { title: opts.title } : {}),
    ...(opts.placeholder ? { placeholder: opts.placeholder } : { placeholder: "暂无数据" }),
    ...(opts.className ? { className: opts.className } : {}),
  };
}

/** Cards Schema 选项 */
export interface CardsSchemaOptions {
  card: {
    header?: {
      title?: string;
      subTitle?: string;
      avatar?: string;
      avatarClassName?: string;
    };
    body?: AmisSchema[];
    actions?: AmisSchema[];
  };
  source?: string;
  title?: string;
  placeholder?: string;
  className?: string;
  columnsCount?: number;
}

/**
 * 创建 Cards Schema（卡片网格视图）
 */
export function cardsSchema(opts: CardsSchemaOptions): AmisSchema {
  return {
    type: "cards",
    card: opts.card,
    ...(opts.source ? { source: opts.source } : {}),
    ...(opts.title ? { title: opts.title } : {}),
    ...(opts.placeholder ? { placeholder: opts.placeholder } : { placeholder: "暂无数据" }),
    ...(opts.className ? { className: opts.className } : {}),
    ...(opts.columnsCount ? { columnsCount: opts.columnsCount } : {}),
  };
}

// ========== 便捷工厂（预置常用模式） ==========

/** 创建带头像的列表项 */
export function avatarListSchema(opts: {
  source?: string;
  titleField?: string;
  subTitleField?: string;
  avatarField?: string;
  descField?: string;
}): AmisSchema {
  return listSchema({
    source: opts.source,
    listItem: {
      title: `\${${opts.titleField ?? "title"}}`,
      subTitle: `\${${opts.subTitleField ?? "subTitle"}}`,
      avatar: `\${${opts.avatarField ?? "avatar"}}`,
      desc: `\${${opts.descField ?? "description"}}`,
    },
  });
}

/** 创建信息卡片网格 */
export function infoCardsSchema(opts: {
  source?: string;
  titleField?: string;
  bodyFields?: Array<{ name: string; label: string }>;
  columnsCount?: number;
}): AmisSchema {
  return cardsSchema({
    source: opts.source,
    columnsCount: opts.columnsCount ?? 3,
    card: {
      header: {
        title: `\${${opts.titleField ?? "title"}}`,
      },
      body: (opts.bodyFields ?? []).map((f) => ({
        type: "tpl",
        tpl: `<strong>${f.label}：</strong>\${${f.name}}`,
      })),
    },
  });
}

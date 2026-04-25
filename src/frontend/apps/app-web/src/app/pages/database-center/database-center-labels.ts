export interface DatabaseCenterLabels {
  title: string;
  subtitle: string;
  refresh: string;
  newDatabase: string;
  hostProfiles: string;
  sources: string;
  searchSources: string;
  schemas: string;
  noSource: string;
  noSchema: string;
  loading: string;
  loadFailed: string;
  details: string;
  structure: string;
  erDiagram: string;
  sqlEditor: string;
  dock: string;
  tables: string;
  views: string;
  procedures: string;
  triggers: string;
  columns: string;
  preview: string;
  ddl: string;
  copy: string;
  execute: string;
  format: string;
  limit: string;
  affectedRows: string;
  elapsedMs: string;
  createHostProfile: string;
  editHostProfile: string;
  testConnection: string;
  save: string;
  cancel: string;
  delete: string;
  name: string;
  driver: string;
  description: string;
  connectionString: string;
  sqliteRootPath: string;
  defaultDatabase: string;
  defaultSchema: string;
  active: string;
  defaultProfile: string;
  provisionMode: string;
  hostProfile: string;
  physicalDatabase: string;
  environmentMode: string;
  next: string;
  previous: string;
  create: string;
  basicInfo: string;
  hosting: string;
  confirm: string;
  wizardBasicHint: string;
  wizardHostingHint: string;
  createSuccess: string;
  saveSuccess: string;
  testSuccess: string;
  emptyStructure: string;
  readOnly: string;
  createTableVisual: string;
  createTableSql: string;
  createView: string;
  tableName: string;
  viewName: string;
  columnName: string;
  dataType: string;
  nullable: string;
  primaryKey: string;
  autoIncrement: string;
  defaultValue: string;
  comment: string;
  addColumn: string;
  removeColumn: string;
  previewSql: string;
  sqlCreateTable: string;
  viewSelectSql: string;
  previewSuccess: string;
}

export const defaultDatabaseCenterLabels: DatabaseCenterLabels = {
  title: "数据库管理中心",
  subtitle: "管理 AI 数据库、托管配置、Schema 结构与 SQL 查询。",
  refresh: "刷新",
  newDatabase: "新建数据库",
  hostProfiles: "托管配置",
  sources: "数据源",
  searchSources: "搜索数据库",
  schemas: "Schema",
  noSource: "请选择一个数据库资源",
  noSchema: "暂无 Schema",
  loading: "加载中",
  loadFailed: "加载失败",
  details: "详情",
  structure: "结构",
  erDiagram: "ER 图",
  sqlEditor: "SQL 编辑器",
  dock: "Dock",
  tables: "表",
  views: "视图",
  procedures: "存储过程",
  triggers: "触发器",
  columns: "字段",
  preview: "预览",
  ddl: "DDL",
  copy: "复制",
  execute: "执行",
  format: "格式化",
  limit: "行数",
  affectedRows: "影响行数",
  elapsedMs: "耗时",
  createHostProfile: "新建托管配置",
  editHostProfile: "编辑托管配置",
  testConnection: "测试连接",
  save: "保存",
  cancel: "取消",
  delete: "删除",
  name: "名称",
  driver: "驱动",
  description: "描述",
  connectionString: "连接串",
  sqliteRootPath: "SQLite 根目录",
  defaultDatabase: "默认库名",
  defaultSchema: "默认 Schema",
  active: "启用",
  defaultProfile: "默认配置",
  provisionMode: "开通模式",
  hostProfile: "托管配置",
  physicalDatabase: "物理库名",
  environmentMode: "环境模式",
  next: "下一步",
  previous: "上一步",
  create: "创建",
  basicInfo: "基础信息",
  hosting: "托管",
  confirm: "确认",
  wizardBasicHint: "填写资源名称和驱动类型。",
  wizardHostingHint: "选择托管配置和物理库策略。",
  createSuccess: "创建成功",
  saveSuccess: "保存成功",
  testSuccess: "连接测试通过",
  emptyStructure: "当前 Schema 暂无结构对象",
  readOnly: "只读",
  createTableVisual: "可视化建表",
  createTableSql: "SQL 建表",
  createView: "新建视图",
  tableName: "表名",
  viewName: "视图名",
  columnName: "字段名",
  dataType: "数据类型",
  nullable: "可空",
  primaryKey: "主键",
  autoIncrement: "自增",
  defaultValue: "默认值",
  comment: "注释",
  addColumn: "添加字段",
  removeColumn: "移除字段",
  previewSql: "预览 SQL",
  sqlCreateTable: "SQL 建表语句",
  viewSelectSql: "视图 SELECT",
  previewSuccess: "预览成功"
};

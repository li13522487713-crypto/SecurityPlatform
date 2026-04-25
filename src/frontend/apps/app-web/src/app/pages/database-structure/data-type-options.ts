export const DATABASE_TYPE_OPTIONS: Record<string, string[]> = {
  MySql: ["BIGINT", "INT", "VARCHAR", "TEXT", "DATETIME", "TIMESTAMP", "DECIMAL", "TINYINT", "JSON"],
  PostgreSQL: ["BIGINT", "INTEGER", "VARCHAR", "TEXT", "TIMESTAMP", "NUMERIC", "BOOLEAN", "JSONB", "UUID"],
  SqlServer: ["BIGINT", "INT", "NVARCHAR", "VARCHAR", "DATETIME2", "DECIMAL", "BIT", "UNIQUEIDENTIFIER"],
  SQLite: ["INTEGER", "TEXT", "REAL", "NUMERIC", "BLOB"],
  Oracle: ["NUMBER", "VARCHAR2", "NVARCHAR2", "CLOB", "DATE", "TIMESTAMP"],
  Dm: ["BIGINT", "INT", "VARCHAR", "TEXT", "DATETIME", "DECIMAL"],
  Kdbndp: ["BIGINT", "INTEGER", "VARCHAR", "TEXT", "TIMESTAMP", "NUMERIC", "BOOLEAN"],
  Oscar: ["BIGINT", "INTEGER", "VARCHAR", "TEXT", "TIMESTAMP", "NUMERIC"]
};

export function getTypeOptions(driverCode?: string): string[] {
  if (!driverCode) return DATABASE_TYPE_OPTIONS.SQLite;
  return DATABASE_TYPE_OPTIONS[driverCode] ?? DATABASE_TYPE_OPTIONS.SQLite;
}

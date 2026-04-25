const CLAUSES = [
  "select",
  "from",
  "where",
  "group by",
  "order by",
  "having",
  "left join",
  "right join",
  "inner join",
  "outer join",
  "join",
  "values",
  "set"
];

export function formatSql(sql: string): string {
  let formatted = sql.trim().replace(/\s+/g, " ");
  for (const clause of CLAUSES) {
    const pattern = new RegExp(`\\s+${clause}\\s+`, "gi");
    formatted = formatted.replace(pattern, match => `\n${match.trim().toUpperCase()} `);
  }

  return formatted
    .replace(/,\s*/g, ",\n  ")
    .replace(/\(\s*/g, "(\n  ")
    .replace(/\s*\)/g, "\n)")
    .trim();
}

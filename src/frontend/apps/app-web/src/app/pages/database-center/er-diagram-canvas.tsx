import { Empty, Tag, Typography } from "@douyinfe/semi-ui";
import type {
  DatabaseCenterColumnSummary,
  DatabaseCenterObjectSummary,
  DatabaseCenterRelationSummary,
  DatabaseCenterSchemaStructure
} from "../../../services/api-database-center";
import type { DatabaseCenterLabels } from "./database-center-labels";

const { Text } = Typography;

interface ErDiagramCanvasProps {
  labels: DatabaseCenterLabels;
  structure: DatabaseCenterSchemaStructure | null;
  onSelectObject: (object: DatabaseCenterObjectSummary) => void;
}

interface PositionedTable {
  object: DatabaseCenterObjectSummary;
  columns: DatabaseCenterColumnSummary[];
  x: number;
  y: number;
}

export function ErDiagramCanvas({ labels, structure, onSelectObject }: ErDiagramCanvasProps) {
  const tables = (structure?.objects ?? []).filter(item => item.objectType === "table");
  if (!structure || tables.length === 0) {
    return <Empty description={labels.emptyStructure} />;
  }

  const positioned: PositionedTable[] = tables.map((object, index) => ({
    object,
    columns: structure.columnsByObject[object.id] ?? structure.columnsByObject[object.name] ?? [],
    x: 28 + (index % 3) * 260,
    y: 28 + Math.floor(index / 3) * 220
  }));
  const tableByName = new Map(positioned.map(item => [item.object.name, item]));
  const height = Math.max(520, 260 + Math.ceil(positioned.length / 3) * 220);

  return (
    <div className="database-center-er" style={{ height }}>
      {structure.relations.map(relation => (
        <RelationLine key={relation.id} relation={relation} tableByName={tableByName} />
      ))}
      {positioned.map(table => (
        <button
          key={table.object.id}
          type="button"
          className="database-center-er__node"
          style={{ left: table.x, top: table.y, textAlign: "left" }}
          onClick={() => onSelectObject(table.object)}
        >
          <div className="database-center-er__node-header">
            <Text strong ellipsis={{ showTooltip: true }}>{table.object.name}</Text>
          </div>
          {table.columns.slice(0, 8).map(column => (
            <div key={column.name} className="database-center-er__column">
              <span>{column.primaryKey ? "PK " : ""}{column.name}</span>
              <Tag size="small" color={column.foreignKey ? "blue" : "grey"}>{column.dataType}</Tag>
            </div>
          ))}
        </button>
      ))}
    </div>
  );
}

function RelationLine({
  relation,
  tableByName
}: {
  relation: DatabaseCenterRelationSummary;
  tableByName: Map<string, PositionedTable>;
}) {
  const from = tableByName.get(relation.fromTable);
  const to = tableByName.get(relation.toTable);
  if (!from || !to) return null;

  const x1 = from.x + 210;
  const y1 = from.y + 44;
  const x2 = to.x;
  const y2 = to.y + 44;
  const length = Math.hypot(x2 - x1, y2 - y1);
  const angle = Math.atan2(y2 - y1, x2 - x1) * 180 / Math.PI;

  return (
    <div
      className="database-center-er__relation"
      title={`${relation.fromTable}.${relation.fromColumn} -> ${relation.toTable}.${relation.toColumn}`}
      style={{ left: x1, top: y1, width: length, transform: `rotate(${angle}deg)` }}
    />
  );
}

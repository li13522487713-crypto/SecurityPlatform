import { Button, Empty, Input, Space, Tag, Typography } from "@douyinfe/semi-ui";
import { IconMinus, IconPlus, IconRefresh, IconSearch } from "@douyinfe/semi-icons";
import { useEffect, useState } from "react";
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
  const [keyword, setKeyword] = useState("");
  const [zoom, setZoom] = useState(1);
  const [positions, setPositions] = useState<Record<string, { x: number; y: number }>>({});
  const [dragging, setDragging] = useState<{ id: string; startX: number; startY: number; originX: number; originY: number } | null>(null);
  const tables = (structure?.objects ?? []).filter(item => item.objectType === "table");
  const normalizedKeyword = keyword.trim().toLowerCase();
  const visibleTables = tables.filter(table => {
    if (!normalizedKeyword || !structure) return true;
    const columns = structure.columnsByObject[table.id] ?? structure.columnsByObject[table.name] ?? [];
    return table.name.toLowerCase().includes(normalizedKeyword)
      || columns.some(column => column.name.toLowerCase().includes(normalizedKeyword));
  });

  useEffect(() => {
    setPositions(current => {
      const next = { ...current };
      tables.forEach((object, index) => {
        if (!next[object.id]) {
          next[object.id] = { x: 28 + (index % 3) * 280, y: 28 + Math.floor(index / 3) * 240 };
        }
      });
      return next;
    });
  }, [tables.map(item => item.id).join("|")]);

  if (!structure || tables.length === 0) {
    return <Empty description={labels.emptyStructure} />;
  }

  const positioned: PositionedTable[] = visibleTables.map((object, index) => ({
    object,
    columns: structure.columnsByObject[object.id] ?? structure.columnsByObject[object.name] ?? [],
    x: positions[object.id]?.x ?? 28 + (index % 3) * 280,
    y: positions[object.id]?.y ?? 28 + Math.floor(index / 3) * 240
  }));
  const tableByName = new Map(positioned.map(item => [item.object.name, item]));
  const height = Math.max(520, 280 + Math.ceil(positioned.length / 3) * 240);

  return (
    <div className="database-center-er-wrap">
      <div className="database-center-er-toolbar">
        <Space>
          <Button size="small" icon={<IconRefresh />} onClick={autoLayout}>{labels.autoLayout}</Button>
          <Button size="small" icon={<IconMinus />} onClick={() => setZoom(value => Math.max(.6, Math.round((value - .1) * 10) / 10))}>{labels.zoomOut}</Button>
          <Tag>{Math.round(zoom * 100)}%</Tag>
          <Button size="small" icon={<IconPlus />} onClick={() => setZoom(value => Math.min(1.6, Math.round((value + .1) * 10) / 10))}>{labels.zoomIn}</Button>
        </Space>
        <Input prefix={<IconSearch />} placeholder={labels.searchTablesFields} value={keyword} onChange={setKeyword} style={{ width: 260 }} />
      </div>
      <div
        className="database-center-er"
        style={{ height }}
        onMouseMove={event => {
          if (!dragging) return;
          const dx = (event.clientX - dragging.startX) / zoom;
          const dy = (event.clientY - dragging.startY) / zoom;
          setPositions(current => ({ ...current, [dragging.id]: { x: Math.max(8, dragging.originX + dx), y: Math.max(8, dragging.originY + dy) } }));
        }}
        onMouseUp={() => setDragging(null)}
        onMouseLeave={() => setDragging(null)}
      >
        <div className="database-center-er__stage" style={{ transform: `scale(${zoom})`, transformOrigin: "left top" }}>
          {structure.relations.map(relation => (
            <RelationLine key={relation.id} relation={relation} tableByName={tableByName} />
          ))}
          {positioned.map(table => (
            <button
              key={table.object.id}
              type="button"
              className="database-center-er__node"
              style={{ left: table.x, top: table.y, textAlign: "left" }}
              onMouseDown={event => {
                if (event.button !== 0) return;
                setDragging({ id: table.object.id, startX: event.clientX, startY: event.clientY, originX: table.x, originY: table.y });
              }}
              onClick={() => onSelectObject(table.object)}
            >
              <div className="database-center-er__node-header">
                <Text strong ellipsis={{ showTooltip: true }}>{table.object.name}</Text>
                <Tag size="small">{table.columns.length}</Tag>
              </div>
              {table.columns.slice(0, 9).map(column => (
                <div key={column.name} className="database-center-er__column">
                  <span>{column.primaryKey ? "PK " : ""}{(column as DatabaseCenterColumnSummary & { autoIncrement?: boolean }).autoIncrement ? "AI " : ""}{column.foreignKey ? "FK " : ""}{column.name}</span>
                  <Tag size="small" color={column.foreignKey ? "blue" : column.primaryKey ? "violet" : "grey"}>
                    {column.dataType}{column.nullable ? "" : " NN"}
                  </Tag>
                </div>
              ))}
            </button>
          ))}
        </div>
        <div className="database-center-er-minimap">
          {positioned.map(table => (
            <span key={table.object.id} style={{ left: table.x / 8, top: table.y / 8 }} />
          ))}
        </div>
      </div>
    </div>
  );

  function autoLayout() {
    const next: Record<string, { x: number; y: number }> = {};
    tables.forEach((object, index) => {
      next[object.id] = { x: 28 + (index % 3) * 280, y: 28 + Math.floor(index / 3) * 240 };
    });
    setPositions(next);
  }
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

  const x1 = from.x + 230;
  const y1 = from.y + 54;
  const x2 = to.x;
  const y2 = to.y + 54;
  const length = Math.hypot(x2 - x1, y2 - y1);
  const angle = Math.atan2(y2 - y1, x2 - x1) * 180 / Math.PI;
  const midX = x1 + (x2 - x1) / 2;
  const midY = y1 + (y2 - y1) / 2;

  return (
    <>
      <div
        className="database-center-er__relation"
        title={`${relation.fromTable}.${relation.fromColumn} -> ${relation.toTable}.${relation.toColumn}`}
        style={{ left: x1, top: y1, width: length, transform: `rotate(${angle}deg)` }}
      />
      <span className="database-center-er__relation-label" style={{ left: midX, top: midY }}>1 / N</span>
    </>
  );
}

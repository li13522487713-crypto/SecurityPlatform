import { useEffect, useMemo, useRef, useState } from "react";
import { Input, Typography } from "@douyinfe/semi-ui";
import { IconSearch } from "@douyinfe/semi-icons";
import type { MicroflowWorkflowJSON, MicroflowWorkflowNodeJSON } from "../schema/types";
import { NodeIcon } from "./NodeIcon";

const { Text } = Typography;

interface SpotlightNode {
  id: string;
  title: string;
  kind: string;
  collectionId?: string;
}

function collectNodes(workflow: MicroflowWorkflowJSON, out: SpotlightNode[] = []): SpotlightNode[] {
  for (const node of workflow.nodes ?? []) {
    const data = (node as MicroflowWorkflowNodeJSON).data as Record<string, unknown> | undefined;
    const title = String(data?.title ?? data?.caption ?? node.type ?? "");
    const kind = String(data?.objectKind ?? node.type ?? "");
    out.push({ id: String(node.id), title, kind });
    const blocks = (node as Record<string, unknown>).blocks as Array<{ nodes?: unknown[] }> | undefined;
    if (blocks) {
      for (const block of blocks) {
        if (block.nodes) {
          collectNodes({ nodes: block.nodes as MicroflowWorkflowNodeJSON[], edges: [] } as MicroflowWorkflowJSON, out);
        }
      }
    }
  }
  return out;
}

export interface MicroflowNodeSpotlightProps {
  workflow: MicroflowWorkflowJSON;
  onNavigate: (objectId: string) => void;
}

export function MicroflowNodeSpotlight({ workflow, onNavigate }: MicroflowNodeSpotlightProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [activeIndex, setActiveIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement | null>(null);

  const allNodes = useMemo(() => collectNodes(workflow), [workflow]);
  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return allNodes.slice(0, 12);
    return allNodes
      .filter(n =>
        n.title.toLowerCase().includes(q) ||
        n.kind.toLowerCase().includes(q) ||
        n.id.toLowerCase().includes(q),
      )
      .slice(0, 12);
  }, [allNodes, query]);

  useEffect(() => {
    setActiveIndex(0);
  }, [filtered.length]);

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setOpen(v => !v);
        setQuery("");
      }
      if (!open) return;
      if (e.key === "Escape") {
        setOpen(false);
      }
      if (e.key === "ArrowDown") {
        e.preventDefault();
        setActiveIndex(i => Math.min(i + 1, filtered.length - 1));
      }
      if (e.key === "ArrowUp") {
        e.preventDefault();
        setActiveIndex(i => Math.max(i - 1, 0));
      }
      if (e.key === "Enter" && filtered[activeIndex]) {
        e.preventDefault();
        onNavigate(filtered[activeIndex].id);
        setOpen(false);
      }
    };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [open, filtered, activeIndex, onNavigate]);

  useEffect(() => {
    if (open) {
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  }, [open]);

  if (!open) return null;

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 9999,
        display: "grid",
        placeItems: "start center",
        paddingTop: "20vh",
        background: "rgba(0,0,0,0.45)",
        backdropFilter: "blur(2px)",
      }}
      onMouseDown={e => {
        if (e.target === e.currentTarget) setOpen(false);
      }}
    >
      <div style={{
        width: 480,
        maxWidth: "92vw",
        borderRadius: 12,
        background: "#16213a",
        border: "1px solid rgba(255,255,255,0.1)",
        boxShadow: "0 24px 48px rgba(0,0,0,0.6)",
        overflow: "hidden",
      }}>
        <div style={{ padding: "10px 12px", borderBottom: "1px solid rgba(255,255,255,0.08)" }}>
          <Input
            ref={inputRef as React.RefObject<HTMLInputElement>}
            prefix={<IconSearch />}
            placeholder="搜索画布节点… (Ctrl+K 关闭)"
            value={query}
            onChange={setQuery}
            size="large"
            style={{ background: "transparent", border: "none", boxShadow: "none" }}
          />
        </div>
        <div style={{ maxHeight: 360, overflowY: "auto" }}>
          {filtered.length === 0 ? (
            <div style={{ padding: "16px 14px" }}>
              <Text type="tertiary" size="small">未找到匹配节点</Text>
            </div>
          ) : filtered.map((node, index) => (
            <button
              key={node.id}
              type="button"
              style={{
                display: "flex",
                alignItems: "center",
                gap: 10,
                width: "100%",
                padding: "8px 14px",
                background: index === activeIndex ? "rgba(255,255,255,0.07)" : "transparent",
                border: "none",
                borderRadius: 0,
                cursor: "pointer",
                textAlign: "left",
              }}
              onMouseEnter={() => setActiveIndex(index)}
              onClick={() => {
                onNavigate(node.id);
                setOpen(false);
              }}
            >
              <span style={{
                width: 28,
                height: 28,
                borderRadius: 7,
                display: "inline-flex",
                alignItems: "center",
                justifyContent: "center",
                background: "rgba(74, 158, 255, 0.12)",
                color: "#4a9eff",
                border: "1px solid rgba(74,158,255,0.22)",
                flexShrink: 0,
              }}>
                <NodeIcon kind={node.kind} size={15} />
              </span>
              <div style={{ minWidth: 0, flex: 1 }}>
                <div style={{ color: "#e2e8f0", fontSize: 14, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                  {node.title || node.kind}
                </div>
                <div style={{ color: "#64748b", fontSize: 11, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                  {node.kind}
                </div>
              </div>
              {index === activeIndex ? (
                <span style={{ color: "#64748b", fontSize: 11, flexShrink: 0 }}>↵</span>
              ) : null}
            </button>
          ))}
        </div>
        <div style={{ padding: "6px 14px", borderTop: "1px solid rgba(255,255,255,0.06)", display: "flex", gap: 12 }}>
          <Text type="tertiary" size="small">↑↓ 导航</Text>
          <Text type="tertiary" size="small">↵ 跳转</Text>
          <Text type="tertiary" size="small">Esc 关闭</Text>
        </div>
      </div>
    </div>
  );
}

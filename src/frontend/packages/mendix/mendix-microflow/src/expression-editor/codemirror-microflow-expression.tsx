import { defaultKeymap, history, historyKeymap } from "@codemirror/commands";
import { Compartment, EditorState } from "@codemirror/state";
import { EditorView, keymap, lineNumbers } from "@codemirror/view";
import { useEffect, useRef } from "react";

export interface CodemirrorMicroflowExpressionProps {
  value: string;
  onChange: (next: string) => void;
  readonly?: boolean;
  minRows?: number;
}

/** CodeMirror 6 宿主：纯文本编辑 + 历史/换行；语法着色由后续 StreamLanguage 迭代补齐。 */
export default function CodemirrorMicroflowExpression({
  value,
  onChange,
  readonly,
  minRows = 2,
}: CodemirrorMicroflowExpressionProps) {
  const parentRef = useRef<HTMLDivElement>(null);
  const viewRef = useRef<EditorView | null>(null);
  const readOnlyConf = useRef(new Compartment());
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;

  useEffect(() => {
    const parent = parentRef.current;
    if (!parent) {
      return;
    }

    const state = EditorState.create({
      doc: value,
      extensions: [
        lineNumbers(),
        history(),
        keymap.of([...defaultKeymap, ...historyKeymap]),
        EditorView.lineWrapping,
        readOnlyConf.current.of(EditorState.readOnly.of(!!readonly)),
        EditorView.updateListener.of(update => {
          if (update.docChanged) {
            onChangeRef.current(update.state.doc.toString());
          }
        }),
        EditorView.theme({
          "&": { minHeight: `${Math.max(2, minRows) * 22}px` },
          ".cm-scroller": { fontFamily: "inherit", fontSize: "13px" },
          ".cm-content": { paddingBlock: "6px" },
        }),
      ],
    });

    const view = new EditorView({ state, parent });
    viewRef.current = view;
    return () => {
      view.destroy();
      viewRef.current = null;
    };
     
  }, [minRows]);

  useEffect(() => {
    const view = viewRef.current;
    if (!view) {
      return;
    }
    const cur = view.state.doc.toString();
    if (cur !== value) {
      view.dispatch({ changes: { from: 0, to: cur.length, insert: value } });
    }
  }, [value]);

  useEffect(() => {
    const view = viewRef.current;
    if (!view) {
      return;
    }
    view.dispatch({
      effects: readOnlyConf.current.reconfigure(EditorState.readOnly.of(!!readonly)),
    });
  }, [readonly]);

  return <div ref={parentRef} className="microflow-expression-codemirror-host" />;
}

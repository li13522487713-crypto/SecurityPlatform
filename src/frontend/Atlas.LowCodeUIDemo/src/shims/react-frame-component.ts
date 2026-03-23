import React, {
  createContext,
  forwardRef,
  useCallback,
  useContext,
  useEffect,
  useImperativeHandle,
  useMemo,
  useRef,
  useState,
} from "react";
import { createPortal } from "react-dom";

export interface FrameContextProps {
  document?: Document;
  window?: Window;
}

export const FrameContext = createContext<FrameContextProps>({
  document: typeof document !== "undefined" ? document : undefined,
  window: typeof window !== "undefined" ? window : undefined,
});

export const FrameContextProvider = FrameContext.Provider;
export const FrameContextConsumer = FrameContext.Consumer;

export function useFrame(): FrameContextProps {
  return useContext(FrameContext);
}

export interface FrameComponentProps extends React.IframeHTMLAttributes<HTMLIFrameElement> {
  head?: React.ReactNode;
  mountTarget?: string;
  initialContent?: string;
  contentDidMount?: () => void;
  contentDidUpdate?: () => void;
  dangerouslyUseDocWrite?: boolean;
  children: React.ReactNode;
}

const DEFAULT_INITIAL_CONTENT = "<!DOCTYPE html><html><head></head><body><div></div></body></html>";

const FrameComponent = forwardRef<HTMLIFrameElement, FrameComponentProps>(function FrameComponent(props, ref) {
  const {
    head,
    mountTarget,
    initialContent = DEFAULT_INITIAL_CONTENT,
    contentDidMount,
    contentDidUpdate,
    dangerouslyUseDocWrite = false,
    children,
    onLoad,
    ...iframeProps
  } = props;

  const iframeRef = useRef<HTMLIFrameElement | null>(null);
  const [readyDoc, setReadyDoc] = useState<Document | null>(null);
  const didMountRef = useRef(false);

  useImperativeHandle(ref, () => iframeRef.current as HTMLIFrameElement | null, []);

  const resolveDocument = useCallback((): Document | null => {
    const current = iframeRef.current;
    return current?.contentDocument ?? null;
  }, []);

  const markReady = useCallback(() => {
    const doc = resolveDocument();
    if (!doc) {
      return;
    }

    if (dangerouslyUseDocWrite && doc.body.children.length < 1) {
      doc.open("text/html", "replace");
      doc.write(initialContent);
      doc.close();
    }

    setReadyDoc(doc);
  }, [dangerouslyUseDocWrite, initialContent, resolveDocument]);

  useEffect(() => {
    markReady();
  }, [markReady]);

  useEffect(() => {
    if (!readyDoc) {
      return;
    }

    if (!didMountRef.current) {
      didMountRef.current = true;
      contentDidMount?.();
      return;
    }

    contentDidUpdate?.();
  }, [children, contentDidMount, contentDidUpdate, readyDoc]);

  const handleLoad: React.ReactEventHandler<HTMLIFrameElement> = (event) => {
    markReady();
    onLoad?.(event);
  };

  const contextValue = useMemo<FrameContextProps>(() => {
    if (!readyDoc) {
      return {};
    }

    return {
      document: readyDoc,
      window: readyDoc.defaultView ?? undefined,
    };
  }, [readyDoc]);

  const portalTarget = useMemo(() => {
    if (!readyDoc) {
      return null;
    }

    if (mountTarget) {
      return readyDoc.querySelector(mountTarget);
    }

    return readyDoc.body.children[0] ?? readyDoc.body;
  }, [mountTarget, readyDoc]);

  const iframeElement = React.createElement("iframe", {
    ...iframeProps,
    ref: iframeRef,
    onLoad: handleLoad,
    srcDoc: dangerouslyUseDocWrite ? iframeProps.srcDoc : (iframeProps.srcDoc ?? initialContent),
  });

  if (!readyDoc || !portalTarget) {
    return iframeElement;
  }

  const bodyPortal = createPortal(
    React.createElement(
      FrameContextProvider,
      { value: contextValue },
      React.createElement("div", { className: "frame-content" }, children),
    ),
    portalTarget,
  );

  const headPortal = head ? createPortal(head, readyDoc.head) : null;

  return React.createElement(React.Fragment, null, iframeElement, headPortal, bodyPortal);
});

export default FrameComponent;

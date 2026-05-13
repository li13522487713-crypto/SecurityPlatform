/** Semantic SVG icons for microflow node kinds (16×16 viewBox). */
export function NodeIcon({ kind, size }: { kind: string; size?: number }) {
  const dim = size ?? 14;
  const base = {
    width: dim,
    height: dim,
    viewBox: "0 0 16 16",
    fill: "currentColor",
    "aria-hidden": true as const,
    style: { display: "block" },
  };
  switch (kind) {
    case "startEvent":
      return <svg {...base}><polygon points="3,2 14,8 3,14" /></svg>;
    case "endEvent":
      return <svg {...base}><rect x="2" y="2" width="12" height="12" rx="1" /></svg>;
    case "errorEvent":
      return <svg {...base}><path d="M3 3l10 10M13 3L3 13" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" fill="none" /></svg>;
    case "continueEvent":
      return <svg {...base}><path d="M3 8h8M8 4l4 4-4 4" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" fill="none" /></svg>;
    case "breakEvent":
      return <svg {...base}><rect x="4" y="4" width="8" height="8" rx="1.3" /></svg>;
    case "parameterObject":
      return <svg {...base}><path d="M5 12V4h4.1a2.9 2.9 0 0 1 0 5.8H5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" fill="none" /></svg>;
    case "exclusiveSplit":
      return <svg {...base}><path d="M4 3v4M12 3v4M8 5v6M4 7h8M8 11l-3 2M8 11l3 2" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" fill="none" /></svg>;
    case "inheritanceSplit":
      return <svg {...base}><path d="M8 2v3M4 7h8M4 7v5M12 7v5" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" fill="none" /></svg>;
    case "exclusiveMerge":
    case "mergeActivity":
      return <svg {...base}><path d="M2 4l6 4-6 4M14 4l-6 4 6 4" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" fill="none" /></svg>;
    case "loopedActivity":
      return <svg {...base}><path d="M8 2a6 6 0 1 1-4.24 1.76M8 2V6M8 2L5 5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" fill="none" /></svg>;
    case "annotation":
      return <svg {...base}><rect x="2" y="1" width="10" height="14" rx="1" fill="none" stroke="currentColor" strokeWidth="1.5" /><line x1="5" y1="5" x2="9" y2="5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" /><line x1="5" y1="8" x2="9" y2="8" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" /><line x1="5" y1="11" x2="7" y2="11" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" /></svg>;
    case "httpRequest":
      return <svg {...base}><circle cx="8" cy="8" r="6" fill="none" stroke="currentColor" strokeWidth="1.5" /><ellipse cx="8" cy="8" rx="2.5" ry="6" fill="none" stroke="currentColor" strokeWidth="1.2" /><line x1="2" y1="8" x2="14" y2="8" stroke="currentColor" strokeWidth="1.2" /></svg>;
    case "javaAction":
    case "microflowCall":
    case "nanoflowCall":
    case "callMicroflow":
    case "callNanoflow":
      return <svg {...base}><polygon points="9,1 3,9 8,9 7,15 13,7 8,7" /></svg>;
    case "parallelSplit":
    case "parallelMerge":
    case "parallelGateway":
    case "inclusiveGateway":
      return <svg {...base}><rect x="3" y="2" width="3" height="12" rx="1" /><rect x="10" y="2" width="3" height="12" rx="1" /></svg>;
    case "tryCatch":
      return <svg {...base}><path d="M8 1L2 4v4c0 3 2.7 5.7 6 7 3.3-1.3 6-4 6-7V4L8 1z" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round" /></svg>;
    case "createObject":
    case "objectCreate":
      return <svg {...base}><rect x="2" y="4" width="12" height="9" rx="1.5" fill="none" stroke="currentColor" strokeWidth="1.5" /><path d="M6 4V3a2 2 0 0 1 4 0v1" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" fill="none" /><path d="M8 8v3M6.5 9.5h3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" /></svg>;
    case "changeObject":
    case "objectChange":
      return <svg {...base}><rect x="2" y="4" width="12" height="9" rx="1.5" fill="none" stroke="currentColor" strokeWidth="1.5" /><path d="M6 4V3a2 2 0 0 1 4 0v1" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" fill="none" /><path d="M5 9h6M5 11.5h4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" /></svg>;
    case "retrieveObject":
    case "objectRetrieve":
      return <svg {...base}><circle cx="8" cy="8" r="5" fill="none" stroke="currentColor" strokeWidth="1.5" /><circle cx="8" cy="8" r="2" /><path d="M11 11l2.5 2.5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" fill="none" /></svg>;
    case "deleteObject":
    case "objectDelete":
      return <svg {...base}><path d="M3 5h10M6 5V3h4v2M6 8v5M10 8v5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" fill="none" /><rect x="4" y="5" width="8" height="9" rx="1" fill="none" stroke="currentColor" strokeWidth="1.5" /></svg>;
    case "commitObject":
    case "objectCommit":
      return <svg {...base}><path d="M2 8l4 4 8-8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" fill="none" /></svg>;
    case "createVariable":
    case "variableCreate":
      return <svg {...base}><text x="2" y="12" fontSize="11" fontWeight="700" fontFamily="serif" fill="currentColor">x+</text></svg>;
    case "changeVariable":
    case "variableChange":
      return <svg {...base}><text x="2" y="12" fontSize="11" fontWeight="700" fontFamily="serif" fill="currentColor">x=</text></svg>;
    case "filterList":
    case "listFilter":
      return <svg {...base}><path d="M2 4h12M4 8h8M7 12h2" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" fill="none" /></svg>;
    case "sortList":
    case "listSort":
      return <svg {...base}><path d="M4 4l3 4-3 4M8 12h6M8 8h5M8 4h4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" fill="none" /></svg>;
    case "aggregateList":
    case "listAggregate":
      return <svg {...base}><path d="M2 12V8l2-4h8l2 4v4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" fill="none" /><path d="M6 8h4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" /></svg>;
    case "callRest":
    case "restCall":
      return <svg {...base}><path d="M2 5h5M9 5h5M5 5V3M11 5V3M7 11h2M5 11H3M11 11h2" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" fill="none" /><rect x="4" y="7" width="8" height="4" rx="1" fill="none" stroke="currentColor" strokeWidth="1.5" /></svg>;
    case "logMessage":
      return <svg {...base}><rect x="2" y="3" width="12" height="8" rx="1.5" fill="none" stroke="currentColor" strokeWidth="1.5" /><path d="M5 7h6M5 9h3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" /><path d="M5 11l2 2h-1" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" fill="none" /></svg>;
    default:
      return (
        <svg {...base}>
          <circle cx="8" cy="8" r="2.5" fill="none" stroke="currentColor" strokeWidth="1.5" />
          <path d="M8 1v2M8 13v2M1 8h2M13 8h2M3.1 3.1l1.4 1.4M11.5 11.5l1.4 1.4M3.1 12.9l1.4-1.4M11.5 4.5l1.4-1.4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        </svg>
      );
  }
}

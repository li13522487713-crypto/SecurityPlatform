interface BezierLineProps {
  d: string;
  processing?: boolean;
}

export function BezierLine(props: BezierLineProps) {
  return <path d={props.d} className={`wf-flowgram-bezier-line${props.processing ? " wf-flowgram-bezier-line-processing" : ""}`} />;
}

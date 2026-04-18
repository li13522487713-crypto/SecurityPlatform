import type { ConnectorProviderType, ExternalMessageCard } from '../types';

export type MessageCardPreviewLabelsKey =
  | 'viewDetails'
  | 'providerWeCom'
  | 'providerFeishu'
  | 'providerDingTalk'
  | 'providerCustomOidc';

export type MessageCardPreviewLabels = Record<MessageCardPreviewLabelsKey, string>;

export const MESSAGE_CARD_PREVIEW_LABELS_KEYS = [
  'viewDetails',
  'providerWeCom',
  'providerFeishu',
  'providerDingTalk',
  'providerCustomOidc',
] as const satisfies readonly MessageCardPreviewLabelsKey[];

export const defaultMessageCardPreviewLabels: MessageCardPreviewLabels = {
  viewDetails: 'View details',
  providerWeCom: 'WeCom · template_card',
  providerFeishu: 'Feishu · interactive',
  providerDingTalk: 'DingTalk · action_card',
  providerCustomOidc: 'Custom OIDC',
};

export interface MessageCardPreviewProps {
  providerType: ConnectorProviderType;
  card: ExternalMessageCard;
  /** Defaults to 360 px so the preview matches WeCom / Feishu mobile card width. */
  maxWidth?: number;
  labels: MessageCardPreviewLabels;
}

/**
 * Cross-vendor message-card preview. Intentionally rendered with raw HTML so each
 * provider visual stays close to its real IM card; do NOT migrate to Semi.
 */
export function MessageCardPreview({ providerType, card, maxWidth = 360, labels }: MessageCardPreviewProps) {
  const palette = getPalette(providerType);
  const tone = card.tone ?? 'info';
  const accentColor = palette.toneColors[tone] ?? palette.toneColors.info;

  return (
    <article
      data-testid={`message-card-preview-${providerType.toLowerCase()}`}
      style={{
        maxWidth,
        background: palette.background,
        border: `1px solid ${palette.border}`,
        borderRadius: 8,
        padding: 12,
        fontFamily: 'system-ui, -apple-system, sans-serif',
        color: palette.text,
        boxShadow: '0 1px 3px rgba(0,0,0,0.06)',
      }}
    >
      <header style={{ borderLeft: `3px solid ${accentColor}`, paddingLeft: 8, marginBottom: 8 }}>
        <strong style={{ fontSize: 16, display: 'block' }}>{card.title}</strong>
        {card.subtitle && <span style={{ color: palette.subtitleText, fontSize: 13 }}>{card.subtitle}</span>}
      </header>

      {card.content && (
        <p style={{ margin: '8px 0', whiteSpace: 'pre-wrap', lineHeight: 1.55 }}>{card.content}</p>
      )}

      {card.fields && card.fields.length > 0 && (
        <dl style={{ display: 'grid', gridTemplateColumns: 'auto 1fr', columnGap: 12, rowGap: 4, fontSize: 13, margin: 0 }}>
          {card.fields.map((f, idx) => (
            <DataRow key={idx} label={f.key} value={f.value} labelColor={palette.labelText} />
          ))}
        </dl>
      )}

      {(card.jumpUrl || (card.actions && card.actions.length > 0)) && (
        <footer style={{ marginTop: 12, paddingTop: 8, borderTop: `1px solid ${palette.border}`, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          {card.actions && card.actions.length > 0
            ? card.actions.map((a, idx) => (
                <a key={idx} href={a.url ?? card.jumpUrl ?? '#'} style={{ color: accentColor, textDecoration: 'none', fontSize: 13 }} target="_blank" rel="noopener">
                  {a.text}
                </a>
              ))
            : (
              <a href={card.jumpUrl} style={{ color: accentColor, textDecoration: 'none', fontSize: 13 }} target="_blank" rel="noopener">
                {labels.viewDetails}
              </a>
            )}
        </footer>
      )}

      <ProviderBadge providerType={providerType} palette={palette} cardVersion={card.cardVersion} labels={labels} />
    </article>
  );
}

function DataRow({ label, value, labelColor }: { label: string; value: string; labelColor: string }) {
  return (
    <>
      <dt style={{ color: labelColor }}>{label}</dt>
      <dd style={{ margin: 0 }}>{value}</dd>
    </>
  );
}

function ProviderBadge({
  providerType,
  palette,
  cardVersion,
  labels,
}: {
  providerType: ConnectorProviderType;
  palette: Palette;
  cardVersion?: number;
  labels: MessageCardPreviewLabels;
}) {
  return (
    <div
      style={{
        marginTop: 8,
        fontSize: 11,
        color: palette.labelText,
        display: 'flex',
        justifyContent: 'space-between',
      }}
    >
      <span>{getProviderLabel(providerType, labels)}</span>
      {cardVersion !== undefined && <span>v{cardVersion}</span>}
    </div>
  );
}

interface Palette {
  background: string;
  border: string;
  text: string;
  subtitleText: string;
  labelText: string;
  toneColors: Record<string, string>;
}

function getPalette(providerType: ConnectorProviderType): Palette {
  switch (providerType) {
    case 'WeCom':
      return {
        background: '#ffffff',
        border: '#e6e8eb',
        text: '#222222',
        subtitleText: '#888888',
        labelText: '#999999',
        toneColors: { info: '#1aad19', success: '#1aad19', warning: '#e6a23c', danger: '#f56c6c' },
      };
    case 'Feishu':
      return {
        background: '#ffffff',
        border: '#e6e7eb',
        text: '#1f2329',
        subtitleText: '#646a73',
        labelText: '#8f959e',
        toneColors: { info: '#3370ff', success: '#34c724', warning: '#ff8800', danger: '#f54a45' },
      };
    case 'DingTalk':
      return {
        background: '#ffffff',
        border: '#e0e3e7',
        text: '#1d2129',
        subtitleText: '#86909c',
        labelText: '#a4acb8',
        toneColors: { info: '#1677ff', success: '#52c41a', warning: '#faad14', danger: '#ff4d4f' },
      };
    default:
      return {
        background: '#fafafa',
        border: '#e8e8e8',
        text: '#333',
        subtitleText: '#999',
        labelText: '#aaa',
        toneColors: { info: '#0a66c2', success: '#52c41a', warning: '#faad14', danger: '#ff4d4f' },
      };
  }
}

function getProviderLabel(providerType: ConnectorProviderType, labels: MessageCardPreviewLabels): string {
  switch (providerType) {
    case 'WeCom':
      return labels.providerWeCom;
    case 'Feishu':
      return labels.providerFeishu;
    case 'DingTalk':
      return labels.providerDingTalk;
    case 'CustomOidc':
      return labels.providerCustomOidc;
    default:
      return providerType;
  }
}

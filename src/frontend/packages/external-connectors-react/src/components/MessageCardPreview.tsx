import type { ConnectorProviderType, ExternalMessageCard } from '../types';

export interface MessageCardPreviewProps {
  /** wecom / feishu / dingtalk / customOidc：决定渲染样式与配色。 */
  providerType: ConnectorProviderType;
  card: ExternalMessageCard;
  /** 自定义最大宽度（默认 360px，对齐企微 / 飞书移动端卡片宽度）。 */
  maxWidth?: number;
}

/**
 * 跨厂商消息卡片预览：按 provider 渲染近似真实卡片样式（企微 template_card / 飞书 interactive / 钉钉 action_card）。
 * 设计目标：让管理员在配置阶段就能"所见即所得"，避免发到生产才发现样式偏差。
 */
export function MessageCardPreview({ providerType, card, maxWidth = 360 }: MessageCardPreviewProps) {
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
                查看详情 →
              </a>
            )}
        </footer>
      )}

      <ProviderBadge providerType={providerType} palette={palette} cardVersion={card.cardVersion} />
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

function ProviderBadge({ providerType, palette, cardVersion }: { providerType: ConnectorProviderType; palette: Palette; cardVersion?: number }) {
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
      <span>{getProviderLabel(providerType)}</span>
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

function getProviderLabel(providerType: ConnectorProviderType): string {
  switch (providerType) {
    case 'WeCom':
      return '企业微信 · template_card';
    case 'Feishu':
      return '飞书 · interactive';
    case 'DingTalk':
      return '钉钉 · action_card';
    default:
      return providerType;
  }
}

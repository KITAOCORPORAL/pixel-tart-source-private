export type ApiMode = 'Mock' | 'LocalDev';
export type ApiConfig = { mode: ApiMode; baseUrl: string; publicId: string; devAccessToken: string; delayMs?: 0 | 300 | 2000; randomFailure?: boolean };
export type ApiError = { code: string; message: string; retryable: boolean; conflict: boolean; currentSelectionVersion?: number; currentRevision?: number };
export type ApiResult<T> = { ok: true; data: T } | { ok: false; error: ApiError };
export type MutationResult = { selectionVersion: number; revision: number; isLocked: boolean };
export type MutationBody = { expectedSelectionVersion: number; expectedRevision: number; operationId: string };

const config: ApiConfig = { mode: 'Mock', baseUrl: '', publicId: '', devAccessToken: '' };
const humanMessages: Record<string, string> = {
  ProviderNone: '当前是本地演示模式，尚未连接 LocalDev 服务。',
  SelectionConflict: '另一台设备刚刚更新了选片，请刷新后重试。',
  SelectionLocked: '选片结果已提交，如需修改请联系摄影师重新开放。',
  InvalidToken: '本地预览凭证已失效，请从 Desktop 重新打开预览。',
  NetworkError: '网络暂时不可用，刚才的操作仍保留在本机。'
};

function mapError(statusCode: number, body: any): ApiError {
  const code = body?.code || `Http${statusCode}`;
  return {
    code,
    message: humanMessages[code] || body?.message || '服务暂时不可用，请稍后重试。',
    retryable: statusCode >= 500 || statusCode === 408 || statusCode === 429,
    conflict: statusCode === 409 && code === 'SelectionConflict',
    currentSelectionVersion: body?.currentSelectionVersion,
    currentRevision: body?.currentRevision
  };
}

/**
 * Single network boundary. wx.login only yields a temporary code; code2Session,
 * AppSecret and session_key belong on a server. This LocalDev adapter stores no
 * production credential and only talks to a loopback WeChat DevTools preview.
 */
export const api = {
  configure(next: ApiConfig) { Object.assign(config, next); },
  get mode() { return config.mode; },
  get currentPublicId() { return config.publicId; },
  get endpoint() { return config.baseUrl; },
  async request<T>(path: string, method: 'GET' | 'PUT' | 'POST' = 'GET', data?: unknown): Promise<ApiResult<T>> {
    if (config.mode !== 'LocalDev' || !config.baseUrl || !config.devAccessToken)
      return { ok: false, error: { code: 'ProviderNone', message: humanMessages.ProviderNone, retryable: false, conflict: false } };
    return new Promise(resolve => {
      wx.request({
        url: `${config.baseUrl}${path}`,
        method,
        data,
        timeout: 10000,
        header: {
          'X-PixelTart-Dev-Token': config.devAccessToken,
          'X-PixelTart-Dev-Delay': config.delayMs ? String(config.delayMs) : '0',
          'X-PixelTart-Dev-Random-Failure': config.randomFailure ? '1' : '0'
        },
        success: response => response.statusCode >= 200 && response.statusCode < 300
          ? resolve({ ok: true, data: response.data as T })
          : resolve({ ok: false, error: mapError(response.statusCode, response.data) }),
        fail: () => resolve({ ok: false, error: { code: 'NetworkError', message: humanMessages.NetworkError, retryable: true, conflict: false } })
      });
    });
  },
  project() { return this.request(`/v1/client/selection/${encodeURIComponent(config.publicId)}`); },
  assets(cursor = '', limit = 50) { return this.request(`/v1/client/selection/${encodeURIComponent(config.publicId)}/assets?limit=${limit}${cursor ? `&cursor=${encodeURIComponent(cursor)}` : ''}`); },
  choice(assetId: string, selected: boolean, favorite: boolean, base: MutationBody) {
    return this.request<MutationResult>(`/v1/client/selection/${encodeURIComponent(config.publicId)}/choices/${assetId}`, 'PUT', { selected, favorite, ...base });
  },
  favorite(assetId: string, selected: boolean, favorite: boolean, base: MutationBody) {
    return this.request<MutationResult>(`/v1/client/selection/${encodeURIComponent(config.publicId)}/favorites/${assetId}`, 'PUT', { selected, favorite, ...base });
  },
  comment(assetId: string, customerNote: string, base: MutationBody) {
    return this.request<MutationResult>(`/v1/client/selection/${encodeURIComponent(config.publicId)}/comments/${assetId}`, 'PUT', { customerNote, ...base });
  },
  confirm(expectedSelectionVersion: number, expectedRevision: number, confirmationNonce: string) {
    return this.request<any>(`/v1/client/selection/${encodeURIComponent(config.publicId)}/confirm`, 'POST', { confirmed: true, expectedSelectionVersion, expectedRevision, confirmationNonce });
  },
  mediaSession() { return this.request<{ token: string; expiresAtUtc: string }>(`/v1/client/selection/${encodeURIComponent(config.publicId)}/media-session`, 'POST'); }
};

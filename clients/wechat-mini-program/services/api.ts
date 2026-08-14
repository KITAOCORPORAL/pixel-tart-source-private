export type ApiError = { code: string; message: string; retryable: boolean };
export type ApiResult<T> = { ok: true; data: T } | { ok: false; error: ApiError };

/**
 * Single network boundary. This V1 mock never embeds AppSecret, SessionKey,
 * or a long-lived token. A production adapter must run behind HTTPS and keep
 * code2Session/AppSecret/session_key on the server.
 */
export const api = {
  baseUrl: '',
  async request<T>(path: string, method: 'GET' | 'PUT' | 'POST' = 'GET', data?: unknown): Promise<ApiResult<T>> {
    if (!this.baseUrl) return { ok: false, error: { code: 'ProviderNone', message: 'Local mock: online service is not configured.', retryable: false } };
    return new Promise(resolve => {
      wx.request({
        url: `${this.baseUrl}${path}`,
        method,
        data,
        timeout: 10000,
        success: response => response.statusCode >= 200 && response.statusCode < 300
          ? resolve({ ok: true, data: response.data as T })
          : resolve({ ok: false, error: { code: `Http${response.statusCode}`, message: 'The service is temporarily unavailable.', retryable: response.statusCode >= 500 } }),
        fail: () => resolve({ ok: false, error: { code: 'NetworkError', message: 'Network unavailable; local choices are retained.', retryable: true } })
      });
    });
  },
  project(publicId: string) { return this.request(`/v1/client/selection/${encodeURIComponent(publicId)}`); },
  choice(publicId: string, assetId: string, selected: boolean, favorite: boolean) {
    return this.request(`/v1/client/selection/${encodeURIComponent(publicId)}/choices/${assetId}`, 'PUT', { selected, favorite });
  },
  comment(publicId: string, assetId: string, customerNote: string) {
    return this.request(`/v1/client/selection/${encodeURIComponent(publicId)}/comments/${assetId}`, 'PUT', { customerNote });
  },
  confirm(publicId: string) { return this.request(`/v1/client/selection/${encodeURIComponent(publicId)}/confirm`, 'POST', { confirmed: true }); }
};

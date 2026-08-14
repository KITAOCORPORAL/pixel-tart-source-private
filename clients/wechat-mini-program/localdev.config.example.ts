/** Safe committed default. Use WeChat DevTools local storage for temporary overrides. */
export const localDevConfig = {
  enabled: false,
  baseUrl: 'http://127.0.0.1:5127',
  publicId: '',
  devAccessToken: '',
  delayMs: 0 as 0 | 300 | 2000,
  randomFailure: false
};

import { selectionStore } from './services/selection-store';
import { localDevConfig } from './localdev.config.example';

App({
  globalData: { selectionStore },
  onLaunch() {
    selectionStore.restore();
    const localOverride = wx.getStorageSync('pixel-tart-localdev-config/v1') || {};
    const runtimeConfig = { ...localDevConfig, ...localOverride };
    if (runtimeConfig.enabled) selectionStore.initializeLocalDev(runtimeConfig).catch(() => undefined);
  }
});

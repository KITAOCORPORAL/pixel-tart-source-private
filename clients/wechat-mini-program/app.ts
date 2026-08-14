import { selectionStore } from './services/selection-store';

App({
  globalData: { selectionStore },
  onLaunch() { selectionStore.reset(); }
});

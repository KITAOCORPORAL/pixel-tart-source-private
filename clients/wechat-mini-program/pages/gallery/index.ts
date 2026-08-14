import { selectionStore } from '../../services/selection-store';

Page({
  data: { assets: [] as any[], selectedCount: 0, targetCount: 0, confirmed: false, syncing: false, lastError: '' },
  onShow() { this.refresh(); selectionStore.syncPending().finally(() => this.refresh()); },
  refresh() { this.setData({ assets: selectionStore.assets.map(asset => ({ ...asset, thumbUrl: selectionStore.mediaUrl(asset.thumbUrl), selected: selectionStore.isSelected(asset.selectionAssetId), favorite: selectionStore.isFavorite(asset.selectionAssetId) })), selectedCount: selectionStore.selectedCount(), targetCount: selectionStore.project?.targetCount ?? 0, confirmed: selectionStore.confirmed, syncing: selectionStore.pendingRequests.length > 0, lastError: selectionStore.lastError }); },
  onReachBottom() { selectionStore.loadNextPage().finally(() => this.refresh()); },
  choose(event: WechatMiniprogram.BaseEvent) { selectionStore.toggleChoice(event.currentTarget.dataset.id); this.refresh(); },
  favorite(event: WechatMiniprogram.BaseEvent) { selectionStore.toggleFavorite(event.currentTarget.dataset.id); this.refresh(); },
  openPhoto(event: WechatMiniprogram.BaseEvent) { wx.navigateTo({ url: `/pages/photo/index?id=${event.currentTarget.dataset.id}` }); },
  openSelected() { wx.navigateTo({ url: '/pages/selected/index' }); }
});

import { selectionStore } from '../../services/selection-store';

Page({
  data: { assets: [] as any[], selectedCount: 0, targetCount: 0, confirmed: false },
  onShow() { this.refresh(); },
  refresh() { this.setData({ assets: selectionStore.assets.filter(asset => selectionStore.isSelected(asset.selectionAssetId)).map(asset => ({ ...asset, thumbUrl: selectionStore.mediaUrl(asset.thumbUrl) })), selectedCount: selectionStore.selectedCount(), targetCount: selectionStore.project?.targetCount ?? 0, confirmed: selectionStore.confirmed }); },
  cancel(event: WechatMiniprogram.BaseEvent) { selectionStore.toggleChoice(event.currentTarget.dataset.id); this.refresh(); },
  confirm() { wx.navigateTo({ url: '/pages/confirm/index' }); }
});

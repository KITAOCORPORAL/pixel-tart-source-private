import { selectionStore } from '../../services/selection-store';

Page({
  data: { asset: null as any, selected: false, favorite: false, comment: '', confirmed: false },
  onLoad(query: Record<string, string>) { const found = selectionStore.assets.find(item => item.selectionAssetId === query.id); const asset = found ? { ...found, previewUrl: selectionStore.mediaUrl(found.previewUrl), thumbUrl: selectionStore.mediaUrl(found.thumbUrl) } : undefined; this.setData({ asset, selected: found ? selectionStore.isSelected(found.selectionAssetId) : false, favorite: found ? selectionStore.isFavorite(found.selectionAssetId) : false, comment: found ? selectionStore.comments[found.selectionAssetId] ?? '' : '', confirmed: selectionStore.confirmed }); },
  choose() { if (!this.data.asset) return; selectionStore.toggleChoice(this.data.asset.selectionAssetId); this.setData({ selected: selectionStore.isSelected(this.data.asset.selectionAssetId) }); },
  favorite() { if (!this.data.asset) return; selectionStore.toggleFavorite(this.data.asset.selectionAssetId); this.setData({ favorite: selectionStore.isFavorite(this.data.asset.selectionAssetId) }); },
  comment(event: WechatMiniprogram.Input) { if (this.data.asset) selectionStore.setComment(this.data.asset.selectionAssetId, event.detail.value); }
});

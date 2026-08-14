import { selectionStore } from '../../services/selection-store';

Page({
  data: { asset: null as any, selected: false, favorite: false, comment: '', confirmed: false },
  onLoad(query: Record<string, string>) { const asset = selectionStore.assets.find(item => item.selectionAssetId === query.id); this.setData({ asset, selected: asset ? selectionStore.isSelected(asset.selectionAssetId) : false, favorite: asset ? selectionStore.isFavorite(asset.selectionAssetId) : false, comment: asset ? selectionStore.comments[asset.selectionAssetId] ?? '' : '', confirmed: selectionStore.confirmed }); },
  choose() { if (!this.data.asset) return; selectionStore.toggleChoice(this.data.asset.selectionAssetId); this.setData({ selected: selectionStore.isSelected(this.data.asset.selectionAssetId) }); },
  favorite() { if (!this.data.asset) return; selectionStore.toggleFavorite(this.data.asset.selectionAssetId); this.setData({ favorite: selectionStore.isFavorite(this.data.asset.selectionAssetId) }); },
  comment(event: WechatMiniprogram.Input) { if (this.data.asset) selectionStore.setComment(this.data.asset.selectionAssetId, event.detail.value); }
});

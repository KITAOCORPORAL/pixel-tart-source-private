import { selectionStore } from '../../services/selection-store';

Page({
  data: { name: '本地选片预览', photographer: '像素蛋挞', targetCount: 20, selectedCount: 0, deadline: '未设置', confirmed: false },
  onShow() {
    const project = selectionStore.project;
    this.setData({ name: project?.name ?? '本地选片预览', photographer: project?.photographerDisplayName ?? '像素蛋挞', targetCount: project?.targetCount ?? 20, selectedCount: selectionStore.selectedCount(), deadline: project?.deadline ?? '未设置', confirmed: selectionStore.confirmed });
  },
  enterGallery() { wx.navigateTo({ url: '/pages/gallery/index' }); }
});

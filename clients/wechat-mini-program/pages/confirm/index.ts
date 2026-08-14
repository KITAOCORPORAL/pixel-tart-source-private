import { selectionStore } from '../../services/selection-store';

Page({
  data: { selectedCount: 0, targetCount: 0, extraCount: 0, commentCount: 0, confirmed: false },
  onShow() { const selectedCount = selectionStore.selectedCount(); const targetCount = selectionStore.project?.targetCount ?? 0; this.setData({ selectedCount, targetCount, extraCount: Math.max(0, selectedCount - targetCount), commentCount: Object.values(selectionStore.comments).filter(Boolean).length, confirmed: selectionStore.confirmed }); },
  submit() { wx.showModal({ title: '确认选片', content: '确认后将提交给摄影师。', success: async result => { if (!result.confirm) return; await selectionStore.confirm(); this.setData({ confirmed: selectionStore.confirmed }); } }); }
});

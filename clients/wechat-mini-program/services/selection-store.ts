import { api } from './api';

export type SelectionAsset = { selectionAssetId: string; originalFileName: string; thumbUrl?: string; previewUrl?: string; selected?: boolean; favorite?: boolean; comment?: string };
export type SelectionProject = { publicId: string; name: string; photographerDisplayName?: string; targetCount: number; deadline?: string; assets: SelectionAsset[]; confirmed?: boolean };

class SelectionStore {
  project?: SelectionProject;
  assets: SelectionAsset[] = [];
  choices: Record<string, boolean> = {};
  favorites: Record<string, boolean> = {};
  comments: Record<string, string> = {};
  pendingRequests: Array<{ assetId: string; kind: string }> = [];
  confirmed = false;

  reset() { this.project = undefined; this.assets = []; this.choices = {}; this.favorites = {}; this.comments = {}; this.pendingRequests = []; this.confirmed = false; }
  load(project: SelectionProject) {
    this.project = project; this.assets = project.assets ?? []; this.confirmed = !!project.confirmed;
    this.assets.forEach(asset => { this.choices[asset.selectionAssetId] = !!asset.selected; this.favorites[asset.selectionAssetId] = !!asset.favorite; this.comments[asset.selectionAssetId] = asset.comment ?? ''; });
  }
  selectedCount() { return Object.values(this.choices).filter(Boolean).length; }
  isSelected(assetId: string) { return !!this.choices[assetId]; }
  isFavorite(assetId: string) { return !!this.favorites[assetId]; }
  toggleChoice(assetId: string) { if (this.confirmed) return; this.choices[assetId] = !this.choices[assetId]; this.pendingRequests.push({ assetId, kind: 'choice' }); }
  toggleFavorite(assetId: string) { if (this.confirmed) return; this.favorites[assetId] = !this.favorites[assetId]; this.pendingRequests.push({ assetId, kind: 'favorite' }); }
  setComment(assetId: string, value: string) { if (this.confirmed) return; this.comments[assetId] = value; this.pendingRequests.push({ assetId, kind: 'comment' }); }
  async syncPending() {
    if (!this.project) return;
    const queue = this.pendingRequests.splice(0);
    for (const item of queue) {
      const result = item.kind === 'comment'
        ? await api.comment(this.project.publicId, item.assetId, this.comments[item.assetId] ?? '')
        : await api.choice(this.project.publicId, item.assetId, this.isSelected(item.assetId), this.isFavorite(item.assetId));
      if (!result.ok && result.error.retryable) this.pendingRequests.push(item);
    }
  }
  async confirm() { if (!this.project || this.confirmed) return; const result = await api.confirm(this.project.publicId); if (result.ok || result.error.code === 'ProviderNone') this.confirmed = true; }
}

export const selectionStore = new SelectionStore();
export { SelectionStore };

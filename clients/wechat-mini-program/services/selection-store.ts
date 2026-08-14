import { api, ApiError, MutationBody } from './api';

export type SelectionAsset = { selectionAssetId: string; originalFileName: string; thumbUrl?: string; previewUrl?: string; selected?: boolean; favorite?: boolean; comment?: string };
export type SelectionProject = { publicId: string; name: string; photographerDisplayName?: string; targetCount: number; deadline?: string; assets?: SelectionAsset[]; confirmed?: boolean; selectionVersion?: number; revision?: number; isLocked?: boolean };
type PendingKind = 'choice' | 'favorite' | 'comment';
type PendingOperation = { operationId: string; assetId: string; kind: PendingKind; selected: boolean; favorite: boolean; comment: string; selectionVersion: number; revision: number; attempts: number; status: 'pending' | 'conflict' };
type PersistedState = { project?: SelectionProject; assets: SelectionAsset[]; choices: Record<string, boolean>; favorites: Record<string, boolean>; comments: Record<string, string>; pendingRequests: PendingOperation[]; confirmed: boolean; nextCursor?: string; lastError?: string; confirmationNonce?: string; confirmationSelectionVersion?: number };
const storageKey = 'pixel-tart-selection-store/v1';

class SelectionStore {
  project?: SelectionProject;
  assets: SelectionAsset[] = [];
  choices: Record<string, boolean> = {};
  favorites: Record<string, boolean> = {};
  comments: Record<string, string> = {};
  pendingRequests: PendingOperation[] = [];
  confirmed = false;
  nextCursor?: string;
  lastError = '';
  mediaToken = '';
  confirmationNonce = '';
  confirmationSelectionVersion?: number;
  private syncPromise?: Promise<void>;

  reset() { this.project = undefined; this.assets = []; this.choices = {}; this.favorites = {}; this.comments = {}; this.pendingRequests = []; this.confirmed = false; this.nextCursor = undefined; this.lastError = ''; this.confirmationNonce = ''; this.confirmationSelectionVersion = undefined; this.persist(); }
  restore() { const state = wx.getStorageSync(storageKey) as PersistedState | undefined; if (!state) return; Object.assign(this, state); }
  persist() { wx.setStorageSync(storageKey, this.snapshot()); }
  private snapshot(): PersistedState { return { project: this.project, assets: this.assets, choices: this.choices, favorites: this.favorites, comments: this.comments, pendingRequests: this.pendingRequests, confirmed: this.confirmed, nextCursor: this.nextCursor, lastError: this.lastError, confirmationNonce: this.confirmationNonce, confirmationSelectionVersion: this.confirmationSelectionVersion }; }
  load(project: SelectionProject) {
    this.project = project; this.assets = project.assets ?? []; this.confirmed = !!project.confirmed || !!project.isLocked;
    this.assets.forEach(asset => { this.choices[asset.selectionAssetId] = !!asset.selected; this.favorites[asset.selectionAssetId] = !!asset.favorite; this.comments[asset.selectionAssetId] = asset.comment ?? ''; }); this.persist();
  }
  async refreshProject() { const result = await api.project(); if (!result.ok) { this.fail(result.error); return false; } const payload = result.data as any; const remote = payload.project ?? payload; if (this.confirmationSelectionVersion !== undefined && this.confirmationSelectionVersion !== remote.selectionVersion) { this.confirmationNonce = ''; this.confirmationSelectionVersion = undefined; } this.project = { ...(this.project ?? {}), publicId: remote.publicId, name: remote.name, targetCount: remote.targetCount, selectionVersion: remote.selectionVersion, revision: remote.revision, isLocked: payload.isLocked } as SelectionProject; this.confirmed = !!payload.isLocked; this.persist(); return true; }
  async loadNextPage() { const result = await api.assets(this.nextCursor); if (!result.ok) { this.fail(result.error); return; } const page = result.data as any; const known = new Set(this.assets.map(item => item.selectionAssetId)); this.assets.push(...page.items.filter((item: SelectionAsset) => !known.has(item.selectionAssetId))); this.nextCursor = page.nextCursor; this.persist(); }
  async initializeLocalDev(config: { baseUrl: string; publicId: string; devAccessToken: string; delayMs?: 0 | 300 | 2000; randomFailure?: boolean }) { api.configure({ mode: 'LocalDev', ...config }); const ready = await this.refreshProject(); if (!ready) return false; const session = await api.mediaSession(); if (!session.ok) { this.fail(session.error); return false; } this.mediaToken = session.data.token; this.assets = []; this.nextCursor = undefined; await this.loadNextPage(); return true; }
  mediaUrl(relative?: string) { if (!relative || !this.mediaToken) return ''; const separator = relative.includes('?') ? '&' : '?'; return `${api.endpoint}${relative}${separator}media_token=${encodeURIComponent(this.mediaToken)}`; }
  selectedCount() { return Object.values(this.choices).filter(Boolean).length; }
  isSelected(assetId: string) { return !!this.choices[assetId]; }
  isFavorite(assetId: string) { return !!this.favorites[assetId]; }
  toggleChoice(assetId: string) { if (this.confirmed) return; this.choices[assetId] = !this.choices[assetId]; this.enqueue(assetId, 'choice'); }
  toggleFavorite(assetId: string) { if (this.confirmed) return; this.favorites[assetId] = !this.favorites[assetId]; this.enqueue(assetId, 'favorite'); }
  setComment(assetId: string, value: string) { if (this.confirmed) return; this.comments[assetId] = value; this.enqueue(assetId, 'comment'); }
  private enqueue(assetId: string, kind: PendingKind) { if (!this.project) return; this.pendingRequests.push({ operationId: `${Date.now()}-${Math.random().toString(16).slice(2)}`, assetId, kind, selected: this.isSelected(assetId), favorite: this.isFavorite(assetId), comment: this.comments[assetId] ?? '', selectionVersion: this.project.selectionVersion ?? 1, revision: this.project.revision ?? 0, attempts: 0, status: 'pending' }); this.persist(); }
  syncPending() {
    if (this.syncPromise) return this.syncPromise;
    this.syncPromise = this.syncPendingCore().finally(() => { this.syncPromise = undefined; });
    return this.syncPromise;
  }
  private async syncPendingCore() {
    if (!this.project || this.confirmed) return;
    while (this.pendingRequests.length > 0) {
      const item = this.pendingRequests[0]; const base: MutationBody = { expectedSelectionVersion: item.selectionVersion, expectedRevision: item.revision, operationId: item.operationId };
      const result = item.kind === 'comment' ? await api.comment(item.assetId, item.comment, base) : item.kind === 'favorite' ? await api.favorite(item.assetId, item.selected, item.favorite, base) : await api.choice(item.assetId, item.selected, item.favorite, base);
      if (result.ok) { if (this.project) { this.project.selectionVersion = result.data.selectionVersion; this.project.revision = result.data.revision; } this.pendingRequests.shift(); this.rebasePending(); this.persist(); continue; }
      item.attempts++; this.fail(result.error);
      if (result.error.conflict && item.attempts <= 3) { item.status = 'conflict'; const refreshed = await this.refreshProject(); if (refreshed && !this.confirmed) { item.selectionVersion = this.project?.selectionVersion ?? item.selectionVersion; item.revision = this.project?.revision ?? item.revision; item.status = 'pending'; this.persist(); continue; } }
      this.persist(); break;
    }
  }
  private rebasePending() { for (const item of this.pendingRequests) { item.selectionVersion = this.project?.selectionVersion ?? item.selectionVersion; item.revision = this.project?.revision ?? item.revision; } }
  private fail(error: ApiError) { this.lastError = error.message; }
  async confirm() { if (!this.project || this.confirmed) return false; await this.syncPending(); if (this.pendingRequests.length > 0) { this.lastError = '仍有未同步操作，暂不能确认。'; this.persist(); return false; } const version = this.project.selectionVersion ?? 1; if (!this.confirmationNonce || this.confirmationSelectionVersion !== version) { this.confirmationNonce = `confirm-${Date.now()}-${Math.random().toString(16).slice(2)}`; this.confirmationSelectionVersion = version; this.persist(); } const result = await api.confirm(version, this.project.revision ?? 0, this.confirmationNonce); if (!result.ok) { this.fail(result.error); this.persist(); return false; } this.confirmed = true; this.project.isLocked = true; this.project.selectionVersion = result.data.selectionVersion; this.project.revision = result.data.revision; this.persist(); return true; }
}

export const selectionStore = new SelectionStore();
export { SelectionStore };

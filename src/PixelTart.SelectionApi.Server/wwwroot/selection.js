"use strict";
(() => {
    const $ = id => document.getElementById(id);
    const publicId = location.pathname.split("/").filter(Boolean).pop();
    const key = "guangjing-selection/" + publicId;
    let access = new URLSearchParams(location.hash.slice(1)).get("token");
    try {
        if (access) sessionStorage.setItem(key, access);
        else access = sessionStorage.getItem(key);
    } catch { /* A fresh protected link can still be used when Safari storage is unavailable. */ }
    history.replaceState(null, "", location.pathname);

    const base = "/v1/client/selection/" + encodeURIComponent(publicId);
    let project, busy = false, locked = false, cursor = null, totalSelected = 0;
    let showFileNames = true;
    const loaded = new Set();

    function message(text) { $("message").textContent = text; }
    function update() {
        $("count").textContent = "已选 " + totalSelected + " 张";
        $("confirm").disabled = busy || locked || !project;
        $("state").textContent = locked ? "服务器已确认" : "受保护的相册";
        document.querySelectorAll("input").forEach(input => input.disabled = busy || locked);
    }
    async function api(path, method = "GET", body) {
        const response = await fetch(base + path, {
            method,
            headers: { "X-PixelTart-Dev-Token": access || "", ...(body ? { "Content-Type": "application/json" } : {}) },
            body: body ? JSON.stringify(body) : undefined,
            cache: "no-store"
        });
        let value;
        try { value = await response.json(); }
        catch { throw new Error("服务没有返回有效回执，请重试。"); }
        if (!response.ok) {
            const error = new Error(value.message || "请求未完成，请重试。");
            error.code = value.code;
            throw error;
        }
        return value;
    }
    function applyReceipt(receipt) {
        if (!receipt || receipt.projectId !== project.id || receipt.selectionVersion !== project.selectionVersion
            || !Number.isSafeInteger(receipt.revision) || receipt.revision < project.revision
            || receipt.isLocked !== true || !Array.isArray(receipt.items)
            || receipt.items.some(item => item.selectionProjectId !== project.id)
            || !Number.isFinite(Date.parse(receipt.confirmedAtUtc))) {
            throw new Error("未收到当前项目的有效确认回执，请刷新或重试。");
        }
        project.revision = receipt.revision;
        locked = true;
        totalSelected = receipt.items.filter(item => item.selected).length;
        $("receipt").textContent = "服务器确认回执：v" + receipt.selectionVersion + " / r" + receipt.revision
            + " · " + new Date(receipt.confirmedAtUtc).toLocaleString("zh-CN");
        $("receipt").hidden = false;
        $("hint").textContent = "已锁定，摄影师可在电脑端收回结果。";
        message("选片已确认，请通知摄影师刷新客户结果。");
        update();
    }
    async function refresh() {
        const view = await api("");
        project = view.project;
        showFileNames = view.rule.showFileNames;
        totalSelected = view.progress.selected;
        $("title").textContent = project.name;
        $("description").textContent = "为 " + project.clientDisplayName + " 准备 · 目标 " + view.rule.targetCount + " 张";
        if (view.isLocked) applyReceipt(view.confirmation);
        else {
            locked = false;
            $("receipt").hidden = true;
            $("hint").textContent = "勾选喜欢的照片，再确认提交。";
        }
        update();
    }
    function pendingOperation(asset, selected) {
        const name = key + "/pending/" + asset;
        let value;
        try { value = JSON.parse(sessionStorage.getItem(name)); } catch {}
        if (!value || value.selected !== selected) {
            value = { selected, id: crypto.randomUUID() };
            try { sessionStorage.setItem(name, JSON.stringify(value)); } catch {}
        }
        return value;
    }
    async function choose(asset, input) {
        if (busy || locked) return;
        busy = true;
        update();
        const operation = pendingOperation(asset.selectionAssetId, input.checked);
        try {
            const result = await api("/choices/" + asset.selectionAssetId, "PUT", {
                selected: operation.selected, favorite: false,
                expectedSelectionVersion: project.selectionVersion, expectedRevision: project.revision,
                operationId: operation.id
            });
            project.revision = result.revision;
            try { sessionStorage.removeItem(key + "/pending/" + asset.selectionAssetId); } catch {}
            message("已保存选择。");
            await refresh();
        } catch (error) {
            message(error.message);
            try {
                await refresh();
                const state = await api("/assets?limit=100");
                const item = state.items.find(value => value.selectionAssetId === asset.selectionAssetId);
                if (item) input.checked = item.selected;
            } catch { input.checked = !operation.selected; }
        } finally { busy = false; update(); }
    }
    async function page() {
        const result = await api("/assets?limit=50" + (cursor ? "&cursor=" + encodeURIComponent(cursor) : ""));
        for (const asset of result.items) {
            if (loaded.has(asset.selectionAssetId)) continue;
            loaded.add(asset.selectionAssetId);
            const label = document.createElement("label");
            label.className = "photo";
            const image = document.createElement("img");
            image.src = asset.previewUrl;
            image.alt = showFileNames ? "照片 " + asset.originalFileName : "照片 " + loaded.size;
            image.loading = "lazy";
            const caption = document.createElement("div");
            caption.className = "caption";
            const input = document.createElement("input");
            input.type = "checkbox";
            input.dataset.assetId = asset.selectionAssetId;
            input.checked = asset.selected;
            input.disabled = busy || locked;
            input.addEventListener("change", () => choose(asset, input));
            const name = document.createElement("span");
            name.textContent = showFileNames ? asset.originalFileName : "照片 " + loaded.size;
            caption.append(input, name);
            label.append(image, caption);
            $("gallery").append(label);
        }
        cursor = result.nextCursor;
        $("more").hidden = !cursor;
    }
    $("more").onclick = async () => { try { await page(); } catch (error) { message(error.message); } };
    function confirmationNonce() {
        const name = key + "/nonce/v" + project.selectionVersion;
        let nonce;
        try { nonce = sessionStorage.getItem(name); } catch {}
        if (!nonce) {
            nonce = crypto.randomUUID();
            try { sessionStorage.setItem(name, nonce); } catch {}
        }
        return nonce;
    }
    $("confirm").onclick = async () => {
        if (busy || locked || !project) return;
        if (!window.confirm("确认提交当前选片？提交后将锁定，请摄影师协助修改。")) return;
        busy = true;
        update();
        try {
            const receipt = await api("/confirm", "POST", {
                confirmed: true, confirmationNonce: confirmationNonce(),
                expectedSelectionVersion: project.selectionVersion, expectedRevision: project.revision
            });
            applyReceipt(receipt);
        } catch (error) {
            // A lost response may follow a committed transaction. Only the server's stored receipt proves success.
            try { await refresh(); } catch {}
            if (!locked) message(error.message);
        } finally { busy = false; update(); }
    };
    (async () => {
        try {
            if (!access) throw new Error("链接缺少访问凭据，请向摄影师索取完整链接。");
            await refresh();
            await api("/media-session", "POST", {});
            await page();
            if (!locked) message("点击照片下方的方框勾选。");
        } catch (error) {
            project = null;
            $("title").textContent = "暂时无法打开相册";
            message(error.message);
            $("hint").textContent = "链接可能已撤销或过期，请联系摄影师。";
            update();
        }
    })();
})();

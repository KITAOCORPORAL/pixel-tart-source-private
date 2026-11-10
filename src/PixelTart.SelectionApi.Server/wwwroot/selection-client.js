"use strict";
// Confirmation validation and lost-response recovery adapted from c36e1f38; keep this branch's API and media contract.
(() => {
    const operationId = () => Array.from(crypto.getRandomValues(new Uint8Array(16)), b => b.toString(16).padStart(2, "0")).join("");
    const pub = location.pathname.split("/").filter(Boolean).pop();
    const key = "guangjing-selection/" + pub;
    let token = new URLSearchParams(location.hash.slice(1)).get("token");
    try {
        if (token) sessionStorage.setItem(key, token);
        else token = sessionStorage.getItem(key);
    } catch { /* A complete link works even when storage is unavailable. */ }
    history.replaceState(null, "", location.pathname);
    const $ = id => document.getElementById(id);
    const base = "/v1/client/selection/" + encodeURIComponent(pub);
    let project = null, items = [], busy = false, urls = [];

    async function api(path, method = "GET", body) {
        const response = await fetch(path, {
            method, headers: { Authorization: "Bearer " + (token || ""), ...(body ? { "Content-Type": "application/json" } : {}) },
            body: body ? JSON.stringify(body) : undefined, cache: "no-store", referrerPolicy: "no-referrer"
        });
        let value;
        try { value = await response.json(); }
        catch { throw new Error("服务没有返回有效回执，请刷新或重试。"); }
        if (!response.ok) throw new Error(value?.message || "链接不可用或网络未连接（" + response.status + "）");
        return value;
    }
    function state(message) {
        document.querySelectorAll("input[type=checkbox]").forEach(e => e.disabled = busy || !project || project.isLocked);
        $("status").textContent = message;
        $("confirm").disabled = busy || !project || project.isLocked;
        $("count").textContent = "已选 " + items.filter(x => x.selected).length + " / " + (project?.rule.targetCount || 0) + " 张";
    }
    function applyReceipt(receipt) {
        const id = project?.project.id;
        const known = new Set(items.map(item => item.selectionAssetId));
        if (!receipt || receipt.projectId !== id || receipt.selectionVersion !== project.project.selectionVersion
            || !Number.isSafeInteger(receipt.revision) || receipt.revision < project.project.revision
            || receipt.isLocked !== true || !Array.isArray(receipt.items) || receipt.items.length !== known.size
            || new Set(receipt.items.map(item => item.imageId)).size !== known.size
            || receipt.items.some(item => item.selectionProjectId !== id || !known.has(item.imageId) || typeof item.selected !== "boolean")
            || !Number.isFinite(Date.parse(receipt.confirmedAtUtc))) {
            throw new Error("未收到当前项目的有效确认回执，请刷新或重试。");
        }
        const result = new Map(receipt.items.map(item => [item.imageId, item]));
        items.forEach(item => item.selected = result.get(item.selectionAssetId).selected);
        document.querySelectorAll("input[type=checkbox]").forEach(input => input.checked = result.get(input.dataset.assetId).selected);
        project.project.revision = receipt.revision;
        project.isLocked = true;
        project.confirmation = receipt;
        $("receipt").textContent = "服务器确认回执：v" + receipt.selectionVersion + " / r" + receipt.revision
            + " · " + new Date(receipt.confirmedAtUtc).toLocaleString("zh-CN");
        $("receipt").hidden = false;
        state("服务器已确认并锁定，摄影师可读取结果。");
    }
    function pending(name, selected) {
        const storageKey = key + name;
        let value;
        try { value = JSON.parse(sessionStorage.getItem(storageKey)); } catch {}
        if (!value || value.selected !== selected) {
            value = { selected, id: operationId() };
            try { sessionStorage.setItem(storageKey, JSON.stringify(value)); } catch {}
        }
        return value;
    }
    async function choose(item, check) {
        if (busy || project.isLocked) return;
        busy = true;
        state("正在保存选择…");
        const operation = pending("/pending/" + item.selectionAssetId, check.checked);
        try {
            const result = await api(base + "/choices/" + item.selectionAssetId, "PUT", {
                selected: operation.selected, favorite: false, expectedSelectionVersion: project.project.selectionVersion,
                expectedRevision: project.project.revision, operationId: operation.id
            });
            if (result.projectId !== project.project.id || result.selectionAssetId !== item.selectionAssetId
                || result.selectionVersion !== project.project.selectionVersion || !Number.isSafeInteger(result.revision)
                || result.revision < project.project.revision) throw new Error("选择回执身份无效，请刷新。");
            item.selected = operation.selected;
            project.project.revision = result.revision;
            try { sessionStorage.removeItem(key + "/pending/" + item.selectionAssetId); } catch {}
            state("选择已保存");
        } catch (error) {
            check.checked = item.selected;
            // Re-read acknowledged state before another edit after an ambiguous transport failure.
            await load();
            if (!project?.isLocked) state(error.message);
        } finally { busy = false; state($("status").textContent); }
    }
    async function load() {
        busy = true;
        state("正在读取相册…");
        try {
            const view = await api(base);
            const page = await api(base + "/assets");
            if (!view?.project?.id || view.project.publicId !== pub || !Array.isArray(page.items)) throw new Error("相册身份无效。");
            project = view;
            items = page.items;
            urls.forEach(URL.revokeObjectURL); urls = [];
            $("title").textContent = project.project.name;
            $("rule").textContent = "目标 " + project.rule.targetCount + " 张，最多 " + project.rule.maximumCount + " 张；确认后锁定。";
            $("receipt").hidden = true;
            $("gallery").replaceChildren();
            for (const item of items) {
                const card = document.createElement("article"); card.className = "card";
                const img = document.createElement("img"); img.alt = "选片预览";
                const response = await fetch(item.previewUrl, { headers: { Authorization: "Bearer " + token }, cache: "no-store", referrerPolicy: "no-referrer" });
                if (!response.ok) throw new Error("照片暂不可用");
                const url = URL.createObjectURL(await response.blob()); urls.push(url); img.src = url;
                const label = document.createElement("label"); label.className = "choice";
                const check = document.createElement("input"); check.type = "checkbox"; check.dataset.assetId = item.selectionAssetId;
                check.checked = item.selected; check.disabled = true; check.setAttribute("aria-label", "选择 " + item.originalFileName);
                const text = document.createElement("span"); text.className = "name"; text.textContent = item.originalFileName;
                check.onchange = () => choose(item, check);
                label.append(check, text); card.append(img, label); $("gallery").append(card);
            }
            if (view.isLocked) applyReceipt(view.confirmation);
            else state("请勾选照片，再确认选择。");
        } catch (error) {
            project = null; items = []; urls.forEach(URL.revokeObjectURL); urls = [];
            $("gallery").replaceChildren(); $("receipt").hidden = true; $("title").textContent = "相册暂不可用"; state(error.message);
        } finally { busy = false; state($("status").textContent); }
    }
    $("confirm").onclick = async () => {
        if (!project || busy || project.isLocked) return;
        busy = true; state("正在确认…");
        try {
            const receipt = await api(base + "/confirm", "POST", {
                confirmed: true, confirmationNonce: pending("/nonce/v" + project.project.selectionVersion, true).id,
                expectedSelectionVersion: project.project.selectionVersion, expectedRevision: project.project.revision
            });
            applyReceipt(receipt);
        } catch (error) {
            // A committed confirmation may outlive its response. Only a validated stored receipt proves success.
            await load();
            if (!project?.isLocked) state(error.message);
        } finally { busy = false; state($("status").textContent); }
    };
    $("refresh").onclick = () => { if (!busy) return load(); };
    if (!/^[a-f0-9]{36}$/.test(pub || "") || !token) { state("受保护链接不完整。"); $("title").textContent = "无法打开相册"; }
    else load();
})();

// Shared interactive positioning map.
//
// Renders a world coordinate plane with an optional floor-plan overlay,
// draggable anchor markers, and a single tag marker. Used by both the
// live session view (Views/Sessions/Live.cshtml) and the room detail
// cockpit (Views/SessionConfigs/Details.cshtml).
//
// Anchor drag persists via PATCH /api/SessionConfigChips/{id}/coords —
// the same endpoint that already exists.
//
// Usage:
//   const plane = PositionPlane.mount(rootElement, {
//       anchors:   [{ id, chipId, name, x, y, z }],
//       tags:      [{ chipId, deviceIdentifier, name }],
//       floorPlan: { url, ox, oy, w, h, scale, rot, opacity } | null,
//       mode:      "live" | "layout",
//   });
//   plane.updateTagMarker({ tagId, x, y, z });   // live mode only
//   plane.setSelectedAnchors([chipId, ...]);      // routing highlight
//   plane.dispose();

(function (global) {
    "use strict";

    function fmt(n) {
        return typeof n === "number" ? n.toFixed(3) : (n ?? "");
    }

    function mount(root, config) {
        const anchors = (config.anchors || []).map(a => ({ ...a }));
        const tags = config.tags || [];
        const floorPlan = config.floorPlan || null;
        const mode = config.mode || "layout";

        // Root elements (created by the _PositionPlane.cshtml partial).
        const plane = root.querySelector(".position-plane");
        const content = root.querySelector(".plane-content");
        const floorImg = root.querySelector(".floor-plan");
        const marker = root.querySelector(".tag-marker");
        const zoomLabel = root.querySelector(".plane-zoom-label");
        const zoomInBtn = root.querySelector("[data-plane-zoom-in]");
        const zoomOutBtn = root.querySelector("[data-plane-zoom-out]");
        const zoomResetBtn = root.querySelector("[data-plane-zoom-reset]");

        const anchorEls = new Map(); // chipId (lowercased) -> DOM element

        // ---- world bounds --------------------------------------------------
        const bounds = (() => {
            const xs = anchors.map(a => Number(a.x)).filter(Number.isFinite);
            const ys = anchors.map(a => Number(a.y)).filter(Number.isFinite);
            let minX = Math.min(0, ...xs);
            let maxX = Math.max(10, ...xs);
            let minY = Math.min(0, ...ys);
            let maxY = Math.max(10, ...ys);
            if (floorPlan) {
                minX = Math.min(minX, floorPlan.ox);
                minY = Math.min(minY, floorPlan.oy);
                maxX = Math.max(maxX, floorPlan.ox + floorPlan.w);
                maxY = Math.max(maxY, floorPlan.oy + floorPlan.h);
            }
            const padX = Math.max((maxX - minX) * 0.12, 0.5);
            const padY = Math.max((maxY - minY) * 0.12, 0.5);
            return { minX: minX - padX, maxX: maxX + padX, minY: minY - padY, maxY: maxY + padY };
        })();

        function toPercent(x, y) {
            const left = ((x - bounds.minX) / (bounds.maxX - bounds.minX)) * 100;
            const top = 100 - ((y - bounds.minY) / (bounds.maxY - bounds.minY)) * 100;
            return {
                left: Math.max(0, Math.min(100, left)),
                top: Math.max(0, Math.min(100, top))
            };
        }

        // ---- fit content to preserve aspect ratio --------------------------
        function fitContentToWorld() {
            const worldW = bounds.maxX - bounds.minX;
            const worldH = bounds.maxY - bounds.minY;
            if (worldW <= 0 || worldH <= 0) return;
            const worldAR = worldW / worldH;
            const rect = plane.getBoundingClientRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            const planeAR = rect.width / rect.height;

            let cw, ch;
            if (planeAR > worldAR) {
                ch = rect.height;
                cw = ch * worldAR;
            } else {
                cw = rect.width;
                ch = cw / worldAR;
            }
            content.style.width = `${cw}px`;
            content.style.height = `${ch}px`;
            content.style.left = `${(rect.width - cw) / 2}px`;
            content.style.top = `${(rect.height - ch) / 2}px`;
            content.style.right = "auto";
            content.style.bottom = "auto";
        }

        // ---- pan & zoom ----------------------------------------------------
        const view = { scale: 1, tx: 0, ty: 0 };
        const MIN_SCALE = 0.25;
        const MAX_SCALE = 8;

        function applyView() {
            content.style.transform = `translate(${view.tx}px, ${view.ty}px) scale(${view.scale})`;
            const inv = 1 / view.scale;
            content.querySelectorAll(".anchor-marker, .tag-marker").forEach(el => {
                el.style.setProperty("--inv-scale", inv);
            });
            if (zoomLabel) zoomLabel.textContent = `${Math.round(view.scale * 100)}%`;
        }

        function zoomAt(clientX, clientY, factor) {
            const rect = plane.getBoundingClientRect();
            const px = clientX - rect.left;
            const py = clientY - rect.top;
            const newScale = Math.max(MIN_SCALE, Math.min(MAX_SCALE, view.scale * factor));
            const k = newScale / view.scale;
            view.tx = px - k * (px - view.tx);
            view.ty = py - k * (py - view.ty);
            view.scale = newScale;
            applyView();
        }

        function onWheel(e) {
            e.preventDefault();
            zoomAt(e.clientX, e.clientY, Math.exp(-e.deltaY * 0.0015));
        }

        let panning = false;
        let panLastX = 0, panLastY = 0;

        function onPlanePointerDown(e) {
            panning = true;
            panLastX = e.clientX;
            panLastY = e.clientY;
            plane.setPointerCapture(e.pointerId);
            plane.classList.add("is-panning");
        }
        function onPlanePointerMove(e) {
            if (!panning) return;
            view.tx += e.clientX - panLastX;
            view.ty += e.clientY - panLastY;
            panLastX = e.clientX;
            panLastY = e.clientY;
            applyView();
        }
        function endPan(e) {
            if (!panning) return;
            panning = false;
            try { plane.releasePointerCapture(e.pointerId); } catch { }
            plane.classList.remove("is-panning");
        }

        plane.addEventListener("wheel", onWheel, { passive: false });
        plane.addEventListener("pointerdown", onPlanePointerDown);
        plane.addEventListener("pointermove", onPlanePointerMove);
        plane.addEventListener("pointerup", endPan);
        plane.addEventListener("pointercancel", endPan);

        if (zoomInBtn) zoomInBtn.addEventListener("click", () => {
            const r = plane.getBoundingClientRect();
            zoomAt(r.left + r.width / 2, r.top + r.height / 2, 1.25);
        });
        if (zoomOutBtn) zoomOutBtn.addEventListener("click", () => {
            const r = plane.getBoundingClientRect();
            zoomAt(r.left + r.width / 2, r.top + r.height / 2, 1 / 1.25);
        });
        if (zoomResetBtn) zoomResetBtn.addEventListener("click", () => {
            view.scale = 1; view.tx = 0; view.ty = 0; applyView();
        });

        function onResize() {
            fitContentToWorld();
            applyView();
        }
        window.addEventListener("resize", onResize);

        // ---- anchor drag ---------------------------------------------------
        function pointerToWorld(clientX, clientY) {
            const planeRect = plane.getBoundingClientRect();
            const cw = content.offsetWidth;
            const ch = content.offsetHeight;
            const cLeft = content.offsetLeft;
            const cTop = content.offsetTop;
            const px = clientX - planeRect.left - cLeft;
            const py = clientY - planeRect.top - cTop;
            const localX = (px - view.tx) / view.scale;
            const localY = (py - view.ty) / view.scale;
            const fracX = localX / cw;
            const fracY = localY / ch;
            return {
                x: bounds.minX + fracX * (bounds.maxX - bounds.minX),
                y: bounds.minY + (1 - fracY) * (bounds.maxY - bounds.minY),
            };
        }

        function updateAnchorVisual(el, anchor) {
            const p = toPercent(Number(anchor.x), Number(anchor.y));
            el.style.left = `${p.left}%`;
            el.style.top = `${p.top}%`;
            el.title = `${anchor.name} (${fmt(Number(anchor.x))}, ${fmt(Number(anchor.y))})`;
        }

        async function persistAnchorCoords(anchor) {
            if (!anchor.id) return;
            try {
                const resp = await fetch(`/api/SessionConfigChips/${encodeURIComponent(anchor.id)}/coords`, {
                    method: "PATCH",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ x: Number(anchor.x), y: Number(anchor.y) })
                });
                if (!resp.ok) {
                    console.warn("[position-plane] failed to persist anchor coords", resp.status);
                }
            } catch (err) {
                console.warn("[position-plane] failed to persist anchor coords", err);
            }
        }

        function enableAnchorDrag(el, anchor) {
            let dragging = false;
            let moved = false;
            let startX = 0, startY = 0;

            el.addEventListener("pointerdown", e => {
                if (e.button !== undefined && e.button !== 0) return;
                e.stopPropagation();
                e.preventDefault();
                dragging = true;
                moved = false;
                startX = e.clientX;
                startY = e.clientY;
                el.setPointerCapture(e.pointerId);
                el.classList.add("is-dragging");
            });

            el.addEventListener("pointermove", e => {
                if (!dragging) return;
                if (!moved) {
                    const dx = e.clientX - startX;
                    const dy = e.clientY - startY;
                    if (dx * dx + dy * dy < 9) return;
                    moved = true;
                }
                const w = pointerToWorld(e.clientX, e.clientY);
                anchor.x = w.x;
                anchor.y = w.y;
                updateAnchorVisual(el, anchor);
            });

            function finishDrag(e) {
                if (!dragging) return;
                dragging = false;
                try { el.releasePointerCapture(e.pointerId); } catch { }
                el.classList.remove("is-dragging");
                if (moved) persistAnchorCoords(anchor);
            }
            el.addEventListener("pointerup", finishDrag);
            el.addEventListener("pointercancel", finishDrag);
        }

        function placeAnchor(anchor) {
            const el = document.createElement("div");
            el.className = "anchor-marker is-draggable";
            el.innerHTML = `<span></span><strong></strong>`;
            el.querySelector("strong").textContent = anchor.name;
            content.appendChild(el);
            updateAnchorVisual(el, anchor);
            if (anchor.chipId) {
                anchorEls.set(String(anchor.chipId).toLowerCase(), el);
            }
            enableAnchorDrag(el, anchor);
        }

        function placeFloorPlan() {
            if (!floorPlan || !floorImg) return;
            const tl = toPercent(floorPlan.ox, floorPlan.oy + floorPlan.h);
            const br = toPercent(floorPlan.ox + floorPlan.w, floorPlan.oy);
            floorImg.src = floorPlan.url;
            floorImg.style.left = `${tl.left}%`;
            floorImg.style.top = `${tl.top}%`;
            floorImg.style.width = `${br.left - tl.left}%`;
            floorImg.style.height = `${br.top - tl.top}%`;
            floorImg.style.opacity = floorPlan.opacity ?? 0.7;
            if (floorPlan.rot) {
                floorImg.style.transform = `rotate(${floorPlan.rot}deg)`;
                floorImg.style.transformOrigin = "center center";
            }
            floorImg.hidden = false;
        }

        // ---- tag marker ----------------------------------------------------
        const tagNames = new Map(tags.map(t => [String(t.chipId).toLowerCase(), t.deviceIdentifier]));
        const tagDisplay = new Map(tags.map(t => [String(t.chipId).toLowerCase(), t.name]));

        function tagLabel(tagId) {
            if (!tagId) return "";
            const key = String(tagId).toLowerCase();
            return tagDisplay.get(key) ?? String(tagId).slice(0, 8);
        }

        function updateTagMarker(m) {
            if (!marker) return;
            const x = Number(m.x);
            const y = Number(m.y);
            if (!Number.isFinite(x) || !Number.isFinite(y)) return;
            const p = toPercent(x, y);
            marker.hidden = false;
            marker.style.left = `${p.left}%`;
            marker.style.top = `${p.top}%`;
            const strong = marker.querySelector("strong");
            if (strong) strong.textContent = tagLabel(m.tagId);
        }

        function setSelectedAnchors(selectedChipIds) {
            const selected = new Set((selectedChipIds || []).map(id => String(id).toLowerCase()));
            anchorEls.forEach((el, chipId) => {
                el.classList.toggle("is-selected", selected.has(chipId));
            });
        }

        function tagDeviceIdFor(chipId) {
            if (!chipId) return null;
            return tagNames.get(String(chipId).toLowerCase()) ?? null;
        }

        // ---- initial paint -------------------------------------------------
        placeFloorPlan();
        anchors.forEach(placeAnchor);
        fitContentToWorld();
        applyView();

        function dispose() {
            plane.removeEventListener("wheel", onWheel);
            plane.removeEventListener("pointerdown", onPlanePointerDown);
            plane.removeEventListener("pointermove", onPlanePointerMove);
            plane.removeEventListener("pointerup", endPan);
            plane.removeEventListener("pointercancel", endPan);
            window.removeEventListener("resize", onResize);
        }

        return {
            updateTagMarker,
            setSelectedAnchors,
            tagDeviceIdFor,
            tagLabel,
            bounds,
            mode,
            dispose,
        };
    }

    global.PositionPlane = { mount };
})(window);

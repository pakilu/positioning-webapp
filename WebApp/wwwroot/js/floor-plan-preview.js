// Lightweight live preview for the floor-plan calibration form.
//
// Renders a mini world-coordinate canvas showing:
//   - a light grid (1 m squares),
//   - the floor-plan image at its current calibration (ox, oy, w, h, rot, opacity),
//   - the room's existing anchors as dots (for visual alignment),
//   - a "1 m" scale bar.
//
// It reads values live from the form controls it's wired to and re-renders on
// every input event. No dependency on PositionPlane — deliberately simple so
// the Edit view stays self-contained.
//
// Usage:
//   FloorPlanPreview.mount(rootEl, {
//       anchors: [{name, x, y}, ...],
//       fields: {
//           file:      HTMLInputElement,   // <input type="file">, optional
//           urlPath:   HTMLInputElement,   // fallback URL text input
//           ox:        HTMLInputElement,
//           oy:        HTMLInputElement,
//           w:         HTMLInputElement,
//           h:         HTMLInputElement,
//           scale:     HTMLInputElement,   // 1:N; empty/0 => 1
//           rot:       HTMLInputElement,
//           opacity:   HTMLInputElement,
//           remove:    HTMLInputElement,   // checkbox, optional
//       },
//       initialUrl: "/maps/foo.png" | null,   // already-saved image path
//   });
(function (global) {
    "use strict";

    function num(el, fallback) {
        if (!el) return fallback;
        const v = parseFloat(el.value);
        return Number.isFinite(v) ? v : fallback;
    }

    function mount(root, opts) {
        const anchors = (opts.anchors || []).filter(a => Number.isFinite(a.x) && Number.isFinite(a.y));
        const f = opts.fields;

        // Build DOM. The outer .fp-preview-canvas is the fixed-aspect viewport;
        // the inner .fp-preview-content is sized in JS to match the world's own
        // aspect ratio (letterboxed inside the viewport), so images placed by
        // percentage keep their true aspect ratio — same trick as position-plane.js.
        root.classList.add("fp-preview");
        root.innerHTML = `
            <div class="fp-preview-canvas">
                <div class="fp-preview-content">
                    <img class="fp-preview-img" alt="" hidden />
                    <div class="fp-preview-anchors"></div>
                </div>
                <div class="fp-preview-axis-x">X →</div>
                <div class="fp-preview-axis-y">↑ Y</div>
                <div class="fp-preview-scalebar"><span class="fp-preview-scalebar-line"></span><span class="fp-preview-scalebar-label">1 m</span></div>
                <div class="fp-preview-empty">Upload an image or fill in the size fields to see a preview.</div>
            </div>
            <div class="fp-preview-status text-muted small mt-2"></div>
        `;
        const canvas   = root.querySelector(".fp-preview-canvas");
        const content  = root.querySelector(".fp-preview-content");
        const img      = root.querySelector(".fp-preview-img");
        const dotsHost = root.querySelector(".fp-preview-anchors");
        const scaleBar = root.querySelector(".fp-preview-scalebar-line");
        const emptyHint = root.querySelector(".fp-preview-empty");
        const status    = root.querySelector(".fp-preview-status");

        // Local state that survives across renders.
        let objectUrl = null;            // blob URL for the picked file
        let localUrl  = opts.initialUrl; // effective image url used in preview
        let imgNatural = null;           // {w, h} once the image has loaded

        function currentUrl() {
            if (f.remove && f.remove.checked) return null;
            if (objectUrl) return objectUrl;
            const p = (f.urlPath && f.urlPath.value.trim()) || "";
            if (p) return p;
            return localUrl;
        }

        function fillDefaultsFromImage(natural) {
            // Only fill defaults if the user has not yet typed sensible ones.
            const oxEmpty = !f.ox.value.trim();
            const oyEmpty = !f.oy.value.trim();
            const wEmpty  = !f.w.value.trim()  || parseFloat(f.w.value) <= 0;
            const hEmpty  = !f.h.value.trim()  || parseFloat(f.h.value) <= 0;
            if (!oxEmpty && !oyEmpty && !wEmpty && !hEmpty) return false;

            // Choose a sensible default width. Fit around existing anchors if any.
            let defW = 10;
            let defOx = 0, defOy = 0;
            if (anchors.length > 0) {
                const xs = anchors.map(a => a.x);
                const ys = anchors.map(a => a.y);
                const minX = Math.min(...xs), maxX = Math.max(...xs);
                const minY = Math.min(...ys), maxY = Math.max(...ys);
                const span = Math.max(maxX - minX, 2);
                defW = Math.max(10, Math.round((span + 2) * 10) / 10);
                defOx = Math.round((minX - 1) * 10) / 10;
                defOy = Math.round((minY - 1) * 10) / 10;
            }
            const aspect = natural.w / natural.h;
            const defH = Math.round((defW / aspect) * 10) / 10;

            if (oxEmpty) f.ox.value = defOx;
            if (oyEmpty) f.oy.value = defOy;
            if (wEmpty)  f.w.value  = defW;
            if (hEmpty)  f.h.value  = defH;
            return true;
        }

        function readPlan() {
            const url = currentUrl();
            if (!url) return null;
            const ox = num(f.ox, NaN);
            const oy = num(f.oy, NaN);
            const wRaw = num(f.w, NaN);
            const hRaw = num(f.h, NaN);
            if (!Number.isFinite(ox) || !Number.isFinite(oy) || !Number.isFinite(wRaw) || !Number.isFinite(hRaw)) return null;
            if (wRaw <= 0 || hRaw <= 0) return null;
            const scaleN = Math.max(num(f.scale, 1) || 1, 0.0001);
            return {
                url,
                ox,
                oy,
                w:   wRaw * scaleN,
                h:   hRaw * scaleN,
                rot: num(f.rot, 0),
                opacity: Math.max(0, Math.min(1, num(f.opacity, 0.7))),
            };
        }

        function computeBounds(plan) {
            let minX = 0, maxX = 10, minY = 0, maxY = 10;
            for (const a of anchors) {
                minX = Math.min(minX, a.x); maxX = Math.max(maxX, a.x);
                minY = Math.min(minY, a.y); maxY = Math.max(maxY, a.y);
            }
            if (plan) {
                minX = Math.min(minX, plan.ox);
                minY = Math.min(minY, plan.oy);
                maxX = Math.max(maxX, plan.ox + plan.w);
                maxY = Math.max(maxY, plan.oy + plan.h);
            }
            const padX = Math.max((maxX - minX) * 0.10, 0.5);
            const padY = Math.max((maxY - minY) * 0.10, 0.5);
            return { minX: minX - padX, maxX: maxX + padX, minY: minY - padY, maxY: maxY + padY };
        }

        function toPct(b, x, y) {
            return {
                left: ((x - b.minX) / (b.maxX - b.minX)) * 100,
                top: 100 - ((y - b.minY) / (b.maxY - b.minY)) * 100,
            };
        }

        function fitContentToWorld(worldW, worldH) {
            if (worldW <= 0 || worldH <= 0) return;
            const rect = canvas.getBoundingClientRect();
            if (rect.width <= 0 || rect.height <= 0) return;
            const worldAR = worldW / worldH;
            const canvasAR = rect.width / rect.height;
            let cw, ch;
            if (canvasAR > worldAR) {
                ch = rect.height;
                cw = ch * worldAR;
            } else {
                cw = rect.width;
                ch = cw / worldAR;
            }
            content.style.width  = `${cw}px`;
            content.style.height = `${ch}px`;
            content.style.left = `${(rect.width - cw) / 2}px`;
            content.style.top  = `${(rect.height - ch) / 2}px`;
        }

        function render() {
            const plan = readPlan();
            const b = computeBounds(plan);
            const worldW = b.maxX - b.minX;
            const worldH = b.maxY - b.minY;

            // Size the inner content box to preserve the world's aspect ratio.
            fitContentToWorld(worldW, worldH);

            // Grid: 1 m squares, sized in real content pixels (never stretched).
            const contentW = content.offsetWidth;
            const contentH = content.offsetHeight;
            if (contentW > 0 && contentH > 0 && worldW > 0 && worldH > 0) {
                const pxPerMx = contentW / worldW;
                const pxPerMy = contentH / worldH;
                content.style.backgroundSize = `${pxPerMx}px ${pxPerMy}px`;
                scaleBar.style.width = `${pxPerMx}px`;
            }

            // Anchors.
            dotsHost.innerHTML = "";
            for (const a of anchors) {
                const p = toPct(b, a.x, a.y);
                if (p.left < 0 || p.left > 100 || p.top < 0 || p.top > 100) continue;
                const el = document.createElement("div");
                el.className = "fp-preview-anchor";
                el.style.left = `${p.left}%`;
                el.style.top = `${p.top}%`;
                el.title = `${a.name} (${a.x.toFixed(2)}, ${a.y.toFixed(2)})`;
                const label = document.createElement("span");
                label.textContent = a.name;
                el.appendChild(label);
                dotsHost.appendChild(el);
            }

            // Image.
            if (plan) {
                if (img.src !== plan.url) img.src = plan.url;
                const tl = toPct(b, plan.ox, plan.oy + plan.h);
                const br = toPct(b, plan.ox + plan.w, plan.oy);
                img.style.left = `${tl.left}%`;
                img.style.top = `${tl.top}%`;
                img.style.width = `${br.left - tl.left}%`;
                img.style.height = `${br.top - tl.top}%`;
                img.style.opacity = plan.opacity;
                img.style.transform = plan.rot ? `rotate(${plan.rot}deg)` : "";
                img.style.transformOrigin = "center center";
                img.hidden = false;
                emptyHint.hidden = true;
                status.textContent =
                    `Bottom-left at (${plan.ox.toFixed(2)}, ${plan.oy.toFixed(2)}) m · ` +
                    `${plan.w.toFixed(2)} × ${plan.h.toFixed(2)} m` +
                    (plan.rot ? ` · rot ${plan.rot}°` : "") +
                    (Math.abs(plan.opacity - 0.7) > 0.001 ? ` · opacity ${Math.round(plan.opacity * 100)}%` : "");
            } else {
                img.hidden = true;
                const hasUrl = !!currentUrl();
                emptyHint.hidden = false;
                emptyHint.textContent = hasUrl
                    ? "Fill in Left-edge X, Bottom-edge Y, Width and Height to place the image."
                    : "Upload an image (or provide a web path) to see a preview.";
                status.textContent = anchors.length > 0
                    ? `Room has ${anchors.length} anchor${anchors.length === 1 ? "" : "s"} shown as blue dots.`
                    : "";
            }
        }

        // ---- wire up field events -----------------------------------------
        const listeners = [
            f.ox, f.oy, f.w, f.h, f.scale, f.rot, f.opacity, f.urlPath, f.remove
        ].filter(Boolean);
        for (const el of listeners) {
            el.addEventListener("input", render);
            el.addEventListener("change", render);
        }

        if (f.file) {
            f.file.addEventListener("change", () => {
                const file = f.file.files && f.file.files[0];
                if (objectUrl) { URL.revokeObjectURL(objectUrl); objectUrl = null; }
                if (!file) { render(); return; }
                objectUrl = URL.createObjectURL(file);
                // Load to discover natural size, then optionally seed defaults.
                const probe = new Image();
                probe.onload = () => {
                    imgNatural = { w: probe.naturalWidth || 1, h: probe.naturalHeight || 1 };
                    fillDefaultsFromImage(imgNatural);
                    render();
                };
                probe.onerror = () => { render(); };
                probe.src = objectUrl;
            });
        }

        // Initial render (and pick up naturalWidth of an already-saved image).
        if (localUrl) {
            const probe = new Image();
            probe.onload = () => {
                imgNatural = { w: probe.naturalWidth || 1, h: probe.naturalHeight || 1 };
                render();
            };
            probe.src = localUrl;
        }
        render();
        // Re-render on resize so the grid and scale bar stay correct.
        const ro = ("ResizeObserver" in global) ? new ResizeObserver(render) : null;
        if (ro) ro.observe(canvas);

        return { render };
    }

    global.FloorPlanPreview = { mount };
})(window);

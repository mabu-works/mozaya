"use strict";

// ==========================================
// 🖼️ もざや 画面側の配線
// 計算（パラメータ換算・矩形・座標変換）は js/mozaya-core.js に寄せ、
// ここでは DOM と Canvas の操作だけを行う。
// ==========================================

const Core = window.MozayaCore;

// ==========================================
// 🗂️ 状態
// ==========================================
// 🖼️ もとの画像を保持するオフスクリーン Canvas（元の解像度）。
// 描画は毎回ここから描き直すので、加工が重ねがけで劣化しない。
const srcCanvas = document.createElement("canvas");
let hasImage = false;
let imageName = "";
// ✂️ 加工範囲: {x, y, w, h, mode: "mosaic"|"blur", strength}
// 座標はすべて元の画像のピクセル単位。
let regions = [];
let dragStart = null; // ドラッグ開始点 {x, y}（元画像座標）
let dragCurrent = null; // ドラッグ中の現在点 {x, y}（元画像座標）

// ==========================================
// 🧩 DOM
// ==========================================
const $ = (id) => document.getElementById(id);
const dropZone = $("dropZone");
const fileInput = $("fileInput");
const fileMsg = $("fileMsg");
const imageInfo = $("imageInfo");
const canvas = $("mainCanvas");
const ctx = canvas.getContext("2d");
const strengthRange = $("strengthRange");
const strengthValue = $("strengthValue");
const undoBtn = $("undoBtn");
const clearRegionsBtn = $("clearRegionsBtn");
const exportBtn = $("exportBtn");
const exportMsg = $("exportMsg");
const regionList = $("regionList");
const regionEmpty = $("regionEmpty");
const regionCount = $("regionCount");
const noImageMsg = $("noImageMsg");

// 🔘 選択中の加工モードを返す
function currentMode() {
  const checked = document.querySelector('input[name="mode"]:checked');
  return checked ? checked.value : "mosaic";
}

// 🎚️ 強さスライダーの値を返す（不正な値は既定値）
function currentStrength() {
  return Core.parseStrength(strengthRange.value);
}

strengthRange.addEventListener("input", () => {
  strengthValue.textContent = String(currentStrength());
});

// ==========================================
// 🖼️ 画像読み込み
// ==========================================
// 📂 ファイルを受け取り、画像として読み込む
function loadFile(file) {
  if (!file) return;
  if (!Core.isAcceptedType(file.type)) {
    fileMsg.textContent = "対応していないファイルです。PNG または JPG の画像を選んでください。";
    return;
  }
  const url = URL.createObjectURL(file);
  const img = new Image();
  img.onload = () => {
    URL.revokeObjectURL(url);
    srcCanvas.width = img.naturalWidth;
    srcCanvas.height = img.naturalHeight;
    srcCanvas.getContext("2d").drawImage(img, 0, 0);
    canvas.width = img.naturalWidth;
    canvas.height = img.naturalHeight;
    hasImage = true;
    imageName = file.name || "image";
    regions = [];
    dragStart = null;
    dragCurrent = null;
    fileMsg.textContent = "画像を読み込みました。";
    imageInfo.textContent =
      "ファイル名：" + imageName + "／サイズ：" + img.naturalWidth + " × " + img.naturalHeight + " px（書き出しも同じ解像度です）";
    noImageMsg.style.display = "none";
    exportBtn.disabled = false;
    render();
    renderRegionList();
  };
  img.onerror = () => {
    URL.revokeObjectURL(url);
    fileMsg.textContent = "画像を読み込めませんでした。ファイルが壊れていないか確認してください。";
  };
  img.src = url;
}

dropZone.addEventListener("click", () => fileInput.click());
dropZone.addEventListener("keydown", (e) => {
  if (e.key === "Enter" || e.key === " ") {
    e.preventDefault();
    fileInput.click();
  }
});
fileInput.addEventListener("change", () => {
  loadFile(fileInput.files && fileInput.files[0]);
  fileInput.value = "";
});

["dragenter", "dragover"].forEach((type) => {
  dropZone.addEventListener(type, (e) => {
    e.preventDefault();
    dropZone.classList.add("is-dragover");
  });
});
["dragleave", "drop"].forEach((type) => {
  dropZone.addEventListener(type, (e) => {
    e.preventDefault();
    dropZone.classList.remove("is-dragover");
  });
});
dropZone.addEventListener("drop", (e) => {
  const files = e.dataTransfer && e.dataTransfer.files;
  if (files && files.length > 0) loadFile(files[0]);
});

// ==========================================
// 🎨 加工の適用
// ==========================================
// 🎨 1つの範囲を targetCtx 上に適用する。元画像（srcCanvas）から読むので、
// 隣接ピクセルを使ったぼかしも正しく計算できる。
function applyRegion(targetCtx, region) {
  const { x, y, w, h, mode, strength } = region;
  if (w < 1 || h < 1) return;
  if (mode === "mosaic") {
    // 🧱 範囲を小さく縮小してから、スムージングなしで拡大して描く
    const { smallW, smallH } = Core.mosaicSmallSize(w, h, strength);
    const tmp = document.createElement("canvas");
    tmp.width = smallW;
    tmp.height = smallH;
    const tctx = tmp.getContext("2d");
    tctx.drawImage(srcCanvas, x, y, w, h, 0, 0, smallW, smallH);
    targetCtx.save();
    targetCtx.imageSmoothingEnabled = false;
    targetCtx.drawImage(tmp, 0, 0, smallW, smallH, x, y, w, h);
    targetCtx.restore();
  } else {
    // 🌀 ぼかし：範囲でクリップしたうえで元画像全体をぼかして描く
    const radius = Core.blurRadius(strength);
    targetCtx.save();
    targetCtx.beginPath();
    targetCtx.rect(x, y, w, h);
    targetCtx.clip();
    targetCtx.filter = "blur(" + radius + "px)";
    targetCtx.drawImage(srcCanvas, 0, 0);
    targetCtx.restore();
  }
}

// 🎨 元画像＋全範囲を描く。showGuides=true のときは範囲の枠とドラッグ中プレビューも描く。
function paint(targetCtx, width, height, showGuides) {
  targetCtx.save();
  targetCtx.filter = "none";
  targetCtx.imageSmoothingEnabled = true;
  targetCtx.clearRect(0, 0, width, height);
  if (hasImage) targetCtx.drawImage(srcCanvas, 0, 0);
  targetCtx.restore();
  if (!hasImage) return;
  regions.forEach((r) => applyRegion(targetCtx, r));
  if (showGuides) {
    targetCtx.save();
    targetCtx.strokeStyle = "rgba(109, 40, 217, 0.95)";
    targetCtx.lineWidth = Core.guideLineWidth(width);
    targetCtx.setLineDash([8, 5]);
    regions.forEach((r) => targetCtx.strokeRect(r.x, r.y, r.w, r.h));
    if (dragStart && dragCurrent) {
      const rect = Core.normalizeRect(dragStart, dragCurrent);
      targetCtx.fillStyle = "rgba(109, 40, 217, 0.18)";
      targetCtx.fillRect(rect.x, rect.y, rect.w, rect.h);
      targetCtx.strokeRect(rect.x, rect.y, rect.w, rect.h);
    }
    targetCtx.restore();
  }
}

// 🖼️ 画面を描き直す
function render() {
  paint(ctx, canvas.width, canvas.height, true);
}

// ==========================================
// 🖱️ 範囲のドラッグ追加
// ==========================================
// 🔁 表示座標（CSSピクセル）→ 元画像座標へ変換
function toImageCoords(event) {
  const rect = canvas.getBoundingClientRect();
  return Core.displayToImage(event.clientX, event.clientY, rect, canvas.width, canvas.height);
}

canvas.addEventListener("pointerdown", (e) => {
  if (!hasImage) {
    fileMsg.textContent = "まず画像を読み込んでください。";
    return;
  }
  e.preventDefault();
  canvas.setPointerCapture(e.pointerId);
  dragStart = toImageCoords(e);
  dragCurrent = dragStart;
});

canvas.addEventListener("pointermove", (e) => {
  if (!dragStart) return;
  dragCurrent = toImageCoords(e);
  render();
});

// 🖱️ ドラッグ終了時に範囲を確定する
function finishDrag(e) {
  if (!dragStart) return;
  const end = toImageCoords(e);
  const rect = Core.normalizeRect(dragStart, end);
  dragStart = null;
  dragCurrent = null;
  if (!Core.isRegionBigEnough(rect)) {
    render();
    return; // 小さすぎる範囲は誤操作として無視
  }
  regions.push({
    x: rect.x,
    y: rect.y,
    w: rect.w,
    h: rect.h,
    mode: currentMode(),
    strength: currentStrength()
  });
  render();
  renderRegionList();
}

canvas.addEventListener("pointerup", finishDrag);
canvas.addEventListener("pointercancel", () => {
  dragStart = null;
  dragCurrent = null;
  render();
});

// ==========================================
// 📋 範囲リスト
// ==========================================
function renderRegionList() {
  regionList.innerHTML = "";
  regions.forEach((r, i) => {
    const li = document.createElement("li");
    const info = document.createElement("span");
    info.className = "region-info";
    info.textContent = Core.regionInfoText(i, r);
    const del = document.createElement("button");
    del.type = "button";
    del.className = "btn btn-delete";
    del.textContent = "削除";
    del.addEventListener("click", () => {
      regions.splice(i, 1);
      render();
      renderRegionList();
    });
    li.appendChild(info);
    li.appendChild(del);
    regionList.appendChild(li);
  });
  regionCount.textContent = String(regions.length);
  regionEmpty.style.display = regions.length ? "none" : "";
  undoBtn.disabled = regions.length === 0;
  clearRegionsBtn.disabled = regions.length === 0;
}

undoBtn.addEventListener("click", () => {
  regions.pop();
  render();
  renderRegionList();
});

clearRegionsBtn.addEventListener("click", () => {
  regions = [];
  render();
  renderRegionList();
});

// ==========================================
// 💾 PNG書き出し
// ==========================================
exportBtn.addEventListener("click", () => {
  if (!hasImage) {
    exportMsg.textContent = "まず画像を読み込んでください。";
    return;
  }
  // 🖼️ 枠線なし・元解像度で書き出し用の Canvas に描き直す
  const out = document.createElement("canvas");
  out.width = srcCanvas.width;
  out.height = srcCanvas.height;
  paint(out.getContext("2d"), out.width, out.height, false);
  out.toBlob((blob) => {
    if (!blob) {
      exportMsg.textContent = "書き出しに失敗しました。";
      return;
    }
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = Core.exportFileName(imageName);
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 4000);
    exportMsg.textContent = "PNGをダウンロードしました。";
  }, "image/png");
});

// ==========================================
// 🚀 初期化
// ==========================================
strengthValue.textContent = String(currentStrength());
renderRegionList();

// ==========================================
// 🌙 テーマ（ダーク既定＋ライト切り替え）
// 保存キーは localStorage の "mozaya-theme" のみ。それ以外の状態は保存しない。
// ==========================================
(function initTheme() {
  var root = document.documentElement;
  if (!root || !root.getAttribute || !root.setAttribute) return;
  function apply(theme) {
    root.setAttribute("data-theme", theme);
    var btn = document.getElementById("themeToggle");
    if (btn) btn.textContent = theme === "light" ? "☀️" : "🌙";
  }
  var current = root.getAttribute("data-theme");
  if (current !== "light" && current !== "dark") {
    try { current = localStorage.getItem("mozaya-theme"); } catch (e) { current = null; }
  }
  apply(current === "light" ? "light" : "dark");
  var btn = document.getElementById("themeToggle");
  if (btn && btn.addEventListener) {
    btn.addEventListener("click", function () {
      var next = root.getAttribute("data-theme") === "light" ? "dark" : "light";
      apply(next);
      try { localStorage.setItem("mozaya-theme", next); } catch (e) { /* 保存できなくても切り替え自体は有効 */ }
    });
  }
})();

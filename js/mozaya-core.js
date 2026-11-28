"use strict";

// ==========================================
// 🧩 もざや コアロジック（DOM 非依存）
// 加工パラメータの換算・矩形計算・座標変換など、ブラウザでも Node でも
// 同じ結果になる純粋関数だけをここに置く。app.js は画面側の配線に徹し、
// 計算はすべてこのモジュールへ寄せる（単体テストで仕様を固定するため）。
// ==========================================

(function (global) {
  // 📥 読み込みを許可する画像形式（PNG / JPG のみ）
  const ACCEPTED_TYPES = ["image/png", "image/jpeg"];

  // 🎚️ 強さスライダーの範囲と既定値（index.html の range と一致させる）
  const STRENGTH_MIN = 4;
  const STRENGTH_MAX = 40;
  const STRENGTH_DEFAULT = 16;

  // 🖱️ これ未満のドラッグは誤操作として無視する（元画像ピクセル）
  const MIN_REGION_SIZE = 4;

  // 🎚️ スライダーの生値を強さとして解釈する。不正な値は既定値 16。
  function parseStrength(value) {
    const n = Number.parseInt(value, 10);
    return Number.isFinite(n) ? n : STRENGTH_DEFAULT;
  }

  // 🧱 モザイクのブロック一辺（元画像ピクセル）。最低でも 2。
  function mosaicBlockSize(strength) {
    return Math.max(2, strength);
  }

  // 🧱 モザイク用に範囲を縮小したときのピクセル数。
  // 範囲を block 単位で割り、端数は切り上げる（最低 1×1）。
  function mosaicSmallSize(w, h, strength) {
    const block = mosaicBlockSize(strength);
    return {
      smallW: Math.max(1, Math.ceil(w / block)),
      smallH: Math.max(1, Math.ceil(h / block))
    };
  }

  // 🌀 ぼかしの半径換算：強さの半分（四捨五入、最低 1）。
  // Web 版は Canvas の blur フィルター半径にそのまま使う。
  function blurRadius(strength) {
    return Math.max(1, Math.round(strength / 2));
  }

  // 🌀 Windows 版ぼかしの下準備：半径で割って縮小したときのピクセル数。
  function blurSmallSize(w, h, strength) {
    const radius = blurRadius(strength);
    return {
      smallW: Math.max(1, Math.floor(w / radius)),
      smallH: Math.max(1, Math.floor(h / radius))
    };
  }

  // 📐 2点を左上・右下にそろえて矩形にする
  function normalizeRect(a, b) {
    const x = Math.min(a.x, b.x);
    const y = Math.min(a.y, b.y);
    return { x: x, y: y, w: Math.abs(a.x - b.x), h: Math.abs(a.y - b.y) };
  }

  // ✅ ドラッグで確定した矩形が、範囲として採用できる大きさかどうか
  function isRegionBigEnough(rect) {
    return rect.w >= MIN_REGION_SIZE && rect.h >= MIN_REGION_SIZE;
  }

  // 🔁 表示座標（CSSピクセル）→ 元画像座標への変換。
  // rect は Canvas の表示矩形（getBoundingClientRect の left/top/width/height）、
  // canvasWidth/canvasHeight は Canvas の実ピクセル数。
  // 結果は画像の範囲内に収め、整数へ丸める。
  function displayToImage(clientX, clientY, rect, canvasWidth, canvasHeight) {
    const scaleX = canvasWidth / rect.width;
    const scaleY = canvasHeight / rect.height;
    const x = (clientX - rect.left) * scaleX;
    const y = (clientY - rect.top) * scaleY;
    return {
      x: Math.max(0, Math.min(canvasWidth, Math.round(x))),
      y: Math.max(0, Math.min(canvasHeight, Math.round(y)))
    };
  }

  // 🖼️ 範囲ガイド枠の線幅（画像幅に応じて決める）
  function guideLineWidth(imageWidth) {
    return Math.max(2, Math.round(imageWidth / 400));
  }

  // 📂 ファイル形式が読み込み対象かどうか
  function isAcceptedType(type) {
    return ACCEPTED_TYPES.indexOf(type) !== -1;
  }

  // 🏷️ 加工モードの表示名
  function modeLabel(mode) {
    return mode === "mosaic" ? "モザイク" : "ぼかし";
  }

  // 📋 範囲リストの1行分テキスト（Web / Windows で同じ書式）
  function regionInfoText(index, region) {
    return (
      (index + 1) + ". " + modeLabel(region.mode) + "（強さ " + region.strength + "）" +
      " x:" + region.x + " y:" + region.y + " 幅:" + region.w + " 高さ:" + region.h
    );
  }

  // 💾 書き出しファイル名：元の名前から拡張子を除き「-mozaya.png」を付ける
  function exportFileName(imageName) {
    const base = String(imageName || "").replace(/\.[^.]+$/, "") || "image";
    return base + "-mozaya.png";
  }

  // 🧮 矩形範囲内のピクセル平均（RGBA、data は行優先の配列状データ）。
  // モザイク1ブロック分の色の定義：縮小描画は元範囲の平均色で塗ることに等しい。
  // 範囲は画像内に収まる前提で、はみ出しは切り取る。
  function blockAverage(data, imageWidth, imageHeight, rx, ry, rw, rh) {
    const x0 = Math.max(0, rx);
    const y0 = Math.max(0, ry);
    const x1 = Math.min(imageWidth, rx + rw);
    const y1 = Math.min(imageHeight, ry + rh);
    let r = 0, g = 0, b = 0, a = 0, count = 0;
    for (let y = y0; y < y1; y++) {
      for (let x = x0; x < x1; x++) {
        const off = (y * imageWidth + x) * 4;
        r += data[off];
        g += data[off + 1];
        b += data[off + 2];
        a += data[off + 3];
        count++;
      }
    }
    if (count === 0) return { r: 0, g: 0, b: 0, a: 0 };
    return { r: r / count, g: g / count, b: b / count, a: a / count };
  }

  // 🧮 範囲をモザイクのブロック格子に分け、各ブロックの平均色を返す。
  // 格子の数（smallW × smallH）は mosaicSmallSize と同じで、
  // ブロック境界は範囲を等分した位置に取る。
  function mosaicBlocks(data, imageWidth, imageHeight, region) {
    const size = mosaicSmallSize(region.w, region.h, region.strength);
    const blocks = [];
    for (let j = 0; j < size.smallH; j++) {
      for (let i = 0; i < size.smallW; i++) {
        const bx = region.x + Math.floor((i * region.w) / size.smallW);
        const bw = Math.floor(((i + 1) * region.w) / size.smallW) - Math.floor((i * region.w) / size.smallW);
        const by = region.y + Math.floor((j * region.h) / size.smallH);
        const bh = Math.floor(((j + 1) * region.h) / size.smallH) - Math.floor((j * region.h) / size.smallH);
        blocks.push({
          x: bx,
          y: by,
          w: bw,
          h: bh,
          color: blockAverage(data, imageWidth, imageHeight, bx, by, bw, bh)
        });
      }
    }
    return blocks;
  }

  const MozayaCore = {
    ACCEPTED_TYPES: ACCEPTED_TYPES,
    STRENGTH_MIN: STRENGTH_MIN,
    STRENGTH_MAX: STRENGTH_MAX,
    STRENGTH_DEFAULT: STRENGTH_DEFAULT,
    MIN_REGION_SIZE: MIN_REGION_SIZE,
    parseStrength: parseStrength,
    mosaicBlockSize: mosaicBlockSize,
    mosaicSmallSize: mosaicSmallSize,
    blurRadius: blurRadius,
    blurSmallSize: blurSmallSize,
    normalizeRect: normalizeRect,
    isRegionBigEnough: isRegionBigEnough,
    displayToImage: displayToImage,
    guideLineWidth: guideLineWidth,
    isAcceptedType: isAcceptedType,
    modeLabel: modeLabel,
    regionInfoText: regionInfoText,
    exportFileName: exportFileName,
    blockAverage: blockAverage,
    mosaicBlocks: mosaicBlocks
  };

  if (typeof module !== "undefined" && module.exports) {
    module.exports = MozayaCore;
  }
  global.MozayaCore = MozayaCore;
})(typeof window !== "undefined" ? window : globalThis);

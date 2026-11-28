"use strict";

// ==========================================
// 🧪 もざや コアロジックの単体テスト（Node 標準の test ランナー、依存なし）
// 加工パラメータの換算・ブロック平均・座標変換・書き出し寸法の仕様を固定する。
// ==========================================

const test = require("node:test");
const assert = require("node:assert/strict");
const Core = require("../js/mozaya-core.js");

// 🎨 合成ピクセルデータを作る：幅 w・高さ h、画素値は fill(x, y) → [r,g,b,a]
function makePixels(w, h, fill) {
  const data = new Uint8ClampedArray(w * h * 4);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const [r, g, b, a] = fill(x, y);
      const off = (y * w + x) * 4;
      data[off] = r;
      data[off + 1] = g;
      data[off + 2] = b;
      data[off + 3] = a;
    }
  }
  return data;
}

// ---------- 🎚️ 強さの解釈 ----------

test("parseStrength: 整数文字列をそのまま返す", () => {
  assert.equal(Core.parseStrength("16"), 16);
  assert.equal(Core.parseStrength("4"), 4);
  assert.equal(Core.parseStrength("40"), 40);
  assert.equal(Core.parseStrength("24"), 24);
});

test("parseStrength: 不正な値は既定値 16", () => {
  assert.equal(Core.parseStrength(""), 16);
  assert.equal(Core.parseStrength("abc"), 16);
  assert.equal(Core.parseStrength(undefined), 16);
  assert.equal(Core.parseStrength(null), 16);
});

test("強さの範囲定数はスライダー仕様（4〜40、既定16）と一致する", () => {
  assert.equal(Core.STRENGTH_MIN, 4);
  assert.equal(Core.STRENGTH_MAX, 40);
  assert.equal(Core.STRENGTH_DEFAULT, 16);
});

// ---------- 🧱 モザイクのパラメータ換算 ----------

test("mosaicBlockSize: 強さがそのままブロック一辺、最低 2", () => {
  assert.equal(Core.mosaicBlockSize(16), 16);
  assert.equal(Core.mosaicBlockSize(4), 4);
  assert.equal(Core.mosaicBlockSize(40), 40);
  assert.equal(Core.mosaicBlockSize(1), 2);
  assert.equal(Core.mosaicBlockSize(0), 2);
});

test("mosaicSmallSize: 範囲をブロックで割り切り上げ、最低 1", () => {
  assert.deepEqual(Core.mosaicSmallSize(160, 80, 16), { smallW: 10, smallH: 5 });
  assert.deepEqual(Core.mosaicSmallSize(100, 50, 16), { smallW: 7, smallH: 4 }); // 端数は切り上げ
  assert.deepEqual(Core.mosaicSmallSize(10, 10, 40), { smallW: 1, smallH: 1 }); // 範囲より大きいブロックでも 1
  assert.deepEqual(Core.mosaicSmallSize(33, 17, 4), { smallW: 9, smallH: 5 });
});

// ---------- 🌀 ぼかしのパラメータ換算 ----------

test("blurRadius: 強さの半分（四捨五入、最低 1）", () => {
  assert.equal(Core.blurRadius(16), 8);
  assert.equal(Core.blurRadius(4), 2);
  assert.equal(Core.blurRadius(40), 20);
  assert.equal(Core.blurRadius(5), 3); // 2.5 → 3
  assert.equal(Core.blurRadius(7), 4); // 3.5 → 4
  assert.equal(Core.blurRadius(1), 1);
  assert.equal(Core.blurRadius(0), 1);
});

test("blurSmallSize: Windows 版の下準備として半径で割って切り捨て、最低 1", () => {
  assert.deepEqual(Core.blurSmallSize(160, 80, 16), { smallW: 20, smallH: 10 }); // 半径 8
  assert.deepEqual(Core.blurSmallSize(100, 50, 40), { smallW: 5, smallH: 2 }); // 半径 20、端数は切り捨て
  assert.deepEqual(Core.blurSmallSize(10, 10, 40), { smallW: 1, smallH: 1 });
});

// ---------- 🧮 ブロック平均（モザイクの色の定義） ----------

test("blockAverage: 一様な色の範囲はその色の平均になる", () => {
  const data = makePixels(8, 8, () => [200, 100, 50, 255]);
  assert.deepEqual(Core.blockAverage(data, 8, 8, 0, 0, 8, 8), { r: 200, g: 100, b: 50, a: 255 });
  assert.deepEqual(Core.blockAverage(data, 8, 8, 2, 3, 4, 2), { r: 200, g: 100, b: 50, a: 255 });
});

test("blockAverage: 市松模様の平均は中間色になる", () => {
  const data = makePixels(4, 4, (x, y) => ((x + y) % 2 === 0 ? [0, 0, 0, 255] : [255, 255, 255, 255]));
  const avg = Core.blockAverage(data, 4, 4, 0, 0, 4, 4);
  assert.equal(avg.r, 127.5);
  assert.equal(avg.g, 127.5);
  assert.equal(avg.b, 127.5);
  assert.equal(avg.a, 255);
});

test("blockAverage: 範囲の一部だけを平均する", () => {
  // 左半分が黒、右半分が白。左上 2x2 は黒のみ、横断する 4x2 は半々。
  const data = makePixels(4, 4, (x) => (x < 2 ? [0, 0, 0, 255] : [255, 255, 255, 255]));
  assert.equal(Core.blockAverage(data, 4, 4, 0, 0, 2, 2).r, 0);
  assert.equal(Core.blockAverage(data, 4, 4, 2, 0, 2, 2).r, 255);
  assert.equal(Core.blockAverage(data, 4, 4, 0, 0, 4, 2).r, 127.5);
});

test("blockAverage: 空の範囲は 0 を返す", () => {
  const data = makePixels(4, 4, () => [10, 20, 30, 255]);
  assert.deepEqual(Core.blockAverage(data, 4, 4, 1, 1, 0, 0), { r: 0, g: 0, b: 0, a: 0 });
});

test("mosaicBlocks: 範囲がブロック格子に分かれ、各ブロックが平均色を持つ", () => {
  // 8x8、左半分が赤・右半分が青。強さ 4 → ブロック 4 → 縮小 2x2。
  const data = makePixels(8, 8, (x) => (x < 4 ? [255, 0, 0, 255] : [0, 0, 255, 255]));
  const blocks = Core.mosaicBlocks(data, 8, 8, { x: 0, y: 0, w: 8, h: 8, strength: 4 });
  assert.equal(blocks.length, 4);
  assert.deepEqual(
    blocks.map((b) => [b.x, b.y, b.w, b.h]),
    [
      [0, 0, 4, 4],
      [4, 0, 4, 4],
      [0, 4, 4, 4],
      [4, 4, 4, 4]
    ]
  );
  assert.equal(blocks[0].color.r, 255);
  assert.equal(blocks[0].color.b, 0);
  assert.equal(blocks[1].color.r, 0);
  assert.equal(blocks[1].color.b, 255);
});

test("mosaicBlocks: モザイクをかけると元の画素差が消える（ブロック内一様化の定義）", () => {
  // 4x4 の市松模様に強さ 4（ブロック 4 = 範囲全体で1ブロック）→ 全体が平均色 127.5 になる。
  const data = makePixels(4, 4, (x, y) => ((x + y) % 2 === 0 ? [0, 0, 0, 255] : [255, 255, 255, 255]));
  const blocks = Core.mosaicBlocks(data, 4, 4, { x: 0, y: 0, w: 4, h: 4, strength: 4 });
  assert.equal(blocks.length, 1);
  assert.equal(blocks[0].color.r, 127.5);
  // 元データには 0 と 255 の画素しかない＝平均色は「変わった」値であること
  assert.notEqual(blocks[0].color.r, 0);
  assert.notEqual(blocks[0].color.r, 255);
});

test("mosaicBlocks: 画像の途中にある範囲でも座標がずれない", () => {
  const data = makePixels(16, 16, (x, y) => [x * 16, y * 16, 0, 255]);
  const blocks = Core.mosaicBlocks(data, 16, 16, { x: 8, y: 8, w: 8, h: 8, strength: 8 });
  assert.equal(blocks.length, 1);
  assert.equal(blocks[0].x, 8);
  assert.equal(blocks[0].y, 8);
  // x が 8..15 → r の平均は (8+…+15)/8*16 = 184、y も同様
  assert.equal(blocks[0].color.r, 184);
  assert.equal(blocks[0].color.g, 184);
});

// ---------- 📐 矩形とドラッグ判定 ----------

test("normalizeRect: ドラッグ方向によらず左上・右下にそろう", () => {
  assert.deepEqual(Core.normalizeRect({ x: 10, y: 20 }, { x: 30, y: 50 }), { x: 10, y: 20, w: 20, h: 30 });
  assert.deepEqual(Core.normalizeRect({ x: 30, y: 50 }, { x: 10, y: 20 }), { x: 10, y: 20, w: 20, h: 30 });
  assert.deepEqual(Core.normalizeRect({ x: 30, y: 20 }, { x: 10, y: 50 }), { x: 10, y: 20, w: 20, h: 30 });
  assert.deepEqual(Core.normalizeRect({ x: 5, y: 5 }, { x: 5, y: 5 }), { x: 5, y: 5, w: 0, h: 0 });
});

test("isRegionBigEnough: 幅・高さとも 4 以上で採用、未満は誤操作として無視", () => {
  assert.equal(Core.isRegionBigEnough({ w: 4, h: 4 }), true);
  assert.equal(Core.isRegionBigEnough({ w: 100, h: 4 }), true);
  assert.equal(Core.isRegionBigEnough({ w: 3, h: 100 }), false);
  assert.equal(Core.isRegionBigEnough({ w: 100, h: 3 }), false);
  assert.equal(Core.isRegionBigEnough({ w: 0, h: 0 }), false);
  assert.equal(Core.MIN_REGION_SIZE, 4);
});

// ---------- 🔁 座標変換 ----------

test("displayToImage: 等倍表示では表示座標がそのまま画像座標になる", () => {
  const rect = { left: 0, top: 0, width: 640, height: 400 };
  assert.deepEqual(Core.displayToImage(100, 50, rect, 640, 400), { x: 100, y: 50 });
});

test("displayToImage: 縮小表示では倍率で割り戻し、四捨五入する", () => {
  // 実画像 1200x800 を 600x400 で表示（0.5倍）→ 表示 10px は画像 20px
  const rect = { left: 0, top: 0, width: 600, height: 400 };
  assert.deepEqual(Core.displayToImage(10, 10, rect, 1200, 800), { x: 20, y: 20 });
  assert.deepEqual(Core.displayToImage(11, 11, rect, 1200, 800), { x: 22, y: 22 });
  // 表示オフセット（ページ内の位置）を引いてから換算する
  const offset = { left: 100, top: 40, width: 600, height: 400 };
  assert.deepEqual(Core.displayToImage(110, 50, offset, 1200, 800), { x: 20, y: 20 });
});

test("displayToImage: 画像の外にはみ出した座標は端に収める", () => {
  const rect = { left: 0, top: 0, width: 600, height: 400 };
  assert.deepEqual(Core.displayToImage(-50, -50, rect, 1200, 800), { x: 0, y: 0 });
  assert.deepEqual(Core.displayToImage(9999, 9999, rect, 1200, 800), { x: 1200, y: 800 });
});

// ---------- 🖼️ ガイド枠・書き出し ----------

test("guideLineWidth: 画像幅の 1/400、最低 2", () => {
  assert.equal(Core.guideLineWidth(400), 2);
  assert.equal(Core.guideLineWidth(100), 2);
  assert.equal(Core.guideLineWidth(1200), 3);
  assert.equal(Core.guideLineWidth(4000), 10);
});

test("exportFileName: 拡張子を除いて -mozaya.png を付ける", () => {
  assert.equal(Core.exportFileName("photo.png"), "photo-mozaya.png");
  assert.equal(Core.exportFileName("photo.jpg"), "photo-mozaya.png");
  assert.equal(Core.exportFileName("my.photo.jpeg"), "my.photo-mozaya.png");
  assert.equal(Core.exportFileName("noext"), "noext-mozaya.png");
  assert.equal(Core.exportFileName(""), "image-mozaya.png");
});

test("書き出し寸法の仕様：書き出しは元画像と同じ解像度（表示倍率に依存しない）", () => {
  // paint は srcCanvas の寸法で書き出す。ここでは「表示が半分でも書き出し座標系は
  // 元画像のまま」であることを、座標変換が元画像寸法を上限にすることから固定する。
  const rect = { left: 0, top: 0, width: 320, height: 200 }; // 640x400 を半分で表示
  const p = Core.displayToImage(320, 200, rect, 640, 400);
  assert.deepEqual(p, { x: 640, y: 400 }); // 右下端は元画像の右下端に一致する
});

// ---------- 📂 形式判定と表示名 ----------

test("isAcceptedType: PNG / JPG のみ許可する", () => {
  assert.equal(Core.isAcceptedType("image/png"), true);
  assert.equal(Core.isAcceptedType("image/jpeg"), true);
  assert.equal(Core.isAcceptedType("image/gif"), false);
  assert.equal(Core.isAcceptedType("image/webp"), false);
  assert.equal(Core.isAcceptedType(""), false);
  assert.deepEqual(Core.ACCEPTED_TYPES, ["image/png", "image/jpeg"]);
});

test("modeLabel: モードの表示名", () => {
  assert.equal(Core.modeLabel("mosaic"), "モザイク");
  assert.equal(Core.modeLabel("blur"), "ぼかし");
});

test("regionInfoText: 範囲リストの書式（Web / Windows 共通）", () => {
  assert.equal(
    Core.regionInfoText(0, { mode: "mosaic", strength: 16, x: 10, y: 20, w: 300, h: 200 }),
    "1. モザイク（強さ 16） x:10 y:20 幅:300 高さ:200"
  );
  assert.equal(
    Core.regionInfoText(2, { mode: "blur", strength: 40, x: 0, y: 0, w: 4, h: 4 }),
    "3. ぼかし（強さ 40） x:0 y:0 幅:4 高さ:4"
  );
});

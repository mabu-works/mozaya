using System;
using System.Drawing;
using Mozaya;

// ==========================================
// 🧪 MozayaLogic の単体テスト（依存なしのコンソールランナー）
// Web 版（js/mozaya-core.js）のテストと同じ仕様を C# 側でも固定する。
// ==========================================

int passed = 0;
int failed = 0;

void Check(string name, bool cond)
{
    if (cond) { passed++; }
    else { failed++; Console.WriteLine("FAIL: " + name); }
}

void Eq<T>(string name, T expected, T actual)
{
    Check($"{name} (expected={expected}, actual={actual})", Equals(expected, actual));
}

// ---------- 🧱 モザイクのパラメータ換算 ----------

Eq("MosaicBlockSize(16)", 16, MozayaLogic.MosaicBlockSize(16));
Eq("MosaicBlockSize(1) は最低 2", 2, MozayaLogic.MosaicBlockSize(1));
Eq("MosaicSmallSize(160,80,16)", (10, 5), MozayaLogic.MosaicSmallSize(160, 80, 16));
Eq("MosaicSmallSize(100,50,16) は切り上げ", (7, 4), MozayaLogic.MosaicSmallSize(100, 50, 16));
Eq("MosaicSmallSize(10,10,40) は最低 1", (1, 1), MozayaLogic.MosaicSmallSize(10, 10, 40));

// ---------- 🌀 ぼかしのパラメータ換算 ----------

Eq("BlurRadius(16)", 8, MozayaLogic.BlurRadius(16));
Eq("BlurRadius(4)", 2, MozayaLogic.BlurRadius(4));
Eq("BlurRadius(40)", 20, MozayaLogic.BlurRadius(40));
// .NET の Math.Round は銀行丸め（.5 は偶数側）で、旧 Windows 版もこの結果だった。
// Web 版（JS の Math.round は .5 を切り上げ）とは強さ 5・9・17・25・33 で半径が 1 違う。
// 旧 Windows 版とのパリティを優先し、この癖をそのまま固定する。
Eq("BlurRadius(5) は銀行丸めで 2（旧 Windows 版どおり）", 2, MozayaLogic.BlurRadius(5));
Eq("BlurRadius(7) は銀行丸めで 4", 4, MozayaLogic.BlurRadius(7));
Eq("BlurRadius(9) は銀行丸めで 4（旧 Windows 版どおり）", 4, MozayaLogic.BlurRadius(9));
Eq("BlurRadius(0) は最低 1", 1, MozayaLogic.BlurRadius(0));
Eq("BlurSmallSize(160,80,16)", (20, 10), MozayaLogic.BlurSmallSize(160, 80, 16));
Eq("BlurSmallSize(100,50,40) は切り捨て", (5, 2), MozayaLogic.BlurSmallSize(100, 50, 40));
Eq("BlurSmallSize(10,10,40) は最低 1", (1, 1), MozayaLogic.BlurSmallSize(10, 10, 40));

// ---------- 📐 矩形とドラッグ判定 ----------

Eq("NormalizeRect 左上→右下",
    new Rectangle(10, 20, 20, 30),
    MozayaLogic.NormalizeRect(new Point(10, 20), new Point(30, 50)));
Eq("NormalizeRect 右下→左上でも同じ",
    new Rectangle(10, 20, 20, 30),
    MozayaLogic.NormalizeRect(new Point(30, 50), new Point(10, 20)));
Check("IsRegionBigEnough(4x4)", MozayaLogic.IsRegionBigEnough(new Rectangle(0, 0, 4, 4)));
Check("IsRegionBigEnough(3x100) は不採用", !MozayaLogic.IsRegionBigEnough(new Rectangle(0, 0, 3, 100)));
Check("IsRegionBigEnough(100x3) は不採用", !MozayaLogic.IsRegionBigEnough(new Rectangle(0, 0, 100, 3)));
Eq("MinRegionSize", 4, MozayaLogic.MinRegionSize);

// ---------- 📋 表示テキストと書き出し名 ----------

Eq("ModeLabel(mosaic)", "モザイク", MozayaLogic.ModeLabel(true));
Eq("ModeLabel(blur)", "ぼかし", MozayaLogic.ModeLabel(false));
Eq("RegionInfoText",
    "1. モザイク（強さ 16） x:10 y:20 幅:300 高さ:200",
    MozayaLogic.RegionInfoText(0, new Region { Rect = new Rectangle(10, 20, 300, 200), Mosaic = true, Strength = 16 }));
Eq("RegionInfoText blur",
    "3. ぼかし（強さ 40） x:0 y:0 幅:4 高さ:4",
    MozayaLogic.RegionInfoText(2, new Region { Rect = new Rectangle(0, 0, 4, 4), Mosaic = false, Strength = 40 }));
Eq("ExportBaseName(photo.png)", "photo", MozayaLogic.ExportBaseName("photo.png"));
Eq("ExportBaseName(my.photo.jpeg)", "my.photo", MozayaLogic.ExportBaseName("my.photo.jpeg"));
Eq("ExportBaseName(空) は image", "image", MozayaLogic.ExportBaseName(""));

// ---------- 🌀 ボックスブラー ----------

// 一様な画像はブラーしても変わらない
{
    int w = 6, h = 6, stride = w * 4;
    var buf = new byte[stride * h];
    for (int i = 0; i < buf.Length; i += 4) { buf[i] = 30; buf[i + 1] = 60; buf[i + 2] = 90; buf[i + 3] = 255; }
    MozayaLogic.BoxBlur(buf, w, h, stride, 2);
    bool same = true;
    for (int i = 0; i < buf.Length; i += 4)
        if (buf[i] != 30 || buf[i + 1] != 60 || buf[i + 2] != 90 || buf[i + 3] != 255) same = false;
    Check("BoxBlur: 一様な画像は不変", same);
}

// 市松模様（1px）はブラーで隣接差が消える方向に変わる
{
    int w = 8, h = 8, stride = w * 4;
    var buf = new byte[stride * h];
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            byte v = (byte)(((x + y) % 2 == 0) ? 0 : 255);
            int off = y * stride + x * 4;
            buf[off] = v; buf[off + 1] = v; buf[off + 2] = v; buf[off + 3] = 255;
        }
    MozayaLogic.BoxBlur(buf, w, h, stride, 2);
    int center = buf[(4 * stride + 4 * 4)];
    Check($"BoxBlur: 市松模様の中心は中間色になる (actual={center})", center > 100 && center < 155);
    int maxDiff = 0;
    for (int y = 3; y < 5; y++)
        for (int x = 3; x < 4; x++)
        {
            int a = buf[y * stride + x * 4], b = buf[y * stride + (x + 1) * 4];
            maxDiff = Math.Max(maxDiff, Math.Abs(a - b));
        }
    Check($"BoxBlur: 隣接差が 255 から下がる (actual={maxDiff})", maxDiff < 255);
}

// 左半分が黒・右半分が白：境界付近だけが混ざり、遠方は元の色を保つ
{
    int w = 16, h = 4, stride = w * 4;
    var buf = new byte[stride * h];
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            byte v = (byte)(x < 8 ? 0 : 255);
            int off = y * stride + x * 4;
            buf[off] = v; buf[off + 1] = v; buf[off + 2] = v; buf[off + 3] = 255;
        }
    MozayaLogic.BoxBlur(buf, w, h, stride, 2);
    Eq("BoxBlur: 境界から遠い左端は黒のまま", (byte)0, buf[0]);
    Eq("BoxBlur: 境界から遠い右端は白のまま", (byte)255, buf[15 * 4]);
    int edge = buf[7 * 4];
    Check($"BoxBlur: 境界の画素は中間の値になる (actual={edge})", edge > 0 && edge < 255);
}

// radius < 1 では何もしない
{
    var buf = new byte[] { 1, 2, 3, 255, 200, 100, 50, 255 };
    var copy = (byte[])buf.Clone();
    MozayaLogic.BoxBlur(buf, 2, 1, 8, 0);
    bool same = true;
    for (int i = 0; i < buf.Length; i++) if (buf[i] != copy[i]) same = false;
    Check("BoxBlur: radius 0 は無変化", same);
}

Console.WriteLine($"Windows logic tests: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

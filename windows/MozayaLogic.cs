using System;
using System.Drawing;

namespace Mozaya
{
    // 🗂️ 加工範囲（座標は元画像のピクセル単位）。追加時点の種類と強さを保持する
    internal sealed class Region
    {
        public Rectangle Rect;
        public bool Mosaic;
        public int Strength;
    }

    // 🧩 純粋ロジック（画面・ビットマップに依存しない計算だけを集約）
    // Web 版の js/mozaya-core.js と同じ仕様。単体テスト（windows/tests/）で固定する。
    internal static class MozayaLogic
    {
        // 🖱️ これ未満のドラッグは誤操作として無視する（元画像ピクセル）
        public const int MinRegionSize = 4;

        // 🧱 モザイクのブロック一辺（元画像ピクセル）。最低でも 2。
        public static int MosaicBlockSize(int strength)
        {
            return Math.Max(2, strength);
        }

        // 🧱 モザイク用に範囲を縮小したときのピクセル数（端数は切り上げ、最低 1）
        public static (int SmallW, int SmallH) MosaicSmallSize(int w, int h, int strength)
        {
            int block = MosaicBlockSize(strength);
            return (
                Math.Max(1, (int)Math.Ceiling((double)w / block)),
                Math.Max(1, (int)Math.Ceiling((double)h / block)));
        }

        // 🌀 ぼかしの半径換算：強さの半分（最低 1）。
        // Math.Round は銀行丸めのため、.5 になる強さでは Web 版（JS）と半径が 1 違う
        // 場合がある。旧 Windows 版の結果と一致させるため、この丸めをそのまま使う。
        public static int BlurRadius(int strength)
        {
            return Math.Max(1, (int)Math.Round(strength / 2.0));
        }

        // 🌀 ぼかしの下準備：半径で割って縮小したときのピクセル数（切り捨て、最低 1）
        public static (int SmallW, int SmallH) BlurSmallSize(int w, int h, int strength)
        {
            int radius = BlurRadius(strength);
            return (Math.Max(1, w / radius), Math.Max(1, h / radius));
        }

        // 📐 2点を左上・右下にそろえて矩形にする
        public static Rectangle NormalizeRect(Point a, Point b)
        {
            int x = Math.Min(a.X, b.X);
            int y = Math.Min(a.Y, b.Y);
            return new Rectangle(x, y, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        // ✅ ドラッグで確定した矩形が、範囲として採用できる大きさかどうか
        public static bool IsRegionBigEnough(Rectangle rect)
        {
            return rect.Width >= MinRegionSize && rect.Height >= MinRegionSize;
        }

        // 🏷️ 加工モードの表示名
        public static string ModeLabel(bool mosaic)
        {
            return mosaic ? "モザイク" : "ぼかし";
        }

        // 📋 範囲リストの1行分テキスト（Web 版と同じ書式）
        public static string RegionInfoText(int index, Region region)
        {
            return $"{index + 1}. {ModeLabel(region.Mosaic)}（強さ {region.Strength}）" +
                   $" x:{region.Rect.X} y:{region.Rect.Y} 幅:{region.Rect.Width} 高さ:{region.Rect.Height}";
        }

        // 💾 書き出しの基本名：元のファイル名から拡張子を除いたもの（空なら "image"）
        public static string ExportBaseName(string imageName)
        {
            string baseName = System.IO.Path.GetFileNameWithoutExtension(imageName ?? "");
            return string.IsNullOrEmpty(baseName) ? "image" : baseName;
        }

        // 🌀 簡易ボックスブラー（水平→垂直の分離型、端はクランプ）。
        // buf は BGRA 4バイト/画素のバッファ、stride は1行分のバイト数。
        public static void BoxBlur(byte[] buf, int width, int height, int stride, int radius)
        {
            if (radius < 1 || width < 1 || height < 1) return;
            var tmp = new byte[buf.Length];

            // ↔ 水平方向
            for (int y = 0; y < height; y++)
            {
                int rowOff = y * stride;
                for (int x = 0; x < width; x++)
                {
                    long b = 0, g = 0, r = 0, a = 0;
                    int count = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sx = Math.Max(0, Math.Min(width - 1, x + k));
                        int off = rowOff + sx * 4;
                        b += buf[off]; g += buf[off + 1]; r += buf[off + 2]; a += buf[off + 3];
                        count++;
                    }
                    int o = rowOff + x * 4;
                    tmp[o] = (byte)(b / count); tmp[o + 1] = (byte)(g / count);
                    tmp[o + 2] = (byte)(r / count); tmp[o + 3] = (byte)(a / count);
                }
            }

            // ↕ 垂直方向（水平の結果から元バッファへ戻す）
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    long b = 0, g = 0, r = 0, a = 0;
                    int count = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sy = Math.Max(0, Math.Min(height - 1, y + k));
                        int off = sy * stride + x * 4;
                        b += tmp[off]; g += tmp[off + 1]; r += tmp[off + 2]; a += tmp[off + 3];
                        count++;
                    }
                    int o = y * stride + x * 4;
                    buf[o] = (byte)(b / count); buf[o + 1] = (byte)(g / count);
                    buf[o + 2] = (byte)(r / count); buf[o + 3] = (byte)(a / count);
                }
            }
        }
    }
}

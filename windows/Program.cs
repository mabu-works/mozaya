using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Mozaya
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        // 🎨 配色（Web 版のダークテーマに合わせる）
        private static readonly Color Bg = Color.FromArgb(10, 15, 28);
        private static readonly Color Surface = Color.FromArgb(17, 26, 46);
        private static readonly Color Panel2 = Color.FromArgb(24, 38, 66);
        private static readonly Color TextColor = Color.FromArgb(232, 238, 252);
        private static readonly Color Muted = Color.FromArgb(157, 177, 216);
        private static readonly Color Accent = Color.FromArgb(106, 141, 255);
        private static readonly Color GuideColor = Color.FromArgb(109, 40, 217);

        private readonly Label fileMsg;
        private readonly Label imageInfo;
        private readonly RadioButton mosaicRadio;
        private readonly RadioButton blurRadio;
        private readonly TrackBar strengthBar;
        private readonly Label strengthValue;
        private readonly Panel previewPanel;
        private readonly Label regionCountLabel;
        private readonly ListBox regionList;
        private readonly Button deleteBtn;
        private readonly Button undoBtn;
        private readonly Button clearBtn;
        private readonly Button saveBtn;
        private readonly Label exportMsg;

        private Bitmap? sourceBitmap;
        private Bitmap? processedBitmap;
        private string imageName = "";
        private readonly List<Region> regions = new List<Region>();
        private Point? dragStartImg;
        private Point? dragCurrentImg;

        public MainForm()
        {
            Text = "もざや for Windows｜画像モザイク・ぼかし";
            ClientSize = new Size(1000, 740);
            MinimumSize = new Size(1016, 779);
            BackColor = Bg;
            ForeColor = TextColor;
            Font = new Font("Yu Gothic UI", 10F);
            AllowDrop = true;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;

            var title = new Label
            {
                Text = "もざや｜画像モザイク・ぼかし",
                Font = new Font("Yu Gothic UI", 16F, FontStyle.Bold),
                ForeColor = TextColor,
                Location = new Point(20, 12),
                AutoSize = true
            };
            Controls.Add(title);

            var openBtn = new Button
            {
                Text = "画像を開く",
                BackColor = Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Yu Gothic UI", 10F, FontStyle.Bold),
                Location = new Point(20, 52),
                Size = new Size(150, 34)
            };
            openBtn.FlatAppearance.BorderSize = 0;
            openBtn.Click += (s, e) => OpenFile();
            Controls.Add(openBtn);

            fileMsg = new Label
            {
                Text = "PNG または JPG の画像を選んでください。",
                ForeColor = Muted,
                Location = new Point(184, 60),
                AutoSize = true
            };
            Controls.Add(fileMsg);

            imageInfo = new Label
            {
                Text = "",
                ForeColor = Muted,
                Location = new Point(20, 94),
                AutoSize = true
            };
            Controls.Add(imageInfo);

            var modeLabel = new Label { Text = "加工の種類", ForeColor = Muted, Location = new Point(20, 124), AutoSize = true };
            Controls.Add(modeLabel);
            mosaicRadio = new RadioButton
            {
                Text = "モザイク",
                Checked = true,
                ForeColor = TextColor,
                Location = new Point(106, 122),
                AutoSize = true
            };
            blurRadio = new RadioButton
            {
                Text = "ぼかし",
                ForeColor = TextColor,
                Location = new Point(206, 122),
                AutoSize = true
            };
            Controls.Add(mosaicRadio);
            Controls.Add(blurRadio);

            strengthValue = new Label { Text = "強さ：16", ForeColor = Muted, Location = new Point(300, 124), AutoSize = true };
            Controls.Add(strengthValue);
            strengthBar = new TrackBar
            {
                Minimum = 4,
                Maximum = 40,
                Value = 16,
                TickFrequency = 4,
                Location = new Point(376, 116),
                Width = 264
            };
            strengthBar.ValueChanged += (s, e) => strengthValue.Text = $"強さ：{strengthBar.Value}";
            Controls.Add(strengthBar);

            previewPanel = new Panel
            {
                Location = new Point(20, 156),
                Size = new Size(620, 500),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
                BackColor = Panel2,
                BorderStyle = BorderStyle.FixedSingle,
                AllowDrop = true
            };
            previewPanel.Paint += OnPreviewPaint;
            previewPanel.MouseDown += OnPreviewMouseDown;
            previewPanel.MouseMove += OnPreviewMouseMove;
            previewPanel.MouseUp += OnPreviewMouseUp;
            previewPanel.DragEnter += OnDragEnter;
            previewPanel.DragDrop += OnDragDrop;
            previewPanel.Resize += (s, e) => previewPanel.Invalidate();
            Controls.Add(previewPanel);

            // ==========================================
            // 📋 右側: 範囲リストと保存
            // ==========================================
            regionCountLabel = new Label
            {
                Text = "加工した範囲（0件）",
                ForeColor = Muted,
                Location = new Point(660, 156),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AutoSize = true
            };
            Controls.Add(regionCountLabel);

            regionList = new ListBox
            {
                Location = new Point(660, 184),
                Size = new Size(320, 296),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Surface,
                ForeColor = TextColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Yu Gothic UI", 9F)
            };
            Controls.Add(regionList);

            deleteBtn = MakeSubButton("削除", 660, 490, 96);
            deleteBtn.Click += (s, e) =>
            {
                int i = regionList.SelectedIndex;
                if (i < 0 || i >= regions.Count) return;
                regions.RemoveAt(i);
                AfterRegionsChanged();
            };
            undoBtn = MakeSubButton("最後を取り消す", 762, 490, 112);
            undoBtn.Click += (s, e) =>
            {
                if (regions.Count == 0) return;
                regions.RemoveAt(regions.Count - 1);
                AfterRegionsChanged();
            };
            clearBtn = MakeSubButton("すべて消す", 880, 490, 100);
            clearBtn.Click += (s, e) =>
            {
                regions.Clear();
                AfterRegionsChanged();
            };
            Controls.Add(deleteBtn);
            Controls.Add(undoBtn);
            Controls.Add(clearBtn);

            saveBtn = new Button
            {
                Text = "画像を保存する",
                BackColor = Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Yu Gothic UI", 11F, FontStyle.Bold),
                Location = new Point(660, 540),
                Size = new Size(320, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Enabled = false
            };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += (s, e) => SaveImage();
            Controls.Add(saveBtn);

            exportMsg = new Label
            {
                Text = "",
                ForeColor = Muted,
                Location = new Point(660, 592),
                Size = new Size(320, 56),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            Controls.Add(exportMsg);

            var note = new Label
            {
                Text = "読み込んだ画像はこのアプリの中でのみ処理され、外部に送信されません。書き出しは元の解像度で、範囲の枠線は含まれません。",
                ForeColor = Muted,
                Location = new Point(20, 676),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
                AutoSize = true
            };
            Controls.Add(note);

            UpdateRegionUi();
        }

        private Button MakeSubButton(string text, int x, int y, int width)
        {
            return new Button
            {
                Text = text,
                BackColor = Surface,
                ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(x, y),
                Size = new Size(width, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Yu Gothic UI", 9F)
            };
        }

        // ==========================================
        // 🖼️ 画像読み込み
        // ==========================================
        // 📂 画像ファイルを開いて読み込む
        private void OpenFile()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "画像ファイル (PNG / JPG)|*.png;*.jpg;*.jpeg|すべてのファイル|*.*"
            };
            if (dialog.ShowDialog(this) == DialogResult.OK) LoadFile(dialog.FileName);
        }

        private void OnDragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void OnDragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                LoadFile(files[0]);
            }
        }

        // 📂 画像ファイルを読み込む（拡張子と実形式の両方を確認する）
        private void LoadFile(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
            {
                fileMsg.Text = "対応していないファイルです。PNG または JPG の画像を選んでください。";
                return;
            }
            Bitmap loaded;
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                using var img = Image.FromStream(stream);
                if (!img.RawFormat.Equals(ImageFormat.Png) && !img.RawFormat.Equals(ImageFormat.Jpeg))
                {
                    fileMsg.Text = "対応していないファイルです。PNG または JPG の画像を選んでください。";
                    return;
                }
                loaded = new Bitmap(img);
            }
            catch (Exception)
            {
                fileMsg.Text = "画像を読み込めませんでした。ファイルが壊れていないか確認してください。";
                return;
            }

            sourceBitmap?.Dispose();
            sourceBitmap = loaded;
            imageName = Path.GetFileName(path);
            regions.Clear();
            dragStartImg = null;
            dragCurrentImg = null;
            RebuildProcessed();
            fileMsg.Text = "画像を読み込みました。";
            imageInfo.Text = $"ファイル名：{imageName}／サイズ：{loaded.Width} × {loaded.Height} px（書き出しも同じ解像度です）";
            saveBtn.Enabled = true;
            UpdateRegionUi();
            previewPanel.Invalidate();
        }

        // ==========================================
        // 🔁 座標変換（プレビュー表示 ↔ 元画像）
        // ==========================================
        // 🔁 プレビュー表示と元画像の対応（収まる倍率と中央寄せの表示矩形）を求める
        private bool TryGetView(out RectangleF dest, out float scale)
        {
            dest = default;
            scale = 1f;
            if (sourceBitmap == null) return false;
            var bounds = previewPanel.ClientRectangle;
            if (bounds.Width < 1 || bounds.Height < 1) return false;
            scale = Math.Min((float)bounds.Width / sourceBitmap.Width, (float)bounds.Height / sourceBitmap.Height);
            float w = sourceBitmap.Width * scale;
            float h = sourceBitmap.Height * scale;
            dest = new RectangleF((bounds.Width - w) / 2f, (bounds.Height - h) / 2f, w, h);
            return true;
        }

        private Point DisplayToImage(Point p)
        {
            if (!TryGetView(out var dest, out float scale) || sourceBitmap == null) return Point.Empty;
            int x = (int)Math.Round((p.X - dest.X) / scale);
            int y = (int)Math.Round((p.Y - dest.Y) / scale);
            x = Math.Max(0, Math.Min(sourceBitmap.Width, x));
            y = Math.Max(0, Math.Min(sourceBitmap.Height, y));
            return new Point(x, y);
        }

        private RectangleF ImageToDisplay(Rectangle r, RectangleF dest, float scale)
        {
            return new RectangleF(
                dest.X + r.X * scale,
                dest.Y + r.Y * scale,
                r.Width * scale,
                r.Height * scale);
        }

        // ==========================================
        // 🖱️ プレビュー描画とドラッグ
        // ==========================================
        // 🖱️ プレビューを描き、ドラッグ中の範囲も表示する
        private void OnPreviewPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            if (processedBitmap == null || !TryGetView(out var dest, out float scale))
            {
                using var brush = new SolidBrush(Muted);
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("画像をここにドラッグ＆ドロップするか、\n「画像を開く」から選んでください", Font, brush, previewPanel.ClientRectangle, format);
                return;
            }
            g.InterpolationMode = scale > 1f ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBilinear;
            g.DrawImage(processedBitmap, dest);

            using var pen = new Pen(GuideColor, 2f) { DashStyle = DashStyle.Dash };
            foreach (var r in regions)
            {
                g.DrawRectangle(pen, ImageToDisplay(r.Rect, dest, scale));
            }
            if (dragStartImg != null && dragCurrentImg != null)
            {
                var rect = MozayaLogic.NormalizeRect(dragStartImg.Value, dragCurrentImg.Value);
                var d = ImageToDisplay(rect, dest, scale);
                using var fill = new SolidBrush(Color.FromArgb(46, GuideColor));
                g.FillRectangle(fill, d);
                g.DrawRectangle(pen, d);
            }
        }

        private void OnPreviewMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (sourceBitmap == null)
            {
                fileMsg.Text = "まず画像を読み込んでください。";
                return;
            }
            dragStartImg = DisplayToImage(e.Location);
            dragCurrentImg = dragStartImg;
            previewPanel.Capture = true;
        }

        private void OnPreviewMouseMove(object? sender, MouseEventArgs e)
        {
            if (dragStartImg == null) return;
            dragCurrentImg = DisplayToImage(e.Location);
            previewPanel.Invalidate();
        }

        private void OnPreviewMouseUp(object? sender, MouseEventArgs e)
        {
            if (dragStartImg == null) return;
            var end = DisplayToImage(e.Location);
            var rect = MozayaLogic.NormalizeRect(dragStartImg.Value, end);
            dragStartImg = null;
            dragCurrentImg = null;
            previewPanel.Capture = false;
            if (!MozayaLogic.IsRegionBigEnough(rect))
            {
                previewPanel.Invalidate();
                return; // 小さすぎる範囲は誤操作として無視（Web版と同じ）
            }
            regions.Add(new Region
            {
                Rect = rect,
                Mosaic = mosaicRadio.Checked,
                Strength = strengthBar.Value
            });
            AfterRegionsChanged();
        }

        private void AfterRegionsChanged()
        {
            RebuildProcessed();
            UpdateRegionUi();
            previewPanel.Invalidate();
        }

        private void UpdateRegionUi()
        {
            regionList.Items.Clear();
            for (int i = 0; i < regions.Count; i++)
            {
                regionList.Items.Add(MozayaLogic.RegionInfoText(i, regions[i]));
            }
            regionCountLabel.Text = $"加工した範囲（{regions.Count}件）";
            undoBtn.Enabled = regions.Count > 0;
            clearBtn.Enabled = regions.Count > 0;
            deleteBtn.Enabled = regions.Count > 0;
        }

        // ==========================================
        // 🎨 加工
        // ==========================================
        // 🎨 毎回もとの画像から描き直すので、加工が重ねがけで劣化しない（Web版と同じ）
        private void RebuildProcessed()
        {
            processedBitmap?.Dispose();
            processedBitmap = null;
            if (sourceBitmap == null) return;
            var output = new Bitmap(sourceBitmap.Width, sourceBitmap.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(output))
            {
                g.DrawImage(sourceBitmap, 0, 0, sourceBitmap.Width, sourceBitmap.Height);
            }
            foreach (var r in regions) ApplyRegion(output, r);
            processedBitmap = output;
        }

        private void ApplyRegion(Bitmap output, Region region)
        {
            if (sourceBitmap == null) return;
            var rect = Rectangle.Intersect(region.Rect, new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height));
            if (rect.Width < 1 || rect.Height < 1) return;

            if (region.Mosaic)
            {
                // 🧱 範囲を小さく縮小してから、スムージングなしで拡大して描く
                var (smallW, smallH) = MozayaLogic.MosaicSmallSize(rect.Width, rect.Height, region.Strength);
                using var small = new Bitmap(smallW, smallH, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(sourceBitmap, new Rectangle(0, 0, smallW, smallH), rect, GraphicsUnit.Pixel);
                }
                using var og = Graphics.FromImage(output);
                og.InterpolationMode = InterpolationMode.NearestNeighbor;
                og.SmoothingMode = SmoothingMode.None;
                og.PixelOffsetMode = PixelOffsetMode.Half;
                og.DrawImage(small, rect);
            }
            else
            {
                // 🌀 ぼかし：縮小 → 簡易ボックスブラー → 高品質で拡大して戻す
                // （Web 版の Canvas フィルターとは計算方法が異なるため、見た目がわずかに違う場合がある）
                var (smallW, smallH) = MozayaLogic.BlurSmallSize(rect.Width, rect.Height, region.Strength);
                using var small = new Bitmap(smallW, smallH, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(sourceBitmap, new Rectangle(0, 0, smallW, smallH), rect, GraphicsUnit.Pixel);
                }
                BoxBlurInPlace(small, 2);
                BoxBlurInPlace(small, 2);
                using var og = Graphics.FromImage(output);
                og.SetClip(rect);
                og.InterpolationMode = InterpolationMode.HighQualityBilinear;
                og.DrawImage(small, rect);
                og.ResetClip();
            }
        }

        // 🌀 ビットマップへロックしてボックスブラーを適用する（計算本体は MozayaLogic）
        private static void BoxBlurInPlace(Bitmap bmp, int radius)
        {
            int w = bmp.Width, h = bmp.Height;
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            int stride = Math.Abs(data.Stride);
            var buf = new byte[stride * h];
            Marshal.Copy(data.Scan0, buf, 0, buf.Length);
            MozayaLogic.BoxBlur(buf, w, h, stride, radius);
            Marshal.Copy(buf, 0, data.Scan0, buf.Length);
            bmp.UnlockBits(data);
        }

        // ==========================================
        // 💾 保存
        // ==========================================
        // 💾 加工済み画像を PNG または JPEG（品質 90）で保存する
        private void SaveImage()
        {
            if (processedBitmap == null || sourceBitmap == null)
            {
                exportMsg.Text = "まず画像を読み込んでください。";
                return;
            }
            using var dialog = new SaveFileDialog
            {
                Filter = "PNG 画像|*.png|JPEG 画像|*.jpg",
                FileName = MozayaLogic.ExportBaseName(imageName) + "-mozaya.png",
                DefaultExt = "png"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            string path = dialog.FileName;
            try
            {
                if (dialog.FilterIndex == 2)
                {
                    if (Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
                        path = Path.ChangeExtension(path, ".jpg");
                    var jpegCodec = Array.Find(ImageCodecInfo.GetImageEncoders(), c => c.FormatID == ImageFormat.Jpeg.Guid);
                    using var param = new EncoderParameters(1);
                    param.Param[0] = new EncoderParameter(Encoder.Quality, 90L);
                    processedBitmap.Save(path, jpegCodec, param);
                    exportMsg.Text = "JPEGを保存しました。";
                }
                else
                {
                    processedBitmap.Save(path, ImageFormat.Png);
                    exportMsg.Text = "PNGを保存しました。";
                }
            }
            catch (Exception)
            {
                exportMsg.Text = "保存に失敗しました。";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                sourceBitmap?.Dispose();
                processedBitmap?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

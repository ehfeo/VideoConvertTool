using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VideoConvertTool
{
    public class MainForm : Form
    {
        // ---------- 控件 ----------
        private Panel dropPanel;
        private Label dropLabel;
        private Button btnBrowse;
        private Button btnClear;
        private ComboBox cbFormat;
        private ComboBox cbVCodec;
        private ComboBox cbPreset;
        private ComboBox cbMode;        // CRF / 码率
        private Label lblModeHint;   // 模式说明（CRF / 码率动态提示）
        private TrackBar tbCrf;
        private Label lblCrf;
        private ComboBox txtBitrate;
        private ComboBox cbACodec;
        private ComboBox cbABitrate;
        private CheckBox chkDeinterlace;
        private ComboBox cbResolution;
        private CheckBox chkGpu;
        private TextBox txtOutDir;
        private Button btnOutDir;
        private TextBox txtOutName;
        private TextBox txtFfmpeg;
        private Button btnFfmpeg;
        private ProgressBar progressBar;
        private Label lblProgress;
        private Button btnStart;
        private TextBox txtLog;

        private readonly List<string> _files = new List<string>();
        private Process _proc;
        private bool _running;
        private bool _stopping;
        private double _durationSec;
        private double _speedX;
        private int _sourceVideoBitrate;   // 源视频码率(kbps)，用于"源码率"选项
        private readonly string _configPath;
        private readonly Dictionary<string, string> _settings = new Dictionary<string, string>();

        // ---------- 配色（浅色清新主题）----------
        private static readonly Color Bg = Color.FromArgb(0xec, 0xf6, 0xf4);          // 全局背景 淡薄荷
        private static readonly Color PanelBg = Color.FromArgb(0xff, 0xff, 0xff);      // 面板 纯白
        private static readonly Color PanelAlt = Color.FromArgb(0xd9, 0xf2, 0xeb);     // 拖放区 渐变薄荷
        private static readonly Color FieldBg = Color.FromArgb(0xf6, 0xfb, 0xf9);      // 输入框 极浅薄荷
        private static readonly Color FieldFg = Color.FromArgb(0x2b, 0x3a, 0x36);      // 输入字 深青灰
        private static readonly Color Fg = Color.FromArgb(0x2b, 0x3a, 0x36);           // 正文 深青灰
        private static readonly Color Accent = Color.FromArgb(0x1f, 0xa7, 0x8f);       // 主按钮 薄荷绿
        private static readonly Color AccentText = Color.FromArgb(0xff, 0xff, 0xff);   // 主按钮文字 白
        private static readonly Color Border = Color.FromArgb(0xbf, 0xde, 0xd6);       // 边框 淡青
        private static readonly Color LogBg = Color.FromArgb(0xff, 0xff, 0xff);        // 日志 白
        private static readonly Color LogFg = Color.FromArgb(0x2b, 0x3a, 0x36);        // 日志字 深
        private static readonly Color OkText = Color.FromArgb(0x1f, 0xa7, 0x8f);       // 成功/进度 薄荷
        private static readonly Color TrackBg = Color.FromArgb(0xf6, 0xfb, 0xf9);      // 滑块 极浅薄荷

        // 分区标签点缀色（清新色盘）
        private static readonly Color LabelFormat = Color.FromArgb(0x0a, 0x7d, 0xb8);  // 天蓝
        private static readonly Color LabelVideo = Color.FromArgb(0x2d, 0x9b, 0x6e);    // 薄荷绿
        private static readonly Color LabelAudio = Color.FromArgb(0xf0, 0x8a, 0x4d);    // 橙
        private static readonly Color LabelMisc = Color.FromArgb(0x8e, 0x6d, 0xc7);     // 紫

        public MainForm()
        {
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VideoConvertTool.ini");
            LoadSettings();
            InitUi();
            // 窗口图标：读取可执行文件嵌入的图标
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            DetectFfmpeg();
        }

        // ================= 界面 =================
        private void InitUi()
        {
            Text = "AVS 视频转码工具 (ffmpeg 拖放转码) - 52pojie出品";
            BackColor = Bg;
            ForeColor = Fg;
            Font = new Font("Microsoft YaHei UI", 9F);
            MinimumSize = new Size(860, 700);
            Size = new Size(940, 760);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;
            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) => AddFiles((string[])e.Data.GetData(DataFormats.FileDrop));

            var top = 12;
            var left = 12;
            var rowH = 30;

            // ---- 拖放区 ----
            dropPanel = new Panel
            {
                Bounds = new Rectangle(left, top, ClientSize.Width - 24, 64),
                AllowDrop = true,
                BackColor = PanelAlt,
                BorderStyle = BorderStyle.FixedSingle
            };
            dropPanel.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            dropPanel.DragDrop += (s, e) => AddFiles((string[])e.Data.GetData(DataFormats.FileDrop));
            dropLabel = new Label
            {
                Bounds = new Rectangle(8, 6, dropPanel.Width - 280, 40),
                Text = "把视频/音频文件拖到此处",
                ForeColor = LabelFormat,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            btnBrowse = MakeButton("选择文件", new Rectangle(dropPanel.Width - 128, 16, 114, 32), BtnBrowse_Click);
            btnClear = MakeButton("清空", new Rectangle(dropPanel.Width - 250, 16, 112, 32), (s, e) => { _files.Clear(); RefreshFileList(); });
            dropPanel.Controls.Add(dropLabel);
            dropPanel.Controls.Add(btnBrowse);
            dropPanel.Controls.Add(btnClear);

            // ---- 参数区 ----
            top += 64 + 10;

            // 输出格式
            AddLabel("输出格式", left, top, 70, LabelFormat);
            cbFormat = MakeCombo(new Rectangle(left + 74, top, 120, 26), new[] { "MKV", "MP4", "TS", "MOV", "AVI", "WEBM", "MPEG" });
            cbFormat.SelectedIndex = 0;

            // 视频编码
            AddLabel("视频编码", left + 206, top, 70, LabelVideo);
            cbVCodec = MakeCombo(new Rectangle(left + 280, top, 150, 26), new[] { "H.264 (x264)", "H.265 (x265)", "AV1 (libaom)", "MPEG-4", "MPEG-2", "仅封装(复制)" });
            cbVCodec.SelectedIndex = 0;

            // 编码预设
            AddLabel("预设", left + 442, top, 40, LabelVideo);
            cbPreset = MakeCombo(new Rectangle(left + 486, top, 90, 26), new[] { "ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow" });
            cbPreset.SelectedIndex = 5;

            // 编码模式
            AddLabel("模式", left + 588, top, 40, LabelVideo);
            cbMode = MakeCombo(new Rectangle(left + 632, top, 70, 26), new[] { "CRF", "码率" });
            cbMode.SelectedIndex = 0;
            cbMode.SelectedIndexChanged += (s, e) => UpdateCrfUi();
            // 模式说明（CRF / 码率动态提示）
            lblModeHint = new Label
            {
                Bounds = new Rectangle(left + 706, top, 170, 26),
                ForeColor = Fg,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _labels.Add(lblModeHint);
            lblModeHint.Text = "值越小越清晰 推荐25-40";   // 初始为 CRF 解释

            top += rowH + 8;

            // CRF 滑块（左小=清晰，右大=模糊）
            AddLabel("CRF", left, top, 40, LabelVideo);
            AddLabel("清晰", left + 44, top, 34);
            tbCrf = new TrackBar
            {
                Bounds = new Rectangle(left + 80, top - 2, 200, 30),
                Minimum = 20, Maximum = 50, TickFrequency = 5, Value = 40,
                BackColor = TrackBg, ForeColor = Accent
            };
            AddLabel("模糊", left + 282, top, 34);
            lblCrf = new Label { Bounds = new Rectangle(left + 318, top, 40, 26), Text = "40", ForeColor = Accent, TextAlign = ContentAlignment.MiddleLeft };
            tbCrf.ValueChanged += (s, e) => lblCrf.Text = tbCrf.Value.ToString();
            // 视频码率
            AddLabel("视频码率kbps", left + 392, top, 100, LabelVideo);
            txtBitrate = MakeCombo(new Rectangle(left + 496, top, 84, 26), null);
            ((ComboBox)txtBitrate).DropDownStyle = ComboBoxStyle.DropDown;   // 可编辑
            ((ComboBox)txtBitrate).Items.Add("源码率");
            ((ComboBox)txtBitrate).Text = "8000";
            AddLabel("(0=自动)", left + 584, top, 60);

            top += rowH + 8;

            // 音频
            AddLabel("音频编码", left, top, 70, LabelAudio);
            cbACodec = MakeCombo(new Rectangle(left + 74, top, 130, 26), new[] { "复制(不转码)", "AAC", "AC-3", "MP3", "FLAC" });
            cbACodec.SelectedIndex = 0;
            AddLabel("音频码率", left + 216, top, 70, LabelAudio);
            cbABitrate = MakeCombo(new Rectangle(left + 290, top, 110, 26), new[] { "自动", "128k", "192k", "256k", "320k", "448k" });
            cbABitrate.SelectedIndex = 0;
            AddLabel("去隔行", left + 412, top, 60, LabelVideo);
            chkDeinterlace = new CheckBox
            {
                Bounds = new Rectangle(left + 476, top + 3, 60, 24),
                Text = "yadif", Checked = true, ForeColor = Fg
            };
            AddLabel("分辨率", left + 540, top, 60, LabelVideo);
            cbResolution = MakeCombo(new Rectangle(left + 604, top, 130, 26), new[] { "保持原样", "1920x1080", "1280x720", "960x540", "640x360" });
            cbResolution.SelectedIndex = 0;

            // GPU 硬件加速
            AddLabel("GPU加速", left + 748, top, 60, LabelVideo);
            chkGpu = new CheckBox { Bounds = new Rectangle(left + 812, top + 3, 40, 24), Text = "", Checked = false, ForeColor = Fg };

            top += rowH + 10;

            // 输出目录 / 文件名
            AddLabel("输出目录", left, top, 70, LabelMisc);
            txtOutDir = new TextBox
            {
                Bounds = new Rectangle(left + 74, top, ClientSize.Width - 24 - 74 - 86, 26),
                BackColor = FieldBg, ForeColor = FieldFg
            };
            btnOutDir = MakeButton("浏览", new Rectangle(ClientSize.Width - 86, top, 74, 28), BtnOutDir_Click);
            top += rowH + 8;

            AddLabel("输出名称", left, top, 70, LabelMisc);
            txtOutName = new TextBox
            {
                Bounds = new Rectangle(left + 74, top, ClientSize.Width - 24 - 74, 26),
                BackColor = FieldBg, ForeColor = FieldFg,
                Text = "(默认: 源文件名_转码)"
            };

            top += rowH + 10;

            // ffmpeg 路径
            AddLabel("ffmpeg", left, top, 70, LabelMisc);
            txtFfmpeg = new TextBox
            {
                Bounds = new Rectangle(left + 74, top, ClientSize.Width - 24 - 74 - 86, 26),
                BackColor = FieldBg, ForeColor = FieldFg
            };
            btnFfmpeg = MakeButton("选择", new Rectangle(ClientSize.Width - 86, top, 74, 28), BtnFfmpeg_Click);

            top += rowH + 12;

            // 进度条
            progressBar = new ProgressBar
            {
                Bounds = new Rectangle(left, top, ClientSize.Width - 24 - 140, 26),
                Style = ProgressBarStyle.Continuous,
                ForeColor = Accent
            };
            btnStart = MakeButton("开始转码", new Rectangle(ClientSize.Width - 130, top - 1, 118, 30), BtnStart_Click);
            btnStart.BackColor = Accent;
            btnStart.ForeColor = AccentText;

            top += 40;

            lblProgress = new Label
            {
                Bounds = new Rectangle(left, top - 4, ClientSize.Width - 24, 22),
                Text = "就绪",
                ForeColor = LabelFormat
            };

            top += 26;

            // 日志
            txtLog = new TextBox
            {
                Bounds = new Rectangle(left, top, ClientSize.Width - 24, ClientSize.Height - top - 16),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                BackColor = LogBg,
                ForeColor = LogFg,
                Font = new Font("Consolas", 9F),
                WordWrap = false
            };

            Controls.AddRange(new Control[] {
                dropPanel, cbFormat, cbVCodec, cbPreset, cbMode, tbCrf, lblCrf, txtBitrate,
                cbACodec, cbABitrate, chkDeinterlace, cbResolution, chkGpu,
                txtOutDir, btnOutDir, txtOutName, txtFfmpeg, btnFfmpeg,
                progressBar, btnStart, lblProgress, txtLog
            });
            AddLabels();

            // 从配置恢复
            if (_settings.TryGetValue("outdir", out var od)) txtOutDir.Text = od;
            if (_settings.TryGetValue("ffmpeg", out var ff)) txtFfmpeg.Text = ff;
            if (_settings.TryGetValue("format", out var fmt) && cbFormat.Items.Contains(fmt)) cbFormat.SelectedItem = fmt;
            if (_settings.TryGetValue("vcodec", out var vc) && cbVCodec.Items.Contains(vc)) cbVCodec.SelectedItem = vc;
            if (_settings.TryGetValue("acodec", out var ac) && cbACodec.Items.Contains(ac)) cbACodec.SelectedItem = ac;
            if (_settings.TryGetValue("crf", out var crf) && int.TryParse(crf, out var cv)) tbCrf.Value = Math.Max(20, Math.Min(50, cv));
            if (_settings.TryGetValue("resolution", out var rs) && cbResolution.Items.Contains(rs)) cbResolution.SelectedItem = rs;
        }

        private List<Label> _labels = new List<Label>();
        private void AddLabel(string text, int x, int y, int w, Color? c = null)
        {
            var l = new Label
            {
                Bounds = new Rectangle(x, y, w, 26),
                Text = text,
                ForeColor = c ?? Fg,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _labels.Add(l);
        }
        private void AddLabels() => Controls.AddRange(_labels.ToArray());

        private Button MakeButton(string text, Rectangle bounds, EventHandler onClick)
        {
            var b = new Button
            {
                Text = text,
                Bounds = bounds,
                FlatStyle = FlatStyle.Flat,
                BackColor = PanelBg,
                ForeColor = Fg,
                FlatAppearance = { BorderColor = Border, BorderSize = 1 },
                Cursor = Cursors.Hand
            };
            b.Click += onClick;
            return b;
        }

        private ComboBox MakeCombo(Rectangle bounds, string[] items)
        {
            var c = new ComboBox
            {
                Bounds = bounds,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = FieldBg,
                ForeColor = FieldFg,
                FlatStyle = FlatStyle.Flat
            };
            c.Items.AddRange(items ?? new string[0]);
            return c;
        }

        // ================= 文件处理 =================
        private void AddFiles(string[] paths)
        {
            foreach (var p in paths)
            {
                if (File.Exists(p) || Directory.Exists(p))
                    _files.Add(p);
            }
            RefreshFileList();
        }

        private void RefreshFileList()
        {
            if (_files.Count == 0)
            {
                dropLabel.Text = "把视频/音频文件拖到此处";
                return;
            }
            var names = _files.Select(Path.GetFileName).ToList();
            dropLabel.Text = "已添加 " + _files.Count + " 个: " + string.Join("  |  ", names);
            if (string.IsNullOrWhiteSpace(txtOutName.Text) || txtOutName.Text == "(默认: 源文件名_转码)")
            {
                txtOutName.Text = Path.GetFileNameWithoutExtension(_files[0]) + "_转码";
            }
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Multiselect = true, Filter = "媒体文件|*.ts;*.mp4;*.mkv;*.avi;*.mov;*.flv;*.wmv;*.m2ts;*.mts;*.mpg;*.mpeg;*.rmvb;*.webm;*.mp3;*.aac;*.flac;*.wav;*.ac3|所有文件|*.*" };
            if (ofd.ShowDialog() == DialogResult.OK)
                AddFiles(ofd.FileNames);
        }

        private void BtnOutDir_Click(object sender, EventArgs e)
        {
            using var fbd = new FolderBrowserDialog { Description = "选择输出目录" };
            if (fbd.ShowDialog() == DialogResult.OK)
                txtOutDir.Text = fbd.SelectedPath;
        }

        private void BtnFfmpeg_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "ffmpeg|ffmpeg.exe|所有文件|*.*" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                txtFfmpeg.Text = ofd.FileName;
                QueryEncodersAsync();   // 路径变更后重新探测硬件编码器
            }
        }

        // ================= ffmpeg 检测 =================
        private void DetectFfmpeg()
        {
            // 默认优先使用与程序同目录下的 ffmpeg.exe
            var local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (File.Exists(local))
            {
                txtFfmpeg.Text = local;
            }
            else if (string.IsNullOrWhiteSpace(txtFfmpeg.Text))
            {
                // 配置里也没有才弹窗让用户手动选择
                using var ofd = new OpenFileDialog
                {
                    Title = "请选择 ffmpeg.exe（默认应放在程序同目录）",
                    Filter = "ffmpeg|ffmpeg.exe|所有文件|*.*"
                };
                if (ofd.ShowDialog(this) == DialogResult.OK)
                    txtFfmpeg.Text = ofd.FileName;
                else
                    txtFfmpeg.Text = "";
            }
            // 后台预热硬件编码器检测（无论 ffmpeg 来自同目录还是用户选择都必须执行，
            // 否则 HardwareEncoders 永远为空，GPU 加速会一直回退到软件编码）
            if (File.Exists(txtFfmpeg.Text.Trim()))
                QueryEncodersAsync();
        }

        // ================= 配置 =================
        private void LoadSettings()
        {
            if (!File.Exists(_configPath)) return;
            foreach (var line in File.ReadAllLines(_configPath))
            {
                var i = line.IndexOf('=');
                if (i > 0) _settings[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
            }
        }
        private void SaveSettings()
        {
            var sb = new StringBuilder();
            sb.AppendLine("outdir=" + txtOutDir.Text);
            sb.AppendLine("ffmpeg=" + txtFfmpeg.Text);
            sb.AppendLine("format=" + cbFormat.SelectedItem);
            sb.AppendLine("vcodec=" + cbVCodec.SelectedItem);
            sb.AppendLine("acodec=" + cbACodec.SelectedItem);
            sb.AppendLine("crf=" + tbCrf.Value);
            sb.AppendLine("resolution=" + cbResolution.SelectedItem);
            File.WriteAllText(_configPath, sb.ToString());
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { SaveSettings(); } catch { }
            KillFfmpeg();   // 关闭窗口时兜底终止 ffmpeg，避免残留后台进程
            base.OnFormClosing(e);
        }

        // ================= 转码 =================
        private void UpdateCrfUi()
        {
            var isCrf = cbMode.SelectedIndex == 0;
            tbCrf.Enabled = isCrf;
            lblCrf.Enabled = isCrf;
            txtBitrate.Enabled = !isCrf;
            lblModeHint.Text = isCrf
                ? "值越小越清晰 推荐25-40"
                : "指定码率 越大越清晰 0=自动";
        }

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            if (_running) { await StopAsync(); return; }
            if (_files.Count == 0) { Log("请先拖入或选择文件。"); return; }

            var ffmpeg = txtFfmpeg.Text.Trim();
            if (!File.Exists(ffmpeg)) { Log("找不到 ffmpeg.exe，请点击 \"选择\" 指定路径。"); return; }

            var outDir = string.IsNullOrWhiteSpace(txtOutDir.Text) ? Path.GetDirectoryName(_files[0]) : txtOutDir.Text;
            if (!Directory.Exists(outDir)) { Log("输出目录不存在: " + outDir); return; }

            var baseName = string.IsNullOrWhiteSpace(txtOutName.Text) || txtOutName.Text.StartsWith("(")
                ? Path.GetFileNameWithoutExtension(_files[0]) + "_转码"
                : txtOutName.Text;
            var ext = cbFormat.SelectedItem.ToString().ToLowerInvariant();
            if (ext == "mpeg") ext = "mpg";

            btnStart.Enabled = false;
            txtLog.Clear();
            progressBar.Value = 0;
            lblProgress.Text = "准备中...";

            try
            {
                // 勾选了 GPU 时，等待后台硬件编码器探测完成（最长30秒），避免探测未完成就回退软件编码
                if (chkGpu.Checked && _hwProbeTask != null && HardwareEncoders == null)
                {
                    lblProgress.Text = "正在检测 GPU 硬件编码器...";
                    await Task.WhenAny(_hwProbeTask, Task.Delay(30000));
                }

                var ffprobe = Path.Combine(Path.GetDirectoryName(ffmpeg), "ffprobe.exe");
                var useSourceBitrate = cbMode.SelectedIndex == 1 && txtBitrate.Text.Trim() == "源码率";

                for (int idx = 0; idx < _files.Count; idx++)
                {
                    if (_stopping) break;
                    var input = _files[idx];
                    var outputName = _files.Count == 1 ? baseName : baseName + "_" + (idx + 1);
                    var output = Path.Combine(outDir, outputName + "." + ext);
                    // 每个文件单独取时长，保证进度条准确
                    _durationSec = await Task.Run(() => GetDuration(ffprobe, input));
                    _sourceVideoBitrate = 0;
                    if (useSourceBitrate)
                        _sourceVideoBitrate = await Task.Run(() => GetSourceVideoBitrate(ffprobe, input));
                    var args = BuildArgs(input, output);
                    Log($"[{idx + 1}/{_files.Count}] 开始: {Path.GetFileName(input)}");
                    Log("命令: " + ffmpeg + " " + args);
                    await RunFfmpegAsync(ffmpeg, args);
                    if (!_stopping)
                    {
                        // ffmpeg 统计行最后一帧的 time= 略小于总时长，手动补满进度条
                        var done = idx + 1;
                        SafeUi(() =>
                        {
                            progressBar.Value = 100;
                            lblProgress.Text = _files.Count == 1
                                ? "转码完成 100%"
                                : $"已完成 {done}/{_files.Count}";
                        });
                    }
                }
                if (!_stopping)
                {
                    lblProgress.Text = "全部完成 ✔";
                    lblProgress.ForeColor = OkText;
                    SafeUi(() => progressBar.Value = 100);
                }
                Log("完成。");
            }
            catch (Exception ex)
            {
                Log("错误: " + ex.Message);
            }
            finally
            {
                _running = false;
                btnStart.Enabled = true;
                btnStart.Text = "开始转码";
            }
        }

        private string BuildArgs(string input, string output)
        {
            var sb = new StringBuilder();
            sb.Append("-hide_banner -y -i \"").Append(input).Append('"');

            var vf = new List<string>();
            if (chkDeinterlace.Checked) vf.Add("yadif");
            var res = cbResolution.SelectedItem.ToString();
            if (res != "保持原样") vf.Add("scale=" + res.Replace("x", ":"));
            if (vf.Count > 0) sb.Append(" -vf ").Append(string.Join(",", vf));

            sb.Append(" -map 0:v:0? -map 0:a:0? -map 0:s? -map 0:3? ");

            var vcodec = cbVCodec.SelectedItem.ToString();
            var isCopy = vcodec == "仅封装(复制)";
            if (isCopy)
            {
                sb.Append("-c copy ");
            }
            else
            {
                string enc = SelectVideoEncoder();
                bool isGpuEnc = enc.Contains("nvenc") || enc.Contains("_qsv") || enc.Contains("_amf") || enc.Contains("videotoolbox");
                sb.Append("-c:v ").Append(enc).Append(' ');
                if (enc == "libx264" || enc == "libx265")
                    sb.Append("-preset ").Append(cbPreset.SelectedItem).Append(' ');
                else if (enc == "libaom-av1")
                    sb.Append("-cpu-used 6 ");

                if (cbMode.SelectedIndex == 0) // CRF（质量模式）
                {
                    if (!isGpuEnc)
                        sb.Append("-crf ").Append(tbCrf.Value).Append(' ');
                    else if (enc.Contains("nvenc"))
                        sb.Append("-rc vbr -cq ").Append(tbCrf.Value).Append(" -b:v 0 ");   // nvenc 无限码率的恒定质量
                    else if (enc.Contains("_qsv"))
                        sb.Append("-global_quality ").Append(tbCrf.Value).Append(' ');
                    else
                        sb.Append("-cq ").Append(tbCrf.Value).Append(' ');
                }
                else // 码率模式：只给 -b:v，绝不叠加 -cq（否则 NVENC 下 -cq 优先导致码率失控）
                {
                    var brText = txtBitrate.Text.Trim();
                    if (brText == "源码率")
                    {
                        if (_sourceVideoBitrate > 0)
                            sb.Append("-b:v ").Append(_sourceVideoBitrate).Append("k ");
                    }
                    else if (int.TryParse(brText, out var br) && br > 0)
                        sb.Append("-b:v ").Append(br).Append("k ");
                }
                sb.Append("-c:a ").Append(GetAudioEncoder()).Append(' ').Append(GetAudioBitrateArg()).Append(' ');
                sb.Append("-c:s copy ");
                sb.Append("-sn ");
            }

            // 不传 -progress/-nostats，让 ffmpeg 默认把编码统计输出到 stderr，
            // 由下方 time= / speed= 正则实时更新进度条、进度文本与倍速。
            sb.Append("-nostdin ");
            sb.Append('"').Append(output).Append('"');
            return sb.ToString();
        }

        private bool _gpuFbLogged;
        private string SelectVideoEncoder()
        {
            var vcodec = cbVCodec.SelectedItem.ToString();
            if (chkGpu.Checked)
            {
                // 硬件加速编码器：按 GPU 品牌优先
                var hwCandidates = vcodec switch
                {
                    "H.264 (x264)" => new[] { "h264_nvenc", "h264_qsv", "h264_amf", "h264_videotoolbox" },
                    "H.265 (x265)" => new[] { "hevc_nvenc", "hevc_qsv", "hevc_amf", "hevc_videotoolbox" },
                    _ => Array.Empty<string>()
                };
                var avail = GetHardwareEncodersBlocking();
                foreach (var c in hwCandidates)
                    if (avail.Contains(c)) return c;
                // 没有可用硬件编码器则回退软件编码
                if (!_gpuFbLogged)
                {
                    _gpuFbLogged = true;
                    SafeUi(() => Log("提示: 未找到可用的 GPU 硬件编码器(可能驱动过旧或无硬件支持)，已自动回退为软件编码。"));
                }
                if (vcodec.StartsWith("H.264")) return "libx264";
                if (vcodec.StartsWith("H.265")) return "libx265";
            }
            return vcodec switch
            {
                "H.264 (x264)" => "libx264",
                "H.265 (x265)" => "libx265",
                "AV1 (libaom)" => "libaom-av1",
                "MPEG-4" => "mpeg4",
                "MPEG-2" => "mpeg2video",
                _ => "libx264"
            };
        }

        private HashSet<string> HardwareEncoders;
        private Task _hwProbeTask;
        private string _hwProbedPath;
        private void QueryEncodersAsync()
        {
            // 必须在 UI 线程先取出路径，避免后台线程访问控件（跨线程异常会被吞掉导致探测失败）
            var ffmpeg = txtFfmpeg.Text.Trim();
            if (!File.Exists(ffmpeg)) return;
            if (_hwProbeTask != null && _hwProbedPath == ffmpeg) return;   // 同一路径只探测一次
            _hwProbedPath = ffmpeg;
            _hwProbeTask = Task.Run(() =>
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(ffmpeg))
                {
                    try
                    {
                        var psi = new ProcessStartInfo(ffmpeg, "-hide_banner -encoders")
                        {
                            RedirectStandardError = true, RedirectStandardOutput = true,
                            UseShellExecute = false, CreateNoWindow = true
                        };
                        using var p = Process.Start(psi);
                        var tOut = p.StandardOutput.ReadToEndAsync();
                        var tErr = p.StandardError.ReadToEndAsync();
                        if (!p.WaitForExit(8000)) { try { p.Kill(); } catch { } }
                        Task.WaitAll(new[] { tOut, tErr });
                        var outAll = tErr.Result + tOut.Result;
                        var hwCandidates = new List<string>();
                        foreach (var line in outAll.Split('\n'))
                        {
                            if (!line.Contains("V.....") && !line.Contains("V....D")) continue;
                            var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2 && IsHardwareEncoder(parts[1])) hwCandidates.Add(parts[1]);
                        }
                        // 逐个实际试转一次，验证驱动/硬件真能打开（剔除打不开的编码器）
                        foreach (var enc in hwCandidates)
                            if (ProbeEncoder(ffmpeg, enc)) set.Add(enc);
                    }
                    catch { }
                }
                HardwareEncoders = set;
                // 输出探测结果，方便确认 GPU 加速是否可用
                if (set.Count > 0)
                    SafeUi(() => Log("GPU 硬件编码器可用: " + string.Join(", ", set)));
                else
                    SafeUi(() => Log("提示: 未探测到可用的 GPU 硬件编码器（检查显卡驱动版本）。"));
            });
        }
        private static bool IsHardwareEncoder(string name)
        {
            return name.Contains("nvenc") || name.Contains("_qsv") || name.Contains("_amf") || name.Contains("videotoolbox");
        }
        private static bool ProbeEncoder(string ffmpeg, string enc)
        {
            try
            {
                var psi = new ProcessStartInfo(ffmpeg,
                    $"-hide_banner -y -v error -f lavfi -i color=size=128x128:rate=30 -frames:v 1 -c:v {enc} -f null -")
                {
                    RedirectStandardError = true, RedirectStandardOutput = true,
                    UseShellExecute = false, CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                var tErr = p.StandardError.ReadToEndAsync();
                var tOut = p.StandardOutput.ReadToEndAsync();
                p.WaitForExit(8000);
                Task.WaitAll(new[] { tErr, tOut });
                return p.ExitCode == 0;
            }
            catch { return false; }
        }
        private HashSet<string> GetHardwareEncodersBlocking()
        {
            // 绝不阻塞 UI 线程：检测在启动时已预热的后台任务中完成，
            // 未就绪时返回空集合（回退软件编码），由 SelectVideoEncoder 提示。
            return HardwareEncoders ?? new HashSet<string>();
        }

        private string GetAudioEncoder()
        {
            return cbACodec.SelectedItem.ToString() switch
            {
                "AAC" => "aac",
                "AC-3" => "ac3",
                "MP3" => "libmp3lame",
                "FLAC" => "flac",
                _ => "copy"
            };
        }
        private string GetAudioBitrateArg()
        {
            var sel = cbABitrate.SelectedItem.ToString();
            if (sel == "自动") return "";
            var enc = GetAudioEncoder();
            if (enc == "copy" || enc == "flac") return "";
            return "-b:a " + sel;
        }

        private double GetDuration(string ffprobe, string input)
        {
            if (!File.Exists(ffprobe)) return 0;
            try
            {
                var psi = new ProcessStartInfo(ffprobe, $"-v error -show_entries format=duration -of csv=p=0 \"{input}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                var outStr = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(5000);
                if (double.TryParse(outStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    return d;
            }
            catch { }
            return 0;
        }

        // 读取源文件的视频码率(kbps)，供"源码率"选项使用
        private int GetSourceVideoBitrate(string ffprobe, string input)
        {
            if (!File.Exists(ffprobe)) return 0;
            try
            {
                var psi = new ProcessStartInfo(ffprobe,
                    $"-v error -select_streams v:0 -show_entries stream=bit_rate -of default=noprint_wrappers=1:nokey=1 \"{input}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                var outStr = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(5000);
                if (double.TryParse(outStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var bps) && bps > 0)
                    return (int)Math.Round(bps / 1000);
            }
            catch { }
            return 0;
        }

        private Task RunFfmpegAsync(string ffmpeg, string args)
        {
            var tcs = new TaskCompletionSource<bool>();
            _running = true;
            _stopping = false;
            btnStart.Enabled = true;   // 重新启用按钮，使其作为「停止」可点击
            btnStart.Text = "停止";

            var psi = new ProcessStartInfo(ffmpeg, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

            _proc.OutputDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                var mSpeed = Regex.Match(e.Data, @"speed=([\d.]+)");
                if (mSpeed.Success && double.TryParse(mSpeed.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var spd))
                {
                    _speedX = spd / 1e6;   // -progress 的 speed 以微秒计，除以1e6得倍率
                }
                var m = Regex.Match(e.Data, @"out_time_ms=(\d+)");
                if (m.Success)
                {
                    var ms = long.Parse(m.Groups[1].Value);
                    UpdateProgress(ms / 1e6);
                }
            };
            _proc.ErrorDataReceived += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                // 从 ffmpeg 头部的 Duration: 行解析总时长（当 ffprobe 读取失败时为 0 时启用）
                if (_durationSec <= 0)
                {
                    var mDur = Regex.Match(e.Data, @"Duration:\s*(\d+):(\d+):([\d.]+)");
                    if (mDur.Success)
                    {
                        var d = int.Parse(mDur.Groups[1].Value) * 3600 + int.Parse(mDur.Groups[2].Value) * 60 + double.Parse(mDur.Groups[3].Value, CultureInfo.InvariantCulture);
                        if (d > 0) _durationSec = d;
                    }
                }
                var mSpeed = Regex.Match(e.Data, @"speed=\s*([\d.]+)x");
                if (mSpeed.Success && double.TryParse(mSpeed.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var spdE))
                    _speedX = spdE;
                var m = Regex.Match(e.Data, @"time=(\d+):(\d+):([\d.]+)");
                if (m.Success && _running)
                {
                    var t = int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60 + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                    UpdateProgress(t);
                }
                // 日志行：跳过 ffmpeg 的统计刷新行（含 frame=），其余信息显示
                if (!e.Data.Contains("frame=") && e.Data.Trim().Length > 0)
                {
                    SafeUi(() => Log(e.Data));
                }
            };

            _proc.Exited += (s, e) =>
            {
                var p = _proc;
                try { p?.WaitForExit(); } catch { }
                SafeUi(() =>
                {
                    var code = p?.ExitCode ?? -1;
                    if (code != 0 && !_stopping) Log("ffmpeg 退出码: " + code);
                });
                tcs.TrySetResult(true);
            };

            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
            return tcs.Task;
        }

        private async Task StopAsync()
        {
            _stopping = true;
            KillFfmpeg();
            btnStart.Text = "开始转码";
            btnStart.Enabled = true;
            _running = false;
            lblProgress.Text = "已停止";
            await Task.CompletedTask;
        }

        private void KillFfmpeg()
        {
            try
            {
                if (_proc != null)
                {
                    if (!_proc.HasExited) _proc.Kill();   // 直接强杀
                    // 兜底：连同其进程树一起终止（ffmpeg 可能有子进程或延迟退出）
                    try { _proc.Kill(entireProcessTree: true); } catch { }
                    _proc.Dispose();
                    _proc = null;
                }
            }
            catch { }
            // 最后保险：taskkill 按名称/树强制结束残留 ffmpeg.exe
            try
            {
                var psi = new ProcessStartInfo("taskkill", "/F /T /IM ffmpeg.exe")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                using var tp = Process.Start(psi);
            }
            catch { }
        }

        private string FormatTime(double sec)
        {
            var ts = TimeSpan.FromSeconds(sec);
            return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
        }
        private string SpeedText()
        {
            return _speedX > 0 ? $"{_speedX:0.##}x" : "";
        }
        private void UpdateProgress(double sec)
        {
            SafeUi(() =>
            {
                if (_running && _durationSec > 0)
                {
                    var pct = (int)(sec / _durationSec * 100);
                    pct = Math.Max(0, Math.Min(100, pct));
                    progressBar.Value = pct;
                    lblProgress.Text = $"转码中 {pct}%   {FormatTime(sec)} / {FormatTime(_durationSec)}   {SpeedText()}";
                }
                else
                {
                    // 时长未知时：显示当前进度点与倍速
                    lblProgress.Text = $"转码中  已处理 {FormatTime(sec)}   {SpeedText()}";
                }
            });
        }

        private void SafeUi(Action a)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(a); } catch { } }
            else a();
        }

        private void Log(string msg)
        {
            if (txtLog.TextLength > 60000) txtLog.Clear();
            txtLog.AppendText(msg + Environment.NewLine);
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }
    }
}

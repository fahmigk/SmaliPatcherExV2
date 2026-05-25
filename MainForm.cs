using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SmaliPatcherEx
{
    public partial class MainForm : Form
    {
        // ── constants ────────────────────────────────────────────────────────
        private const string BAKSMALI_URL = "https://github.com/JesusFreke/smali/releases/download/v3.0.9/baksmali-3.0.9.jar";
        private const string SMALI_URL    = "https://github.com/JesusFreke/smali/releases/download/v3.0.9/smali-3.0.9.jar";
        private static readonly string ToolDir    = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".smali_patcher_ex");
        private static readonly string BaksmaliJar = Path.Combine(ToolDir, "baksmali.jar");
        private static readonly string SmaliJar    = Path.Combine(ToolDir, "smali.jar");

        // ── state ─────────────────────────────────────────────────────────────
        private string?  _servicesJarPath;
        private string   _outputDir   = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SmaliPatcherEx_Output");
        private int      _apiLevel    = 36;
        private string   _fingerprint = "";
        private string   _workDir     = "";
        private bool     _busy        = false;

        // ── controls ──────────────────────────────────────────────────────────
        private Panel        _topPanel    = null!;
        private Panel        _midPanel    = null!;
        private Panel        _logPanel    = null!;
        private Label        _titleLabel  = null!;
        private Label        _subtitleLbl = null!;
        private Button       _jarBtn      = null!;
        private Label        _jarLabel    = null!;
        private NumericUpDown _apiBox     = null!;
        private TextBox      _fpBox       = null!;
        private TextBox      _outBox      = null!;
        private Button       _outBtn      = null!;
        private FlowLayoutPanel _patchFlow = null!;
        private Button       _patchBtn    = null!;
        private Button       _selectAll   = null!;
        private Button       _clearAll    = null!;
        private RichTextBox  _logBox      = null!;
        private ProgressBar  _progress    = null!;
        private StatusStrip  _status      = null!;
        private ToolStripStatusLabel _statusLabel = null!;

        private readonly Dictionary<string, CheckBox> _checkBoxes = new();

        // ─────────────────────────────────────────────────────────────────────
        public MainForm()
        {
            InitializeComponent();
            BuildUI();
            PopulatePatches();
            Log("SmaliPatcherEx v2.0  —  Android 16 (API 36) Ready");
            Log("Select services.jar, choose patches, click Patch.");
        }

        // ── UI BUILD ─────────────────────────────────────────────────────────
        private void BuildUI()
        {
            Text            = "SmaliPatcherEx  v2.0  [Android 16 / API 36]";
            Size            = new System.Drawing.Size(860, 780);
            MinimumSize     = new System.Drawing.Size(760, 680);
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = System.Drawing.Color.FromArgb(28, 28, 36);
            ForeColor       = System.Drawing.Color.WhiteSmoke;
            Font            = new System.Drawing.Font("Segoe UI", 9f);

            // ── STATUS STRIP ─────────────────────────────────────────
            _status      = new StatusStrip { BackColor = System.Drawing.Color.FromArgb(20,20,28) };
            _statusLabel = new ToolStripStatusLabel("Ready")
                           { ForeColor = System.Drawing.Color.LightGreen };
            _status.Items.Add(_statusLabel);
            Controls.Add(_status);

            // ── TOP PANEL (title + jar select) ───────────────────────
            _topPanel = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 120,
                Padding   = new Padding(12, 10, 12, 0),
                BackColor = System.Drawing.Color.FromArgb(34, 34, 48),
            };

            _titleLabel = MkLabel("SmaliPatcherEx", 18, bold: true);
            _titleLabel.ForeColor = System.Drawing.Color.FromArgb(100, 210, 255);
            _titleLabel.Location  = new System.Drawing.Point(12, 10);

            _subtitleLbl = MkLabel("Android 16 (API 36) Compatible  —  sabpprook rebuild", 9);
            _subtitleLbl.ForeColor = System.Drawing.Color.Gray;
            _subtitleLbl.Location  = new System.Drawing.Point(14, 38);

            _jarBtn = MkButton("Select services.jar", 140, 28);
            _jarBtn.Location = new System.Drawing.Point(12, 68);
            _jarBtn.Click   += JarBtn_Click;

            _jarLabel = MkLabel("No file selected", 9);
            _jarLabel.ForeColor = System.Drawing.Color.Gray;
            _jarLabel.AutoSize  = false;
            _jarLabel.Size      = new System.Drawing.Size(650, 22);
            _jarLabel.Location  = new System.Drawing.Point(164, 73);

            _topPanel.Controls.AddRange(new Control[]
                { _titleLabel, _subtitleLbl, _jarBtn, _jarLabel });
            Controls.Add(_topPanel);

            // ── MID PANEL (settings + patches) ───────────────────────
            _midPanel = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 360,
                Padding   = new Padding(12, 8, 12, 0),
                BackColor = System.Drawing.Color.FromArgb(30, 30, 44),
            };

            // API level
            var apiLbl = MkLabel("API Level:", 9);
            apiLbl.Location = new System.Drawing.Point(12, 12);
            _apiBox = new NumericUpDown
            {
                Location   = new System.Drawing.Point(84, 8),
                Size       = new System.Drawing.Size(60, 24),
                Minimum    = 21,
                Maximum    = 99,
                Value      = _apiLevel,
                BackColor  = System.Drawing.Color.FromArgb(42, 42, 60),
                ForeColor  = System.Drawing.Color.WhiteSmoke,
            };
            _apiBox.ValueChanged += (s, e) => _apiLevel = (int)_apiBox.Value;

            // Fingerprint
            var fpLbl = MkLabel("Fingerprint (optional):", 9);
            fpLbl.Location = new System.Drawing.Point(160, 12);
            _fpBox = new TextBox
            {
                Location    = new System.Drawing.Point(310, 8),
                Size        = new System.Drawing.Size(400, 24),
                PlaceholderText = "leave blank to skip fingerprint check in module",
                BackColor   = System.Drawing.Color.FromArgb(42, 42, 60),
                ForeColor   = System.Drawing.Color.WhiteSmoke,
            };
            _fpBox.TextChanged += (s, e) => _fingerprint = _fpBox.Text;

            // Output dir
            var outLbl = MkLabel("Output:", 9);
            outLbl.Location = new System.Drawing.Point(12, 42);
            _outBox = new TextBox
            {
                Location  = new System.Drawing.Point(84, 38),
                Size      = new System.Drawing.Size(590, 24),
                Text      = _outputDir,
                BackColor = System.Drawing.Color.FromArgb(42, 42, 60),
                ForeColor = System.Drawing.Color.WhiteSmoke,
            };
            _outBox.TextChanged += (s, e) => _outputDir = _outBox.Text;

            _outBtn = MkButton("…", 36, 24);
            _outBtn.Location = new System.Drawing.Point(682, 38);
            _outBtn.Click   += OutBtn_Click;

            // Patches label + select/clear buttons
            var patchLbl = MkLabel("Smali Patches:", 9, bold: true);
            patchLbl.ForeColor = System.Drawing.Color.FromArgb(100, 210, 255);
            patchLbl.Location  = new System.Drawing.Point(12, 74);

            _selectAll = MkButton("All", 50, 22);
            _selectAll.Location = new System.Drawing.Point(120, 72);
            _selectAll.Click   += (s, e) => SetAllChecked(true);

            _clearAll = MkButton("None", 50, 22);
            _clearAll.Location = new System.Drawing.Point(178, 72);
            _clearAll.Click   += (s, e) => SetAllChecked(false);

            // Patch checkbox panel
            _patchFlow = new FlowLayoutPanel
            {
                Location       = new System.Drawing.Point(12, 100),
                Size           = new System.Drawing.Size(820, 240),
                FlowDirection  = FlowDirection.TopDown,
                WrapContents   = true,
                AutoScroll     = true,
                BackColor      = System.Drawing.Color.FromArgb(26, 26, 40),
                Padding        = new Padding(6),
            };

            // Patch button
            _patchBtn = MkButton("▶  Patch & Build Module", 200, 36);
            _patchBtn.Location  = new System.Drawing.Point(12, 348);
            _patchBtn.Font      = new System.Drawing.Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold);
            _patchBtn.BackColor = System.Drawing.Color.FromArgb(0, 140, 255);
            _patchBtn.FlatAppearance.BorderSize = 0;
            _patchBtn.Click    += PatchBtn_Click;

            _midPanel.Controls.AddRange(new Control[]
            {
                apiLbl, _apiBox, fpLbl, _fpBox,
                outLbl, _outBox, _outBtn,
                patchLbl, _selectAll, _clearAll,
                _patchFlow, _patchBtn,
            });
            Controls.Add(_midPanel);

            // ── LOG PANEL ────────────────────────────────────────────
            _logPanel = new Panel
            {
                Dock      = DockStyle.Fill,
                Padding   = new Padding(12, 8, 12, 8),
                BackColor = System.Drawing.Color.FromArgb(18, 18, 26),
            };

            _progress = new ProgressBar
            {
                Dock   = DockStyle.Top,
                Height = 6,
                Style  = ProgressBarStyle.Marquee,
                Visible = false,
            };

            _logBox = new RichTextBox
            {
                Dock      = DockStyle.Fill,
                BackColor = System.Drawing.Color.FromArgb(14, 14, 22),
                ForeColor = System.Drawing.Color.LightGreen,
                Font      = new System.Drawing.Font("Consolas", 8.5f),
                ReadOnly  = true,
                ScrollBars= RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
            };

            _logPanel.Controls.Add(_logBox);
            _logPanel.Controls.Add(_progress);
            Controls.Add(_logPanel);
        }

        private void PopulatePatches()
        {
            _patchFlow.Controls.Clear();
            _checkBoxes.Clear();

            foreach (var patch in PatchDefinitions.All)
            {
                var cb = new CheckBox
                {
                    Text      = $"{patch.Name}  —  {patch.Description}",
                    Checked   = true,
                    AutoSize  = false,
                    Width     = 780,
                    Height    = 22,
                    ForeColor = System.Drawing.Color.WhiteSmoke,
                    BackColor = System.Drawing.Color.Transparent,
                };
                var tip = new ToolTip();
                tip.SetToolTip(cb,
                    $"{patch.Description}\n" +
                    $"Target: {patch.FileGlob}\n" +
                    $"Android: A{patch.AndroidMin}–{(patch.AndroidMax < 99 ? "A"+patch.AndroidMax : "latest")}");

                _checkBoxes[patch.Name] = cb;
                _patchFlow.Controls.Add(cb);
            }
        }

        // ── HELPERS ──────────────────────────────────────────────────────────
        private static Label MkLabel(string text, float size, bool bold = false)
        {
            return new Label
            {
                Text      = text,
                AutoSize  = true,
                Font      = new System.Drawing.Font("Segoe UI", size,
                                bold ? System.Drawing.FontStyle.Bold
                                     : System.Drawing.FontStyle.Regular),
                ForeColor = System.Drawing.Color.WhiteSmoke,
            };
        }

        private static Button MkButton(string text, int w, int h)
        {
            return new Button
            {
                Text      = text,
                Size      = new System.Drawing.Size(w, h),
                BackColor = System.Drawing.Color.FromArgb(55, 55, 80),
                ForeColor = System.Drawing.Color.WhiteSmoke,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
            };
        }

        private void SetAllChecked(bool value)
        {
            foreach (var cb in _checkBoxes.Values) cb.Checked = value;
        }

        private void SetBusy(bool busy)
        {
            _busy            = busy;
            _patchBtn.Enabled  = !busy;
            _jarBtn.Enabled    = !busy;
            _progress.Visible  = busy;
            _statusLabel.Text  = busy ? "Working…" : "Ready";
        }

        // ── EVENT HANDLERS ───────────────────────────────────────────────────
        private void JarBtn_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "Select services.jar",
                Filter = "JAR files (*.jar)|*.jar|All files (*.*)|*.*",
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _servicesJarPath = dlg.FileName;
                _jarLabel.Text   = _servicesJarPath;
                _jarLabel.ForeColor = System.Drawing.Color.LightGreen;
                Log($"[+] Selected: {_servicesJarPath}");
            }
        }

        private void OutBtn_Click(object? sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description          = "Select output folder",
                UseDescriptionForTitle = true,
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _outputDir  = dlg.SelectedPath;
                _outBox.Text = _outputDir;
            }
        }

        private async void PatchBtn_Click(object? sender, EventArgs e)
        {
            if (_busy) return;

            if (string.IsNullOrEmpty(_servicesJarPath) || !File.Exists(_servicesJarPath))
            {
                MessageBox.Show("Select a valid services.jar first.",
                    "No input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selected = _checkBoxes
                .Where(kv => kv.Value.Checked)
                .Select(kv => kv.Key)
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show("Select at least one patch.",
                    "No patches", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true);
            _logBox.Clear();

            try
            {
                await Task.Run(() => RunPipeline(selected));
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ── PIPELINE ─────────────────────────────────────────────────────────
        private void RunPipeline(List<string> patchNames)
        {
            _workDir = Path.Combine(Path.GetTempPath(),
                $"smpx_{DateTime.Now:yyyyMMddHHmmss}");
            Directory.CreateDirectory(_workDir);

            try
            {
                Log("══════════════════════════════════════════════");
                Log($"SmaliPatcherEx v2.0  —  API {_apiLevel}");
                Log("══════════════════════════════════════════════");

                // ── Step 1: Tools
                Log("\n[1/5] Tool setup …");
                if (!EnsureTools()) return;

                // ── Step 2: Extract DEX
                Log("\n[2/5] Extracting DEX from services.jar …");
                var dexDir = Path.Combine(_workDir, "dex");
                Directory.CreateDirectory(dexDir);
                var dexFiles = ExtractDex(_servicesJarPath!, dexDir);
                if (dexFiles.Count == 0) { Log("[!] No dex found in JAR"); return; }
                Log($"[✓] {dexFiles.Count} dex file(s) extracted");

                // ── Step 3: Baksmali
                Log("\n[3/5] Disassembling to smali …");
                var smaliDir = Path.Combine(_workDir, "smali");
                Directory.CreateDirectory(smaliDir);
                foreach (var dex in dexFiles)
                {
                    if (!RunBaksmali(dex, smaliDir)) return;
                }
                var smaliCount = Directory.GetFiles(smaliDir, "*.smali",
                    SearchOption.AllDirectories).Length;
                Log($"[✓] {smaliCount} smali files");

                // ── Step 4: Patch
                Log("\n[4/5] Applying patches …");
                var engine = new PatchEngine(smaliDir, _apiLevel);
                engine.Log += Log;
                var results = engine.RunAll(patchNames);
                var applied = results.Values.Where(r => r.Applied).Select(r => r.Patch.Name).ToArray();
                Log($"[✓] Applied: {applied.Length}  Skipped: {results.Count - applied.Length}");

                if (applied.Length == 0)
                {
                    Log("[!] No patches applied — target classes not found in this build");
                    return;
                }

                // ── Step 5: Recompile + package
                Log("\n[5/5] Recompiling & packaging …");
                var rebuiltDex = Path.Combine(_workDir, "rebuilt", "classes.dex");
                Directory.CreateDirectory(Path.GetDirectoryName(rebuiltDex)!);
                if (!RunSmali(smaliDir, rebuiltDex)) return;

                var patchedJar = Path.Combine(_workDir, "services.jar");
                RepackJar(_servicesJarPath!, rebuiltDex, patchedJar);

                Directory.CreateDirectory(_outputDir);
                var modZip = MagiskBuilder.Build(
                    patchedJar, _outputDir, _fingerprint, applied, Log);

                // Copy patched JAR to output too
                var outJar = Path.Combine(_outputDir, "services.jar");
                File.Copy(patchedJar, outJar, overwrite: true);

                Log("\n══════════════════════════════════════════════");
                Log($"[✓] Module ZIP:   {modZip}");
                Log($"[✓] Patched JAR:  {outJar}");
                Log("══════════════════════════════════════════════");
                Log("Flash module via Magisk / KernelSU / APatch");
                Log("Reboot to activate patches.");

                Invoke(() =>
                {
                    _statusLabel.Text = "Done!";
                    MessageBox.Show(
                        $"Module built successfully!\n\n{modZip}",
                        "SmaliPatcherEx", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
            }
            finally
            {
                try { Directory.Delete(_workDir, true); } catch { }
            }
        }

        // ── TOOL DOWNLOAD ────────────────────────────────────────────────────
        private bool EnsureTools()
        {
            Directory.CreateDirectory(ToolDir);
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(60);

            foreach (var (jar, url, label) in new[]
            {
                (BaksmaliJar, BAKSMALI_URL, "baksmali"),
                (SmaliJar,    SMALI_URL,    "smali"),
            })
            {
                if (File.Exists(jar)) { Log($"[✓] {label}.jar present"); continue; }
                Log($"[*] Downloading {label}.jar …");
                try
                {
                    var data = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
                    File.WriteAllBytes(jar, data);
                    Log($"[✓] {label}.jar  ({data.Length / 1024} KB)");
                }
                catch (Exception ex)
                {
                    Log($"[!] Download failed: {ex.Message}");
                    Log($"    Place {label}.jar manually at: {jar}");
                    Log($"    URL: {url}");
                    return false;
                }
            }
            return true;
        }

        // ── DEX EXTRACTION ───────────────────────────────────────────────────
        private List<string> ExtractDex(string jarPath, string outDir)
        {
            var result = new List<string>();
            using var zip = ZipFile.OpenRead(jarPath);
            foreach (var entry in zip.Entries)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(
                    entry.Name, @"^classes\d*\.dex$"))
                {
                    var dest = Path.Combine(outDir, entry.Name);
                    entry.ExtractToFile(dest, overwrite: true);
                    result.Add(dest);
                    Log($"  extracted: {entry.Name}  ({entry.Length / 1024} KB)");
                }
            }
            return result;
        }

        // ── BAKSMALI / SMALI ─────────────────────────────────────────────────
        private bool RunBaksmali(string dexPath, string outDir)
        {
            Log($"  baksmali ← {Path.GetFileName(dexPath)}");
            var r = RunJar(BaksmaliJar,
                $"disassemble --api {_apiLevel} --output \"{outDir}\" \"{dexPath}\"");
            if (r.ExitCode != 0) { Log($"[!] baksmali error:\n{r.Stderr}"); return false; }
            return true;
        }

        private bool RunSmali(string smaliDir, string outDex)
        {
            Log($"  smali → {Path.GetFileName(outDex)}");
            var r = RunJar(SmaliJar,
                $"assemble --api {_apiLevel} --output \"{outDex}\" \"{smaliDir}\"");
            if (r.ExitCode != 0) { Log($"[!] smali error:\n{r.Stderr}"); return false; }
            Log($"[✓] Recompiled  ({new FileInfo(outDex).Length / 1024} KB)");
            return true;
        }

        private (int ExitCode, string Stderr) RunJar(string jar, string args)
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "java",
                    Arguments              = $"-jar \"{jar}\" {args}",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                }
            };
            var sb = new StringBuilder();
            proc.ErrorDataReceived  += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) Log($"  {e.Data}"); };
            proc.Start();
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();
            proc.WaitForExit();
            return (proc.ExitCode, sb.ToString());
        }

        // ── JAR REPACK ───────────────────────────────────────────────────────
        private void RepackJar(string srcJar, string patchedDex, string outJar)
        {
            File.Copy(srcJar, outJar, overwrite: true);

            // Rebuild cleanly — replace classes.dex entries
            var tmp = outJar + ".tmp";
            using (var src = ZipFile.OpenRead(srcJar))
            using (var dst = ZipFile.Open(tmp, ZipArchiveMode.Create))
            {
                foreach (var entry in src.Entries)
                {
                    if (entry.Name == "classes.dex")
                    {
                        dst.CreateEntryFromFile(patchedDex, "classes.dex",
                            CompressionLevel.Optimal);
                    }
                    else
                    {
                        using var srcStream = entry.Open();
                        var dstEntry = dst.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                        using var dstStream = dstEntry.Open();
                        srcStream.CopyTo(dstStream);
                    }
                }
            }
            File.Move(tmp, outJar, overwrite: true);
            Log($"[✓] Repacked JAR  ({new FileInfo(outJar).Length / 1024} KB)");
        }

        // ── LOGGING ──────────────────────────────────────────────────────────
        private void Log(string msg)
        {
            if (InvokeRequired)
            {
                Invoke(() => Log(msg));
                return;
            }
            _logBox.AppendText(msg + "\n");
            _logBox.ScrollToCaret();
        }

        // ── DESIGNER STUB ────────────────────────────────────────────────────
        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleDimensions = new System.Drawing.SizeF(7f, 15f);
            AutoScaleMode       = AutoScaleMode.Font;
            ResumeLayout(false);
        }
    }
}

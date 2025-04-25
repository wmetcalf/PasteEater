using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace PasteEater
{
    // Updated Rule class to include target option
    public enum RuleTargetType
    {
        All,        // Match against all text types (raw, normalized, decoded)
        Raw,        // Match against the original raw clipboard text only
        Normalized, // Match against the normalized text
        Decoded,    // Match against any decoded strings found
        Base64,     // Match against Base64 decoded content
        Hex,        // Match against Hex decoded content
        Decimal     // Match against Decimal decoded content
    }
    
    // Helper class for icon creation that can be used by both MainForm and AlertForm
    public static class IconHelper
    {
        public static Icon CreateRobotFistIcon()
        {
            Bitmap bmp = CreateRobotFistImage();
            return Icon.FromHandle(bmp.GetHicon());
        }
        
        public static Bitmap CreateRobotFistImage()
        {
            // Create a glue bottle with prohibition symbol (no-smoking style)
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                
                // Draw glue bottle (blue)
                using (SolidBrush bottleBrush = new SolidBrush(Color.RoyalBlue))
                {
                    // Bottle body
                    g.FillRectangle(bottleBrush, 8, 12, 16, 18);
                    // Bottle neck
                    g.FillRectangle(bottleBrush, 14, 8, 4, 4);
                    // Bottle cap
                    g.FillRectangle(bottleBrush, 12, 4, 8, 4);
                }
                
                // Draw bottle highlights (lighter blue)
                using (SolidBrush highlightBrush = new SolidBrush(Color.LightSkyBlue))
                {
                    // Bottle body
                    g.FillRectangle(highlightBrush, 10, 14, 3, 14);
                }
                
                // Draw glue drips
                using (SolidBrush glueBrush = new SolidBrush(Color.White))
                {
                    // Glue dripping from bottle
                    g.FillRectangle(glueBrush, 15, 8, 2, 2);
                    g.FillRectangle(glueBrush, 10, 12, 2, 3);
                    g.FillRectangle(glueBrush, 20, 13, 2, 4);
                }
                
                // Draw outlines
                using (Pen outlinePen = new Pen(Color.Black, 1))
                {
                    // Bottle outline
                    g.DrawRectangle(outlinePen, 8, 12, 16, 18);
                    g.DrawRectangle(outlinePen, 14, 8, 4, 4);
                    g.DrawRectangle(outlinePen, 12, 4, 8, 4);
                }
                
                // Draw the prohibition circle and line (red)
                using (Pen redPen = new Pen(Color.Red, 2))
                {
                    // Circle
                    g.DrawEllipse(redPen, 3, 3, 26, 26);
                    // Diagonal line (from top-left to bottom-right)
                    g.DrawLine(redPen, 7, 7, 25, 25);
                }
            }
            return bmp;
        }
        
        public static Bitmap CreateLockImage()
        {
            Bitmap bmp = new Bitmap(22, 22);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                
                // Lock body
                using (SolidBrush lockBrush = new SolidBrush(Color.DarkRed))
                using (Pen lockPen = new Pen(Color.DarkRed))
                {
                    // Lock body
                    g.FillRectangle(lockBrush, 3, 11, 16, 9);
                    g.DrawRectangle(lockPen, 3, 11, 16, 9);
                    
                    // Lock shackle
                    g.DrawArc(lockPen, 5, 3, 12, 16, 180, 180);
                }
                
                // Keyhole
                using (Pen holePen = new Pen(Color.White))
                {
                    g.DrawEllipse(holePen, 10, 13, 2, 2);
                    g.DrawLine(holePen, 11, 15, 11, 18);
                }
            }
            return bmp;
        }
        
        public static Bitmap CreateUnlockImage()
        {
            Bitmap bmp = new Bitmap(22, 22);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                
                // Lock body
                using (SolidBrush lockBrush = new SolidBrush(Color.Green))
                using (Pen lockPen = new Pen(Color.Green))
                {
                    // Lock body
                    g.FillRectangle(lockBrush, 3, 11, 16, 9);
                    g.DrawRectangle(lockPen, 3, 11, 16, 9);
                    
                    // Lock shackle (open)
                    g.DrawArc(lockPen, 1, 3, 12, 16, 270, 180);
                }
                
                // Keyhole
                using (Pen holePen = new Pen(Color.White))
                {
                    g.DrawEllipse(holePen, 10, 13, 2, 2);
                    g.DrawLine(holePen, 11, 15, 11, 18);
                }
            }
            return bmp;
        }
    }

    public partial class MainForm : Form
    {
        // Windows API imports
        [DllImport("user32.dll")]
        private static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);
        [DllImport("user32.dll")]
        private static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private IntPtr _nextClipboardViewer;
        private NotifyIcon? _trayIcon;
        private ContextMenuStrip? _trayMenu;
        private bool _debugMode = false;
        private string _rulesPath = string.Empty; // Initialize to empty string
        private bool _useFileLogging = false;
        private string _logFilePath = "";
        private string _updateUrl = "";
        private int _updateIntervalMinutes = 0;
        private System.Timers.Timer? _updateTimer = null; // Initialize as nullable
        private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string StartupRegistryName = "PasteEater";
        private bool _startWithWindows = false;
        private bool _matrixMode = true; // Enable Matrix style by default
        private class PatternEntry
        {
            public Regex? Pattern { get; set; }
            public bool Negated { get; set; }
            public RuleTargetType Target { get; set; } = RuleTargetType.All; // Default to original text
        }
        private class Rule
        {
            public string? RuleId { get; set; }
            public string? Name { get; set; }
            public List<PatternEntry> Patterns { get; set; } = new List<PatternEntry>();
            public RuleTargetType Target { get; set; } = RuleTargetType.All; // Default to original text
        }
        private List<Rule> _rules = new List<Rule>();
        private string[] _browserProcesses = { "chrome", "firefox", "msedge", "opera", "brave", "iexplore" };

        private const string EventLogSource = "PasteEater";
        private const string EventLogName = "Application"; // Use standard Application log
        private const string RegistryKey = @"Software\PasteEater";

        // Matrix colors
        private static readonly Color MatrixBackgroundColor = Color.FromArgb(0, 10, 0);
        private static readonly Color MatrixTextColor = Color.FromArgb(0, 255, 70);
        private static readonly Color MatrixTextHighlightColor = Color.FromArgb(180, 255, 180);

        public MainForm()
        {
            InitializeComponent();
            
            // Hide the window completely - make sure it doesn't appear in Alt+Tab
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
            Opacity = 0;
            
            // Make form invisible as a window
            Size = new Size(1, 1);
            Visible = false;
            
            LoadPreferences();
            
            // Set up logging - try event log first, fall back to file logging
            SetupLogging();
            
            CreateTrayIcon();
            LoadRules();
            _nextClipboardViewer = SetClipboardViewer(this.Handle);

            // Setup rule update timer if configured
            SetupRuleUpdateTimer();
        }
        
        // Override OnShown to ensure the form stays hidden
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Hide();
        }
        
        // Override OnActivated to ensure the form stays hidden if activated
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            Hide();
        }
        
        // Override OnLoad to ensure the form stays hidden
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            BeginInvoke(new Action(() => Hide()));
        }

        private void SetupLogging()
        {
            try 
            {
                // Just use the standard Application log with a distinct message source
                _useFileLogging = false;
                LogMessage("Logging initialized using Windows Event Log", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                // Fall back to file logging
                _useFileLogging = true;
                _logFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PasteEater",
                    "logs.txt"
                );
                
                // Ensure the directory exists
                string? logDirectory = Path.GetDirectoryName(_logFilePath);
                if (logDirectory != null)
                {
                    Directory.CreateDirectory(logDirectory);
                }
                
                // Log the issue to the file
                try
                {
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Warning] Event log access failed: {ex.Message}. Using file logging instead.";
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
                catch { /* Ignore if we can't even write to the log file */ }
                
                // Show a more helpful notification
                using (var msgForm = new Form())
                {
                    msgForm.Text = "Logging Changed";
                    msgForm.Size = new Size(450, 240);
                    msgForm.StartPosition = FormStartPosition.CenterScreen;
                    msgForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    msgForm.MaximizeBox = false;
                    msgForm.MinimizeBox = false;
                    
                    TableLayoutPanel panel = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        Padding = new Padding(10),
                        RowCount = 3,
                        ColumnCount = 1
                    };
                    
                    Label msgLabel = new Label
                    {
                        Text = "Could not create Windows Event Log source. This typically requires administrator privileges." +
                               "\r\n\r\nUsing file logging instead. Log entries will be saved to:",
                        AutoSize = true
                    };
                    panel.Controls.Add(msgLabel, 0, 0);
                    
                    TextBox pathBox = new TextBox
                    {
                        Text = _logFilePath,
                        ReadOnly = true,
                        Dock = DockStyle.Fill
                    };
                    panel.Controls.Add(pathBox, 0, 1);
                    
                    Button okButton = new Button
                    {
                        Text = "OK",
                        DialogResult = DialogResult.OK,
                        Dock = DockStyle.Right
                    };
                    panel.Controls.Add(okButton, 0, 2);
                    
                    msgForm.Controls.Add(panel);
                    msgForm.AcceptButton = okButton;
                    
                    msgForm.ShowDialog();
                }
            }
        }

        private void LoadPreferences()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKey))
                {
                    if (key != null)
                    {
                        _debugMode = GetRegistryValue<bool>(key, "DebugMode", false);
                        _rulesPath = GetRegistryValue<string>(key, "RulesPath", 
                            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rules.json"));
                        _updateUrl = GetRegistryValue<string>(key, "UpdateUrl", "");
                        _updateIntervalMinutes = GetRegistryValue<int>(key, "UpdateIntervalMinutes", 0);
                        _startWithWindows = GetRegistryValue<bool>(key, "StartWithWindows", false);
                        
                        // Ensure startup registry is in sync with our preferences
                        UpdateStartupRegistry();
                        
                        if (_debugMode)
                        {
                            ShowDebugMessage("Preferences loaded from registry");
                        }
                    }
                    else
                    {
                        // Set defaults
                        _debugMode = false;
                        _rulesPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rules.json");
                        _updateUrl = "";
                        _updateIntervalMinutes = 0;
                        _startWithWindows = false;
                        
                        // Create registry key on first run
                        SavePreferences();
                    }
                }
            }
            catch (Exception ex)
            {
                // Fall back to defaults if registry access fails
                _debugMode = false;
                _rulesPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rules.json");
                _updateUrl = "";
                _updateIntervalMinutes = 0;
                _startWithWindows = false;
                
                Console.WriteLine($"Failed to load preferences: {ex.Message}");
            }
        }

        private void SavePreferences()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryKey))
                {
                    if (key != null)
                    {
                        key.SetValue("DebugMode", _debugMode, RegistryValueKind.DWord);
                        key.SetValue("RulesPath", _rulesPath, RegistryValueKind.String);
                        key.SetValue("UpdateUrl", _updateUrl, RegistryValueKind.String);
                        key.SetValue("UpdateIntervalMinutes", _updateIntervalMinutes, RegistryValueKind.DWord);
                        key.SetValue("StartWithWindows", _startWithWindows, RegistryValueKind.DWord);
                        
                        // Update startup registry entry
                        UpdateStartupRegistry();
                        
                        if (_debugMode)
                        {
                            ShowDebugMessage("Preferences saved to registry");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save preferences: {ex.Message}");
            }
        }
        
        private void UpdateStartupRegistry()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true))
                {
                    if (key != null)
                    {
                        if (_startWithWindows)
                        {
                            // Add application to startup
                            string appPath = Application.ExecutablePath;
                            key.SetValue(StartupRegistryName, $"\"{appPath}\"");
                        }
                        else
                        {
                            // Remove application from startup if exists
                            if (key.GetValue(StartupRegistryName) != null)
                            {
                                key.DeleteValue(StartupRegistryName, false);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowDebugMessage($"Failed to update startup registry: {ex.Message}");
            }
        }

        private T GetRegistryValue<T>(RegistryKey key, string valueName, T defaultValue)
        {
            object? value = key.GetValue(valueName);
            if (value == null)
                return defaultValue;
                
            try
            {
                if (typeof(T) == typeof(bool))
                    return (T)(object)(Convert.ToInt32(value) != 0);
                    
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        private void LoadRules()
        {
            if (!System.IO.File.Exists(_rulesPath))
            {
                if (_debugMode)
                {
                    ShowDebugMessage($"Rules file not found at: {_rulesPath}");
                }
                return;
            }
            
            try
            {
                var json = System.IO.File.ReadAllText(_rulesPath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var seenRuleIds = new HashSet<string>();
                foreach (var ruleElem in doc.RootElement.EnumerateArray())
                {
                    string? ruleId = ruleElem.TryGetProperty("rule_id", out var rid) ? rid.GetString() : null;
                    if (string.IsNullOrWhiteSpace(ruleId))
                    {
                        string msg = $"Rule skipped: missing rule_id (name: '{(ruleElem.TryGetProperty("name", out var n1) ? n1.GetString() : "Unnamed Rule")}')";
                        MessageBox.Show(msg, "Rule Load Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        try {
                            string source = "PasteEater";
                            string log = "Application";
                            if (!System.Diagnostics.EventLog.SourceExists(source))
                                System.Diagnostics.EventLog.CreateEventSource(source, log);
                            System.Diagnostics.EventLog.WriteEntry(source, msg, System.Diagnostics.EventLogEntryType.Warning);
                        } catch {}
                        continue;
                    }
                    if (!seenRuleIds.Add(ruleId))
                    {
                        string msg = $"Rule skipped: duplicate rule_id '{ruleId}' (name: '{(ruleElem.TryGetProperty("name", out var n2) ? n2.GetString() : "Unnamed Rule")}')";
                        MessageBox.Show(msg, "Rule Load Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        try {
                            string source = "PasteEater";
                            string log = "Application";
                            if (!System.Diagnostics.EventLog.SourceExists(source))
                                System.Diagnostics.EventLog.CreateEventSource(source, log);
                            System.Diagnostics.EventLog.WriteEntry(source, msg, System.Diagnostics.EventLogEntryType.Warning);
                        } catch {}
                        continue;
                    }
                    string? name = ruleElem.TryGetProperty("name", out var n) ? n.GetString() : "Unnamed Rule";
                    
                    // Parse the rule target (for backward compatibility with older rule files)
                    RuleTargetType ruleTarget = RuleTargetType.All; // Default target type
                    if (ruleElem.TryGetProperty("target", out var targetProp))
                    {
                        string? targetStr = targetProp.GetString();
                        if (!string.IsNullOrEmpty(targetStr))
                        {
                            switch (targetStr.ToLowerInvariant())
                            {
                                case "normalized":
                                    ruleTarget = RuleTargetType.Normalized;
                                    break;
                                case "decoded":
                                    ruleTarget = RuleTargetType.Decoded;
                                    break;
                                default:
                                    ruleTarget = RuleTargetType.All;
                                    break;
                            }
                        }
                    }
                    
                    var rule = new Rule { 
                        RuleId = ruleId, 
                        Name = name ?? "Unnamed Rule",
                        Target = ruleTarget
                    };
                    
                    if (ruleElem.TryGetProperty("patterns", out var patternsElem) && patternsElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var patElem in patternsElem.EnumerateArray())
                        {
                            string? pattern = patElem.TryGetProperty("pattern", out var p) ? p.GetString() : "";
                            string? flags = patElem.TryGetProperty("re_flags", out var f) ? f.GetString() : "";
                            
                            // Support both "negate" and "negated" property names for backwards compatibility
                            bool negated = (patElem.TryGetProperty("negate", out var neg1) && neg1.GetBoolean()) || 
                                         (patElem.TryGetProperty("negated", out var neg2) && neg2.GetBoolean());
                            
                            // Parse pattern-specific target type (overrides rule-level target)
                            RuleTargetType patternTarget = ruleTarget; // Default to rule's target
                            if (patElem.TryGetProperty("target", out var patTargetProp))
                            {
                                string? patTargetStr = patTargetProp.GetString();
                                if (!string.IsNullOrEmpty(patTargetStr))
                                {
                                    switch (patTargetStr.ToLowerInvariant())
                                    {
                                        case "normalized":
                                            patternTarget = RuleTargetType.Normalized;
                                            break;
                                        case "decoded":
                                            patternTarget = RuleTargetType.Decoded;
                                            break;
                                        case "raw":
                                            patternTarget = RuleTargetType.Raw;
                                            break;
                                        case "all":
                                            patternTarget = RuleTargetType.All;
                                            break;
                                        case "base64":
                                            patternTarget = RuleTargetType.Base64;
                                            break;
                                        case "hex":
                                            patternTarget = RuleTargetType.Hex;
                                            break;
                                        case "decimal":
                                            patternTarget = RuleTargetType.Decimal;
                                            break;
                                        // Ignore invalid values
                                    }
                                }
                            }
                            
                            RegexOptions opts = RegexOptions.Compiled;
                            if (flags?.Contains("i") == true) opts |= RegexOptions.IgnoreCase;
                            if (flags?.Contains("s") == true) opts |= RegexOptions.Singleline;
                            if (flags?.Contains("m") == true) opts |= RegexOptions.Multiline;
                            
                            rule.Patterns.Add(new PatternEntry { 
                                Pattern = !string.IsNullOrEmpty(pattern) ? new Regex(pattern, opts) : null, 
                                Negated = negated,
                                Target = patternTarget
                            });
                        }
                    }
                    _rules.Add(rule);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load rules: {ex.Message}\nPath: {_rulesPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CreateTrayIcon()
        {
            _trayMenu = new ContextMenuStrip();
            
            ToolStripMenuItem? debugMenuItem = new ToolStripMenuItem("Toggle Debug Mode", null, (s, e) => {
                _debugMode = !_debugMode;
                if (s is ToolStripMenuItem menuItem)
                {
                    menuItem.Checked = _debugMode;
                }
                SavePreferences();
                ShowDebugMessage($"Debug mode {(_debugMode ? "enabled" : "disabled")}");
            });
            debugMenuItem.CheckOnClick = true;
            _trayMenu.Items.Add(debugMenuItem);
            
            ToolStripMenuItem? startupMenuItem = new ToolStripMenuItem("Start with Windows", null, (s, e) => {
                _startWithWindows = !_startWithWindows;
                if (s is ToolStripMenuItem menuItem)
                {
                    menuItem.Checked = _startWithWindows;
                }
                SavePreferences();
                ShowDebugMessage($"Start with Windows {(_startWithWindows ? "enabled" : "disabled")}");
            });
            startupMenuItem.CheckOnClick = true;
            _trayMenu.Items.Add(startupMenuItem);
            
            _trayMenu.Items.Add("Configure Rules Path", null, (s, e) => ConfigureRulesPath());
            _trayMenu.Items.Add("Reload Rules", null, (s, e) => ReloadRules());
            _trayMenu.Items.Add("Configure Rule Updates", null, (s, e) => ConfigureRuleUpdates());
            _trayMenu.Items.Add("Check for Rule Updates", null, (s, e) => CheckForRuleUpdatesAsync().ConfigureAwait(false));
            _trayMenu.Items.Add("-"); // Separator
            _trayMenu.Items.Add("Exit", null, (s, e) => Application.Exit());
            
            _trayIcon = new NotifyIcon()
            {
                Icon = IconHelper.CreateRobotFistIcon(),  // Use custom robot fist icon instead of SystemIcons.Information
                ContextMenuStrip = _trayMenu,
                Visible = true,
                Text = "PasteEater"
            };
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_DRAWCLIPBOARD = 0x308;
            const int WM_CHANGECBCHAIN = 0x030D;
            switch (m.Msg)
            {
                case WM_DRAWCLIPBOARD:
                    OnClipboardChanged();
                    SendMessage(_nextClipboardViewer, m.Msg, m.WParam, m.LParam);
                    break;
                case WM_CHANGECBCHAIN:
                    if (m.WParam == _nextClipboardViewer)
                        _nextClipboardViewer = m.LParam;
                    else
                        SendMessage(_nextClipboardViewer, m.Msg, m.WParam, m.LParam);
                    break;
                default:
                    base.WndProc(ref m);
                    break;
            }
        }

        private string _lastClipboardText = "";
        private List<DecodedString> _lastDecodedStrings = new List<DecodedString>();

        private void OnClipboardChanged()
        {
            try
            {
                string foregroundProc = GetForegroundProcessName();
                
                if (!Clipboard.ContainsText()) 
                {
                    if (_debugMode)
                    {
                        ShowDebugMessage($"Clipboard changed (non-text). Foreground window: {foregroundProc}");
                    }
                    return;
                }
                
                string text = Clipboard.GetText();
                _lastClipboardText = text; // Store for full view
                
                // Process clipboard content to generate all target types
                // Pass debug mode flag and callback function for verbose logging
                ClipboardTargets targets = ObfuscationHelper.ProcessClipboardContent(
                    text, 
                    _debugMode,  // Pass debug flag
                    _debugMode ? ShowDebugMessage : null  // Pass debug callback only if debug mode is enabled
                );
                
                _lastDecodedStrings = targets.Decoded; // Store for display
                
                if (_debugMode)
                {
                    string debugMsg = $"Clipboard changed. Foreground window: {foregroundProc}";
                    debugMsg += $"\nText ({text.Length} chars): {text.Substring(0, Math.Min(50, text.Length))}";
                    if (text.Length > 50) debugMsg += "...";
                    
                    if (targets.Decoded.Count > 0)
                    {
                        debugMsg += $"\nFound {targets.Decoded.Count} potentially obfuscated strings:";
                        foreach (var category in new[] { 
                            ("Base64", targets.Base64), 
                            ("Hex", targets.Hex), 
                            ("Decimal", targets.Decimal) 
                        })
                        {
                            if (category.Item2.Count > 0)
                            {
                                debugMsg += $"\n- {category.Item1} ({category.Item2.Count}): ";
                                var sample = category.Item2.FirstOrDefault() ?? "";
                                debugMsg += sample.Substring(0, Math.Min(30, sample.Length));
                                if (sample.Length > 30) debugMsg += "...";
                            }
                        }
                    }
                    else 
                    {
                        debugMsg += "\nNo decoded strings found.";
                    }
                    
                    ShowDebugMessage(debugMsg);
                }
                
                if (string.IsNullOrEmpty(text)) return;

                if (IsBrowserProcess(foregroundProc))
                {
                    foreach (var rule in _rules)
                    {
                        // Track normal patterns and negated patterns separately
                        bool normalPatternsMatch = true;
                        bool negatedPatternsMatch = true;
                        
                        // First, process all patterns to gather information
                        foreach (var pat in rule.Patterns)
                        {
                            if (pat.Pattern == null) continue;
                            
                            bool isMatch = false;
                            
                            // Use the target types for pattern matching
                            switch (pat.Target)
                            {
                                case RuleTargetType.All:
                                    // Match against all strings: original, normalized, and decoded
                                    isMatch = targets.All.Any(text => 
                                        !string.IsNullOrEmpty(text) && pat.Pattern.IsMatch(text));
                                    break;
                                    
                                case RuleTargetType.Raw:
                                    // Match against raw clipboard text only
                                    isMatch = pat.Pattern.IsMatch(targets.Raw);
                                    break;
                                    
                                case RuleTargetType.Normalized:
                                    // Match against normalized text (quotes/special chars removed)
                                    isMatch = pat.Pattern.IsMatch(targets.Normalized);
                                    break;
                                    
                                case RuleTargetType.Decoded:
                                    // Try to match against any decoded string
                                    isMatch = targets.Decoded.Any(decodedStr => 
                                        !string.IsNullOrEmpty(decodedStr.Decoded) && pat.Pattern.IsMatch(decodedStr.Decoded));
                                    break;
                                    
                                case RuleTargetType.Base64:
                                    // Match only against Base64-decoded content
                                    isMatch = targets.Base64.Any(base64Text => 
                                        !string.IsNullOrEmpty(base64Text) && pat.Pattern.IsMatch(base64Text));
                                    break;
                                    
                                case RuleTargetType.Hex:
                                    // Match only against Hex-decoded content
                                    isMatch = targets.Hex.Any(hexText => 
                                        !string.IsNullOrEmpty(hexText) && pat.Pattern.IsMatch(hexText));
                                    break;
                                    
                                case RuleTargetType.Decimal:
                                    // Match only against Decimal-decoded content
                                    isMatch = targets.Decimal.Any(decimalText => 
                                        !string.IsNullOrEmpty(decimalText) && pat.Pattern.IsMatch(decimalText));
                                    break;
                            }
                            
                            if (_debugMode)
                            {
                                ShowDebugMessage($"Rule '{rule.Name}' pattern match: {isMatch}, negated: {pat.Negated}, target: {pat.Target}");
                            }
                            
                            // Handle negated patterns separately from regular patterns
                            if (pat.Negated)
                            {
                                // For negated patterns, we expect them NOT to match
                                // If pattern matches when it shouldn't, the rule is violated
                                if (isMatch)
                                {
                                    negatedPatternsMatch = false;
                                    if (_debugMode)
                                    {
                                        ShowDebugMessage($"Negated pattern violation detected: '{pat.Pattern}' matched when it shouldn't");
                                    }
                                }
                            }
                            else
                            {
                                // For regular patterns, we need all to match
                                if (!isMatch)
                                {
                                    normalPatternsMatch = false;
                                    if (_debugMode)
                                    {
                                        ShowDebugMessage($"Regular pattern failed to match: '{pat.Pattern}'");
                                    }
                                }
                            }
                        }
                        
                        // A rule matches when:
                        // 1. All non-negated patterns match AND
                        // 2. All negated patterns do not match (negatedPatternsMatch = true)
                        bool ruleMatches = normalPatternsMatch && negatedPatternsMatch;
                        
                        if (_debugMode)
                        {
                            ShowDebugMessage($"Rule '{rule.Name}' final result: {ruleMatches} " +
                                $"(normalPatternsMatch={normalPatternsMatch}, " +
                                $"negatedPatternsMatch={negatedPatternsMatch})");
                        }
                        
                        if (ruleMatches)
                        {
                            // Get command line of the foreground process
                            string cmdLine = GetProcessCommandLine(foregroundProc);
                            
                            // Log the matched content
                            LogEvent(rule.Name ?? "Unnamed Rule", text, cmdLine);
                            
                            // Show custom icon alert with clipboard content
                            using (AlertForm alert = new AlertForm(rule.Name ?? "Unnamed Rule", text, targets.Decoded, _matrixMode))
                            {
                                alert.ShowDialog();
                            }
                            
                            // Clear the clipboard
                            Clipboard.Clear();
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_debugMode)
                {
                    ShowDebugMessage($"Error in clipboard processing: {ex.Message}\n{ex.StackTrace}");
                }
                // Optionally log or notify
            }
        }

        private string GetForegroundProcessName()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return string.Empty;
            GetWindowThreadProcessId(hwnd, out uint pid);
            try
            {
                Process proc = Process.GetProcessById((int)pid);
                return proc.ProcessName.ToLower();
            }
            catch { return string.Empty; }
        }

        private bool IsBrowserProcess(string processName)
        {
            foreach (var browser in _browserProcesses)
                if (processName.Contains(browser))
                    return true;
            return false;
        }

        private string GetProcessCommandLine(string processName)
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        // Use ProcessStartInfo to capture basic process information instead of WMI
                        return $"{proc.ProcessName} (PID: {proc.Id}, Started: {proc.StartTime})";
                    }
                    catch { /* Continue with next process */ }
                }
            }
            catch (Exception ex)
            {
                if (_debugMode)
                {
                    ShowDebugMessage($"Failed to get process info: {ex.Message}");
                }
            }
            return "Unknown";
        }

        private void LogEvent(string ruleName, string clipboardText, string commandLine)
        {
            try
            {
                string msg = $"Clipboard blocked by rule: {ruleName}\r\n" +
                             $"Process command line: {commandLine}\r\n" +
                             $"Content: {clipboardText}";
                
                LogMessage(msg, EventLogEntryType.Warning);
            }
            catch (Exception ex) 
            { 
                if (_debugMode)
                {
                    ShowDebugMessage($"Failed to log event: {ex.Message}");
                }
            }
        }

        private void LogMessage(string message, EventLogEntryType entryType)
        {
            try
            {
                if (_useFileLogging)
                {
                    // Write to file with timestamp
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{entryType}] {message.Replace("\r\n", " | ")}";
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
                else
                {
                    // Use a simpler event source that doesn't require registration
                    EventLog.WriteEntry("Application", $"[PasteEater] {message}", entryType);
                }
            }
            catch (Exception ex)
            {
                // If event log fails, try to fall back to file logging
                try 
                {
                    if (!_useFileLogging)
                    {
                        _useFileLogging = true;
                        _logFilePath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "PasteEater",
                            "logs.txt"
                        );
                        string? logDir = Path.GetDirectoryName(_logFilePath);
                        if (logDir != null)
                        {
                            Directory.CreateDirectory(logDir);
                        }
                    }
                    
                    // Write to file with timestamp
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{entryType}] {message.Replace("\r\n", " | ")}";
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
                catch
                {
                    Console.WriteLine($"Failed to log message: {ex.Message}");
                }
            }
        }

        private void ShowDebugMessage(string message)
        {
            if (!_debugMode || _trayIcon == null) return;
            
            // Truncate message for balloon tip if too long
            string balloonMessage = message;
            if (balloonMessage.Length > 200)
            {
                balloonMessage = balloonMessage.Substring(0, 197) + "...";
            }
            
            _trayIcon.BalloonTipTitle = "Clipboard Debug";
            _trayIcon.BalloonTipText = balloonMessage;
            _trayIcon.ShowBalloonTip(3000);
            
            try
            {
                LogMessage($"DEBUG: {message}", EventLogEntryType.Information);
            }
            catch (Exception ex) 
            { 
                // Cannot show a message here or we might get into a loop
                Console.WriteLine($"Failed to write to log: {ex.Message}");
            }
        }

        private void ConfigureRulesPath()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                openFileDialog.Title = "Select Rules File";
                openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(_rulesPath);
                openFileDialog.FileName = System.IO.Path.GetFileName(_rulesPath);

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    _rulesPath = openFileDialog.FileName;
                    SavePreferences();
                    ShowDebugMessage($"Rules path updated to: {_rulesPath}");
                    ReloadRules();
                }
            }
        }

        private void ReloadRules()
        {
            _rules.Clear();
            LoadRules();
            ShowDebugMessage($"Rules reloaded from: {_rulesPath}");
        }

        private void ConfigureRuleUpdates()
        {
            using (var form = new Form())
            {
                form.Text = "Configure Rule Updates";
                form.Size = new Size(450, 220);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                
                TableLayoutPanel panel = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(10),
                    RowCount = 4,
                    ColumnCount = 2
                };
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
                
                // URL
                Label urlLabel = new Label { Text = "Update URL:", Anchor = AnchorStyles.Left };
                TextBox urlBox = new TextBox { 
                    Text = _updateUrl, 
                    Dock = DockStyle.Fill,
                    PlaceholderText = "https://example.com/rules.json"
                };
                
                // Interval
                Label intervalLabel = new Label { Text = "Update Interval:", Anchor = AnchorStyles.Left };
                NumericUpDown intervalSpinner = new NumericUpDown {
                    Minimum = 0,
                    Maximum = 1440, // 24 hours
                    Value = _updateIntervalMinutes,
                    Dock = DockStyle.Fill
                };
                
                // Interval description
                Label intervalDescLabel = new Label { 
                    Text = "Minutes (0 to disable)",
                    Anchor = AnchorStyles.Left 
                };
                
                // Buttons
                FlowLayoutPanel buttonPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft
                };
                
                Button cancelButton = new Button { Text = "Cancel" };
                cancelButton.Click += (s, e) => form.DialogResult = DialogResult.Cancel;
                
                Button saveButton = new Button { Text = "Save" };
                saveButton.Click += (s, e) => {
                    _updateUrl = urlBox.Text.Trim();
                    _updateIntervalMinutes = (int)intervalSpinner.Value;
                    SavePreferences();
                    SetupRuleUpdateTimer();
                    form.DialogResult = DialogResult.OK;
                };
                
                // Add controls
                panel.Controls.Add(urlLabel, 0, 0);
                panel.Controls.Add(urlBox, 1, 0);
                panel.Controls.Add(intervalLabel, 0, 1);
                panel.Controls.Add(intervalSpinner, 1, 1);
                panel.Controls.Add(intervalDescLabel, 1, 2);
                
                buttonPanel.Controls.Add(saveButton);
                buttonPanel.Controls.Add(cancelButton);
                panel.Controls.Add(buttonPanel, 0, 3);
                
                form.Controls.Add(panel);
                form.AcceptButton = saveButton;
                form.CancelButton = cancelButton;
                
                form.ShowDialog();
            }
        }

        private void SetupRuleUpdateTimer()
        {
            // Dispose of existing timer if any
            if (_updateTimer != null)
            {
                _updateTimer.Stop();
                _updateTimer.Dispose();
                _updateTimer = null;
            }
            
            // If interval is set and URL is provided, create new timer
            if (_updateIntervalMinutes > 0 && !string.IsNullOrWhiteSpace(_updateUrl))
            {
                _updateTimer = new System.Timers.Timer(_updateIntervalMinutes * 60 * 1000); // Convert to milliseconds
                _updateTimer.Elapsed += async (s, e) => await CheckForRuleUpdatesAsync();
            }
        }

        private async Task CheckForRuleUpdatesAsync()
        {
            if (string.IsNullOrWhiteSpace(_updateUrl))
            {
                ShowDebugMessage("Cannot check for updates: Update URL not configured");
                return;
            }
            
            try
            {
                ShowDebugMessage($"Checking for rule updates from {_updateUrl}");
                
                // Create HTTP client
                using (HttpClient client = new HttpClient())
                {
                    // Set a reasonable timeout
                    client.Timeout = TimeSpan.FromSeconds(30);
                    
                    // Download rules
                    HttpResponseMessage response = await client.GetAsync(_updateUrl);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string newRulesJson = await response.Content.ReadAsStringAsync();
                        
                        // Validate the downloaded rules
                        if (ValidateRulesJson(newRulesJson))
                        {
                            // Create backup of current rules
                            string backupPath = _rulesPath + ".bak";
                            if (File.Exists(_rulesPath))
                            {
                                File.Copy(_rulesPath, backupPath, true);
                            }
                            
                            // Save new rules
                            File.WriteAllText(_rulesPath, newRulesJson);
                            
                            // Reload rules
                            _rules.Clear();
                            LoadRules();
                            
                            ShowDebugMessage($"Rules updated successfully from {_updateUrl}");
                        }
                        else
                        {
                            ShowDebugMessage("Downloaded rules failed validation");
                        }
                    }
                    else
                    {
                        ShowDebugMessage($"Failed to download rules: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowDebugMessage($"Error checking for rule updates: {ex.Message}");
            }
        }

        private bool ValidateRulesJson(string json)
        {
            try
            {
                // Try to parse the JSON as a temporary test
                var tempRules = new List<Rule>();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var seenRuleIds = new HashSet<string>();
                
                foreach (var ruleElem in doc.RootElement.EnumerateArray())
                {
                    string? ruleId = ruleElem.TryGetProperty("rule_id", out var rid) ? rid.GetString() : null;
                    
                    // Basic validation
                    if (string.IsNullOrWhiteSpace(ruleId))
                    {
                        return false;
                    }
                    
                    if (!seenRuleIds.Add(ruleId))
                    {
                        return false;
                    }
                    
                    string? name = ruleElem.TryGetProperty("name", out var n) ? n.GetString() : "Unnamed Rule";
                    var rule = new Rule { RuleId = ruleId, Name = name ?? "Unnamed Rule" };
                    
                    // Validate patterns
                    if (ruleElem.TryGetProperty("patterns", out var patternsElem) && patternsElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var patElem in patternsElem.EnumerateArray())
                        {
                            string? pattern = patElem.TryGetProperty("pattern", out var p) ? p.GetString() : "";
                            string? flags = patElem.TryGetProperty("re_flags", out var f) ? f.GetString() : "";
                            bool negate = patElem.TryGetProperty("negate", out var neg) && neg.GetBoolean();
                            
                            // Check if pattern is valid by trying to compile the regex
                            if (!string.IsNullOrEmpty(pattern))
                            {
                                RegexOptions opts = RegexOptions.Compiled;
                                if (flags?.Contains("i") == true) opts |= RegexOptions.IgnoreCase;
                                if (flags?.Contains("s") == true) opts |= RegexOptions.Singleline;
                                if (flags?.Contains("m") == true) opts |= RegexOptions.Multiline;
                                
                                try
                                {
                                    // Test if regex is valid
                                    _ = new Regex(pattern, opts);
                                }
                                catch
                                {
                                    // Invalid regex pattern
                                    return false;
                                }
                            }
                        }
                    }
                    
                    tempRules.Add(rule);
                }
                
                // Ensure we have at least one rule
                return tempRules.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            ChangeClipboardChain(this.Handle, _nextClipboardViewer);
            if (_trayIcon != null)
                _trayIcon.Visible = false;
            base.OnFormClosing(e);
        }
    }

    public class AlertForm : Form
    {
        // Matrix colors
        private static readonly Color MatrixBackgroundColor = Color.FromArgb(0, 10, 0);
        private static readonly Color MatrixTextColor = Color.Black; // Changed to black for better readability
        private static readonly Color MatrixTextHighlightColor = Color.FromArgb(0, 0, 0); // Changed to black for titles

        // Lock state for content boxes
        private bool _contentLocked = true;
        private TextBox _contentBox = null!;
        private TextBox? _decodedTextBox;
        private PictureBox _lockToggleIcon = null!;
        private ToolTip lockTooltip = null!;
        private List<DecodedString> _decodedStrings = new List<DecodedString>();
        private string _originalClipboardText = "";

        public AlertForm(string ruleName, string clipboardContent, List<DecodedString> decodedStrings, bool matrixMode, bool warnOnly = false)
        {
            this.Text = warnOnly ? "Clipboard Warning" : "Clipboard Blocked";
            this.Size = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Icon = IconHelper.CreateRobotFistIcon();
            
            // Store decoded strings for later use
            _decodedStrings = decodedStrings.ToList();
            
            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                RowCount = 6,
                ColumnCount = 1
            };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            
            // Header area with icon and title
            TableLayoutPanel headerPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 0)
            };
            headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50F));
            headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            
            // Icon
            PictureBox iconBox = new PictureBox
            {
                Image = IconHelper.CreateRobotFistImage(),
                Size = new Size(40, 40),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Margin = new Padding(0, 0, 10, 0)
            };
            headerPanel.Controls.Add(iconBox, 0, 0);
            
            // Title text
            Label titleLabel = new Label
            {
                Text = "Potentially Malicious Clipboard Content From The Big Bad Internet! Press the lock if you actually want to copy it.",
                Font = new Font(this.Font.FontFamily, 11, FontStyle.Bold),
                AutoSize = true,
                MaximumSize = new Size(520, 0),
                Dock = DockStyle.Fill
            };
            headerPanel.Controls.Add(titleLabel, 1, 0);
            panel.Controls.Add(headerPanel, 0, 0);
            
            // Rule info with Lock/Unlock toggle
            TableLayoutPanel rulePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 10)
            };
            
            rulePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80F));
            rulePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            
            // Rule label
            Label ruleLabel = new Label
            {
                Text = $"Rule: {ruleName}",
                AutoSize = true,
                Dock = DockStyle.Left
            };
            rulePanel.Controls.Add(ruleLabel, 0, 0);
            
            // Lock/Unlock icon panel (right-aligned)
            FlowLayoutPanel lockPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true
            };
            
            // Lock/Unlock toggle icon
            _lockToggleIcon = new PictureBox
            {
                Image = IconHelper.CreateLockImage(),
                Size = new Size(22, 22),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0)
            };
            
            // Add tooltip to explain functionality
            ToolTip lockTooltip = new ToolTip();
            lockTooltip.SetToolTip(_lockToggleIcon, "Text is currently locked (non-copyable). Click to unlock.");
            
            // Store tooltip for later updates
            this.lockTooltip = lockTooltip;
            
            // Add click handler for lock toggle (fixed nullability)
            _lockToggleIcon.Click += (sender, e) => LockToggle_Click(sender, e);
            
            lockPanel.Controls.Add(_lockToggleIcon);
            rulePanel.Controls.Add(lockPanel, 1, 0);
            
            panel.Controls.Add(rulePanel, 0, 1);
            
            // Clipboard content (always use dark text on light background for better readability)
            _contentBox = new TextBox
            {
                Text = clipboardContent,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = SystemColors.Window,  // Use standard window background
                ForeColor = Color.Black,          // Always use black text
                Font = new Font("Consolas", 10F, FontStyle.Regular)
            };
            
            // Make content non-copyable by default
            MakeTextBoxNonCopyable(_contentBox);
            
            panel.Controls.Add(_contentBox, 0, 2);
            
            // Filter out decoded strings with no printable characters
            var printableDecodedStrings = decodedStrings
                .Where(ds => ContainsPrintableCharacters(ds.Decoded))
                .ToList();
            
            // Only add decoded strings section if we have valid content to show
            if (printableDecodedStrings.Count > 0)
            {
                Label decodedLabel = new Label
                {
                    Text = "Decoded Strings:",
                    AutoSize = true,
                    Margin = new Padding(0, 10, 0, 5)
                };
                panel.Controls.Add(decodedLabel, 0, 3);
                
                // Use TextBox instead of ListBox for better display of multi-line decoded content
                _decodedTextBox = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    BackColor = SystemColors.Window,  // Use standard window background
                    ForeColor = Color.Black,          // Always use black text
                    Font = new Font("Consolas", 10F, FontStyle.Regular),
                    WordWrap = true
                };
                
                // Make decoded content non-copyable by default
                MakeTextBoxNonCopyable(_decodedTextBox);
                
                // Build the decoded content text with proper formatting
                StringBuilder sb = new StringBuilder();
                foreach (var decoded in printableDecodedStrings)
                {
                    sb.AppendLine($"[{decoded.DecodingMethod}]");
                    sb.AppendLine(decoded.Decoded);
                    sb.AppendLine(new string('-', 40)); // Separator between entries
                }
                
                _decodedTextBox.Text = sb.ToString();
                panel.Controls.Add(_decodedTextBox, 0, 4);
            }
            
            // Button row - only OK button now
            FlowLayoutPanel buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 10, 0, 0)
            };

            Button closeButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                AutoSize = true,
                Margin = new Padding(5, 0, 0, 0)
            };
            buttonPanel.Controls.Add(closeButton);
            
            // Store the original clipboard text
            _originalClipboardText = clipboardContent;
            
            // Add controls to panel
            panel.Controls.Add(buttonPanel, 0, 5);
            
            this.Controls.Add(panel);
            this.AcceptButton = closeButton;
            
            // Apply form styling
            this.BackColor = SystemColors.Control; // Use standard control background
            titleLabel.ForeColor = Color.Black;    // Black text for title
            ruleLabel.ForeColor = Color.Black;     // Black text for rule label
            closeButton.BackColor = SystemColors.Control;
            closeButton.ForeColor = Color.Black;
            
            if (printableDecodedStrings.Count > 0)
            {
                panel.Controls[3].ForeColor = Color.Black; // decodedLabel
            }
        }
        
        // Helper method to check if a string contains printable characters
        private bool ContainsPrintableCharacters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            
            // Count printable characters (including spaces and common punctuation)
            int printableCount = text.Count(c => !char.IsControl(c) || c == '\t' || c == '\r' || c == '\n');
            
            // Consider it printable if at least 3 printable characters or 5% of characters are printable
            return printableCount >= 3 || (text.Length > 0 && (double)printableCount / text.Length >= 0.05);
        }
        
        // Helper method to truncate text for display
        private string TruncateForDisplay(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            
            return text.Length <= maxLength 
                ? text 
                : text.Substring(0, maxLength) + "...";
        }
        
        // Event handler for lock/unlock toggle (fixed nullability)
        private void LockToggle_Click(object? sender, EventArgs e)
        {
            _contentLocked = !_contentLocked;
            
            // Update lock state
            if (_contentLocked)
            {
                _lockToggleIcon.Image = IconHelper.CreateLockImage();
                MakeTextBoxNonCopyable(_contentBox);
                if (_decodedTextBox != null) MakeTextBoxNonCopyable(_decodedTextBox);
                
                // Update tooltip text with the correct message
                lockTooltip.SetToolTip(_lockToggleIcon, "Text is currently locked (non-copyable). Click to unlock.");
            }
            else
            {
                _lockToggleIcon.Image = IconHelper.CreateUnlockImage();
                MakeTextBoxCopyable(_contentBox);
                if (_decodedTextBox != null) MakeTextBoxCopyable(_decodedTextBox);
                
                // Update tooltip text with the correct message
                lockTooltip.SetToolTip(_lockToggleIcon, "Text is currently unlocked (copyable). Click to lock.");
            }
        }
        
        // Make a TextBox non-copyable (fixed method)
        private void MakeTextBoxNonCopyable(TextBox textBox)
        {
            textBox.ReadOnly = true;
            textBox.ContextMenuStrip = new ContextMenuStrip(); // Empty context menu
            
            // Add keyboard handler to prevent copy
            textBox.KeyDown += TextBox_BlockCopyKeyDown;
            
            // Change background color to indicate locked state but keep text black
            textBox.BackColor = SystemColors.Control;
            textBox.ForeColor = Color.Black;
        }
        
        // Key handler to block copy operations
        private void TextBox_BlockCopyKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && (e.KeyCode == Keys.C || e.KeyCode == Keys.Insert))
            {
                e.Handled = true; // Block Ctrl+C
                e.SuppressKeyPress = true;
            }
        }
        
        // Make a TextBox copyable (fixed method)
        private void MakeTextBoxCopyable(TextBox textBox)
        {
            textBox.ReadOnly = true; // Keep it read-only, but allow copy
            textBox.ContextMenuStrip = null; // Allow default context menu
            
            // Remove KeyDown handler that blocks Ctrl+C
            textBox.KeyDown -= TextBox_BlockCopyKeyDown;
            
            // Change background color to indicate unlocked state but keep text black
            textBox.BackColor = SystemColors.Window;
            textBox.ForeColor = Color.Black;
        }
    }
}

using System;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ScintillaNET;
using Timer = System.Windows.Forms.Timer;

namespace MyEngineEditor
{
    public class EditorForm : Form
    {
        private Scintilla _codeEditor;
        private PictureBox _viewport;
        private RichTextBox _console;
        private Timer _engineTimer;
        private Stopwatch _gameTime;
        private object _scriptInstance;
        private MethodInfo _start, _update, _draw;
        private bool _isPlaying = false;
        private long _lastFrame;

        public EditorForm()
        {
            this.Text = "protagonist editor-v0.1.0 alpha1";
            this.Size = new Size(1200, 800);

            // 1. 初始化編輯器
            _codeEditor = new Scintilla { Dock = DockStyle.Left, Width = 500 };
            
            // 2. 先設定樣式，再寫入文字
            SetupSyntaxHighlighting();
            _codeEditor.Text = "public class GameBehavior {\n    public void Start() { }\n    public void Update(float dt) { }\n    public void Draw(System.Drawing.Graphics g) { }\n}";

            // 3. 工具列與視窗
            ToolStrip ts = new ToolStrip { BackColor = Color.FromArgb(45, 45, 48) };
            ts.Items.Add(new ToolStripButton("▶ implement", null, (s, e) => CompileAndRun()));
            ts.Items.Add(new ToolStripButton("■ stop", null, (s, e) => StopEngine()));

            _viewport = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.Black };
            _viewport.Paint += (s, e) => _draw?.Invoke(_scriptInstance, new object[] { e.Graphics });
            _console = new RichTextBox { Dock = DockStyle.Bottom, Height = 120, BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 10) };

            _engineTimer = new Timer { Interval = 16 };
            _engineTimer.Tick += Loop;
            _gameTime = new Stopwatch();

            this.Controls.AddRange(new Control[] { _viewport, _codeEditor, _console, ts });
        }

        private void SetupSyntaxHighlighting()
        {
            _codeEditor.Lexer = Lexer.Cpp;
            _codeEditor.SetKeywords(0, "public class void return if else using namespace float int string bool");
            
            _codeEditor.StyleResetDefault();
            _codeEditor.Styles[Style.Default].Font = "Consolas";
            _codeEditor.Styles[Style.Default].Size = 12;
            _codeEditor.Styles[Style.Default].BackColor = Color.FromArgb(30, 30, 30);
            _codeEditor.Styles[Style.Default].ForeColor = Color.White;
            _codeEditor.StyleClearAll();

           
            _codeEditor.CaretForeColor = Color.White;

            
            _codeEditor.Styles[Style.Cpp.Word].ForeColor = Color.DodgerBlue;
            _codeEditor.Styles[Style.Cpp.String].ForeColor = Color.Orange;
            _codeEditor.Styles[Style.Cpp.CommentLine].ForeColor = Color.Green;
            
            _codeEditor.Refresh();
        }

        private void CompileAndRun()
        {
            var refs = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location));

            var compilation = CSharpCompilation.Create("UserAssembly")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(refs)
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(_codeEditor.Text));

            using (var ms = new MemoryStream())
            {
                var res = compilation.Emit(ms);
                if (res.Success)
                {
                    var asm = Assembly.Load(ms.ToArray());
                    _scriptInstance = asm.CreateInstance("GameBehavior");
                    var type = _scriptInstance?.GetType();
                    _start = type?.GetMethod("Start");
                    _update = type?.GetMethod("Update");
                    _draw = type?.GetMethod("Draw");
                    
                    _start?.Invoke(_scriptInstance, null);
                    _isPlaying = true;
                    _gameTime.Restart();
                    _lastFrame = 0;
                    _engineTimer.Start();
                    _console.Text = "Compilation successful; program running...";
                }
                else
                {
                    _console.Text = "Compilation error: " + res.Diagnostics.FirstOrDefault()?.GetMessage();
                }
            }
        }

        private void StopEngine() { _isPlaying = false; _engineTimer.Stop(); _console.Text = "引擎已停止。"; }

        private void Loop(object s, EventArgs e)
        {
            if (!_isPlaying) return;
            long now = _gameTime.ElapsedMilliseconds;
            float dt = (now - _lastFrame) / 1000f;
            _lastFrame = now;
            _update?.Invoke(_scriptInstance, new object[] { dt });
            _viewport.Invalidate();
        }
    }

    static class Program { [STAThread] static void Main() => Application.Run(new EditorForm()); }
}

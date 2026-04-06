using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace WebToDesk;

internal sealed class SplashForm : Form
{
    private readonly Label _subtitleLabel;
    private readonly Label _statusLabel;
    private readonly SmoothProgressBar _progressBar;
    private readonly System.Windows.Forms.Timer _animationTimer;

    private int _displayedProgress;
    private int _targetProgress;

    public SplashForm()
    {
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(760, 380);
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;

        var shell = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(28)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var logoBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 0, 0, 18),
            Image = TryLoadLogo()
        };

        _subtitleLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(92, 105, 130),
            Text = "Локальная среда запускается. Окно спрячется в трей, когда все будет готово.",
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 0, 14)
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(23, 41, 77),
            Text = "Подготавливаем запуск...",
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(4, 0, 4, 10)
        };

        _progressBar = new SmoothProgressBar
        {
            Dock = DockStyle.Top,
            Height = 16,
            Margin = new Padding(0)
        };

        layout.Controls.Add(logoBox, 0, 0);
        layout.Controls.Add(_subtitleLabel, 0, 1);
        layout.Controls.Add(_statusLabel, 0, 2);
        layout.Controls.Add(_progressBar, 0, 3);

        shell.Controls.Add(layout);
        Controls.Add(shell);

        _animationTimer = new System.Windows.Forms.Timer
        {
            Interval = 16,
            Enabled = true
        };
        _animationTimer.Tick += AnimationTimer_Tick;

        Shown += (_, _) =>
        {
            ApplyRoundedRegion();
            Activate();
        };
        Resize += (_, _) => ApplyRoundedRegion();
    }

    public void Report(string message, int progressPercent)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => Report(message, progressPercent));
            return;
        }

        _statusLabel.Text = message;
        _targetProgress = Math.Max(_displayedProgress, Math.Clamp(progressPercent, 0, 100));
    }

    public void ShowError(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => ShowError(message));
            return;
        }

        _subtitleLabel.Text = "Запуск остановлен из-за ошибки.";
        _subtitleLabel.ForeColor = Color.FromArgb(179, 55, 47);
        _statusLabel.Text = message;
        _progressBar.FillColor = Color.FromArgb(214, 78, 62);
        _targetProgress = 100;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _animationTimer.Stop();
        _animationTimer.Dispose();
        base.OnFormClosed(e);
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        if (_displayedProgress >= _targetProgress)
        {
            return;
        }

        var remaining = _targetProgress - _displayedProgress;
        _displayedProgress += Math.Max(1, Math.Min(4, remaining));
        _progressBar.Value = _displayedProgress;
    }

    private void ApplyRoundedRegion()
    {
        using var path = CreateRoundedPath(new Rectangle(0, 0, Width, Height), 26);
        Region = new Region(path);
    }

    private static Image? TryLoadLogo()
    {
        var assetPath = AppLocator.TryFindFile(Path.Combine("Assets", "Web2deck.png"))
            ?? AppLocator.TryFindFile("Web2deck.png");

        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        using var source = Image.FromFile(assetPath);
        return new Bitmap(source);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }
}

internal sealed class SmoothProgressBar : Control
{
    private int _value;
    private Color _fillColor;

    public SmoothProgressBar()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint,
            true);

        BackColor = Color.FromArgb(233, 238, 248);
        _fillColor = Color.FromArgb(21, 93, 233);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set
        {
            var nextValue = Math.Clamp(value, 0, 100);
            if (_value == nextValue)
            {
                return;
            }

            _value = nextValue;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor
    {
        get => _fillColor;
        set
        {
            if (_fillColor == value)
            {
                return;
            }

            _fillColor = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var trackRect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var trackPath = CreateRoundedPath(trackRect, Height / 2);
        using var trackBrush = new SolidBrush(BackColor);
        e.Graphics.FillPath(trackBrush, trackPath);

        var fillWidth = (int)Math.Round(trackRect.Width * (Value / 100d));
        if (fillWidth <= 0)
        {
            return;
        }

        var fillRect = new Rectangle(trackRect.X, trackRect.Y, Math.Max(fillWidth, Height), trackRect.Height);
        if (fillRect.Width > trackRect.Width)
        {
            fillRect.Width = trackRect.Width;
        }

        using var fillPath = CreateRoundedPath(fillRect, Height / 2);
        using var fillBrush = new SolidBrush(FillColor);
        e.Graphics.FillPath(fillBrush, fillPath);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var safeRadius = Math.Max(1, radius);
        var diameter = safeRadius * 2;
        var path = new GraphicsPath();

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }
}

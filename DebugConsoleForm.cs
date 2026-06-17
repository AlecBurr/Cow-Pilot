namespace CowPilot;

sealed class DebugConsoleForm : Form
{
    public event EventHandler? VerboseCalculationRequested;

    private readonly TextBox _output = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        Font = new Font("Consolas", 10),
        WordWrap = false
    };

    public DebugConsoleForm()
    {
        Text = "Cow Pilot Console";
        Size = new Size(760, 420);
        var print = new Button { Text = "Recalculate and print math steps", Dock = DockStyle.Top, Height = 32 };
        print.Click += (_, _) => VerboseCalculationRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(_output);
        Controls.Add(print);
    }

    public void Log(string message)
    {
        if (IsDisposed) return;
        _output.AppendText($"[{DateTime.Now:hh:mm:ss tt}] {message}{Environment.NewLine}");
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }
}

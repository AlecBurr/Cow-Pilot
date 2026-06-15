namespace CowPilot;

sealed class DebugConsoleForm : Form
{
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
        Controls.Add(_output);
    }

    public void Log(string message)
    {
        if (IsDisposed) return;
        _output.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
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

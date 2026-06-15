namespace CowPilot;

enum UnsavedChoice
{
    Cancel,
    SaveAs,
    QuickSave,
    Close
}

sealed class UnsavedChangesDialog : Form
{
    public UnsavedChoice Choice { get; private set; } = UnsavedChoice.Cancel;

    public UnsavedChangesDialog()
    {
        Text = "Unsaved Changes";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(430, 125);

        var message = new Label
        {
            Text = "This quote has unsaved changes. What would you like to do?",
            Dock = DockStyle.Top,
            Height = 48,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        buttons.Controls.Add(Button("Cancel", UnsavedChoice.Cancel));
        buttons.Controls.Add(Button("Close", UnsavedChoice.Close));
        buttons.Controls.Add(Button("Quick Save", UnsavedChoice.QuickSave));
        buttons.Controls.Add(Button("Save As", UnsavedChoice.SaveAs));

        Controls.Add(message);
        Controls.Add(buttons);
    }

    private Button Button(string text, UnsavedChoice choice)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) =>
        {
            Choice = choice;
            Close();
        };
        return button;
    }
}

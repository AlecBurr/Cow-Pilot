namespace CowPilot;

enum UnsavedChoice
{
    Cancel,
    Yes,
    No
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
            Text = "Would you like to save your work before closing?",
            Dock = DockStyle.Top,
            Height = 48,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8)
        };
        buttons.Controls.Add(Button("Yes", UnsavedChoice.Yes));
        buttons.Controls.Add(Button("No", UnsavedChoice.No));
        buttons.Controls.Add(Button("Cancel", UnsavedChoice.Cancel));

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

namespace CowPilot;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            SelfTests.Run();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }    
}

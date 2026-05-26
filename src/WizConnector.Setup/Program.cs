namespace WizConnector.Setup;

static class Program
{
    [STAThread]
    static void Main()
    {
        SageSdkBootstrap.Initialize();
        ApplicationConfiguration.Initialize();
        Application.Run(new SetupForm());
    }
}

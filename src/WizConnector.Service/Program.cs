using WizAccountant.Contracts;
using WizConnector.Service;
using WizConnector.Service.Sage;

SageSdkBootstrap.Initialize();

var builder = Host.CreateApplicationBuilder(args);

var sageFromDisk = SageConfigStorage.LoadEncrypted();
if (sageFromDisk is not null)
{
    foreach (var (key, value) in sageFromDisk.ToConfigurationDictionary())
        builder.Configuration[key] = value;
}
builder.Services.Configure<ConnectorSettings>(builder.Configuration.GetSection("Connector"));
builder.Services.PostConfigure<ConnectorSettings>(s =>
{
    if (string.IsNullOrWhiteSpace(s.DeviceId))
        s.DeviceId = Environment.MachineName;
});
builder.Services.Configure<SageSettings>(builder.Configuration.GetSection("Sage"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IStateStore, FileStateStore>();

var sageEnabled = builder.Configuration.GetValue("Sage:Enabled", true);
if (sageEnabled)
{
    builder.Services.AddSingleton<SageSession>();
    builder.Services.AddSingleton<IJobExecutor, SageSdkJobExecutor>();
    builder.Services.AddSingleton<WriteConsentStore>();
}
else
{
    builder.Services.AddSingleton<IJobExecutor, MockJobExecutor>();
}

builder.Services.AddSingleton<ConnectorUpdateService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

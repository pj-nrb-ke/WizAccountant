using WizAccountant.Contracts;

namespace WizConnector.Service.Sage;

/// <summary>P3: optional tray/consent window for cloud writes.</summary>
public sealed class WriteConsentStore
{
    public bool IsAllowed() => WriteConsentHelper.IsAllowed();

    public void Grant(TimeSpan duration) => WriteConsentHelper.Grant(duration);
}

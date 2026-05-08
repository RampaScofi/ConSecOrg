namespace ConSecOrg.Client.Services;

public enum AppMode { Personal, Corporate }

public sealed class ModeService
{
    public AppMode Mode { get; private set; } = AppMode.Personal;
    public string ServerUrl { get; private set; } = "http://localhost:5205";

    public void SetPersonal() => Mode = AppMode.Personal;

    public void SetCorporate(string serverUrl)
    {
        Mode = AppMode.Corporate;
        ServerUrl = serverUrl;
    }

    public bool IsCorporate => Mode == AppMode.Corporate;
    public bool IsPersonal => Mode == AppMode.Personal;
}

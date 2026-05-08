namespace ConSecOrg.Client.ViewModels.Shell;

public sealed class SharedMemberInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Letter => Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";
}

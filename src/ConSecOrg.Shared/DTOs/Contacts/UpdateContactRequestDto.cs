namespace ConSecOrg.Shared.DTOs.Contacts;

public class UpdateContactRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
}

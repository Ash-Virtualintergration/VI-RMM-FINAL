namespace ViRMM.Models;

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Technician";
    public string Password { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string? ProfileImage { get; set; }
}

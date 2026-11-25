using ViRMM.Models;

namespace ViRMM.Services;

public sealed class UserStore
{
    private readonly List<AppUser> users = [];

    public bool IsConfigured { get; }

    public UserStore(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<UserStore> logger)
    {
        var password = environment.IsDevelopment()
            ? "admin"
            : configuration["ViRMM:AdminPassword"];

        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogError(
                "Administrator sign-in is unavailable. Set ViRMM:AdminPassword before running outside Development.");
            IsConfigured = false;
            return;
        }

        IsConfigured = true;
        users =
        [
            new AppUser
            {
                FullName = "Administrator",
                Username = "admin",
                Email = "",
                Role = "Administrator",
                Password = password,
                IsActive = true
            }
        ];
    }

    public IReadOnlyList<AppUser> All => users;

    public AppUser? Find(Guid id) => users.FirstOrDefault(x => x.Id == id);

    public AppUser? Authenticate(string username, string password) =>
        users.FirstOrDefault(x =>
            x.IsActive &&
            string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase) &&
            x.Password == password);

    public void Add(AppUser user) => users.Add(user);

    public void Save(AppUser user)
    {
        var existing = Find(user.Id);
        if (existing is null) return;

        existing.FullName = user.FullName;
        existing.Username = user.Username;
        existing.Email = user.Email;
        existing.Role = user.Role;
        existing.Password = user.Password;
        existing.IsActive = user.IsActive;
        existing.ProfileImage = user.ProfileImage;
    }

    public void Delete(Guid id)
    {
        var user = Find(id);
        if (user is not null && user.Username != "admin")
            users.Remove(user);
    }
}

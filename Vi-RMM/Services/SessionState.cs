using ViRMM.Models;

namespace ViRMM.Services;

public sealed class SessionState
{
    public AppUser? CurrentUser { get; private set; }

    public void SignIn(AppUser user) => CurrentUser = user;

    public void SignOut() => CurrentUser = null;
}

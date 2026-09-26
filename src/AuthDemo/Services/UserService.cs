namespace AuthDemo.Services;

/// <summary>
/// Stub user store. Replace with ASP.NET Core Identity / your own persistence layer.
/// </summary>
public interface IUserService
{
    (bool Valid, IEnumerable<string> Roles) Validate(string username, string password);
}

public class UserService : IUserService
{
    // username -> (password, roles)  — plaintext only for demo; use hashed passwords in prod!
    private static readonly Dictionary<string, (string Password, string[] Roles)> Users = new(StringComparer.OrdinalIgnoreCase)
    {
        ["admin"] = ("admin123", ["Admin", "User"]),
        ["alice"] = ("alice123", ["User"]),
        ["bob"] = ("bob123", ["User", "Manager"]),
    };

    public (bool Valid, IEnumerable<string> Roles) Validate(string username, string password)
    {
        if (Users.TryGetValue(username, out var entry) && entry.Password == password)
            return (true, entry.Roles);

        return (false, []);
    }
}
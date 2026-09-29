namespace StudentFeedbackApp;

public static class AuthService
{
    private static readonly Dictionary<string, string> Credentials = new(StringComparer.OrdinalIgnoreCase)
    {
        ["admin"] = "admin123",
        ["student"] = "student123"
    };

    public static bool ValidateCredentials(string username, string password, out string role)
    {
        role = string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var normalizedUsername = username.Trim();

        if (!Credentials.TryGetValue(normalizedUsername, out var storedPassword))
        {
            return false;
        }

        if (!string.Equals(password.Trim(), storedPassword, StringComparison.Ordinal))
        {
            return false;
        }

        role = normalizedUsername;
        return true;
    }

    public static bool RegisterUser(string username, string password, string confirmPassword, out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            message = "Username and password are required.";
            return false;
        }

        var normalizedUsername = username.Trim();

        if (normalizedUsername.Length < 3)
        {
            message = "Username must be at least 3 characters long.";
            return false;
        }

        if (password.Trim().Length < 6)
        {
            message = "Password must be at least 6 characters long.";
            return false;
        }

        if (!string.Equals(password.Trim(), confirmPassword.Trim(), StringComparison.Ordinal))
        {
            message = "Passwords do not match.";
            return false;
        }

        if (Credentials.ContainsKey(normalizedUsername))
        {
            message = "Username already exists.";
            return false;
        }

        Credentials[normalizedUsername] = password.Trim();
        message = "User registered successfully.";
        return true;
    }
}

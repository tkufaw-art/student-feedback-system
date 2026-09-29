using StudentFeedbackApp;

namespace StudentFeedbackApp.Tests;

public class AuthServiceTests
{
    [Fact]
    public void ValidateCredentials_AdminCredentials_AreAccepted()
    {
        var result = AuthService.ValidateCredentials("admin", "admin123", out var role);

        Assert.True(result);
        Assert.Equal("admin", role);
    }

    [Fact]
    public void ValidateCredentials_StudentCredentials_AreAccepted()
    {
        var result = AuthService.ValidateCredentials("student", "student123", out var role);

        Assert.True(result);
        Assert.Equal("student", role);
    }

    [Fact]
    public void ValidateCredentials_InvalidCredentials_AreRejected()
    {
        var result = AuthService.ValidateCredentials("guest", "wrongpass", out var role);

        Assert.False(result);
        Assert.Equal(string.Empty, role);
    }

    [Fact]
    public void RegisterUser_ValidStudentRegistration_Succeeds()
    {
        var result = AuthService.RegisterUser("novastudent", "pass123", "pass123", out var message);

        Assert.True(result);
        Assert.Contains("registered", message, StringComparison.OrdinalIgnoreCase);
        Assert.True(AuthService.ValidateCredentials("novastudent", "pass123", out var role));
        Assert.Equal("novastudent", role);
    }

    [Fact]
    public void RegisterUser_DuplicateUsername_Fails()
    {
        var result = AuthService.RegisterUser("student", "pass123", "pass123", out var message);

        Assert.False(result);
        Assert.Contains("already exists", message, StringComparison.OrdinalIgnoreCase);
    }
}

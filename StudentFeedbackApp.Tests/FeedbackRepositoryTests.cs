using StudentFeedbackApp;

namespace StudentFeedbackApp.Tests;

public class FeedbackRepositoryTests
{
    [Fact]
    public void InitializeDatabase_CreatesFeedbackTable()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"feedback-{Guid.NewGuid():N}.db");

        var repository = new FeedbackRepository(databasePath);

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'CourseFeedback';";

        var tableName = command.ExecuteScalar();

        Assert.NotNull(tableName);
        Assert.Equal("CourseFeedback", tableName);
    }

    [Fact]
    public void AddFeedback_AndGetAverageRatingsByCourse_ReturnsExpectedAverage()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"feedback-{Guid.NewGuid():N}.db");
        var repository = new FeedbackRepository(databasePath);

        repository.AddFeedback(new CourseFeedback
        {
            StudentName = "Alice",
            Department = "Computer Science",
            CourseName = "ADO.NET",
            Subject = "Database Access",
            FacultyTeacher = "Dr. Smith",
            Rating = 5,
            Comment = "Very useful",
            SubmittedOn = DateTime.Now
        });

        repository.AddFeedback(new CourseFeedback
        {
            StudentName = "Bob",
            CourseName = "ADO.NET",
            Rating = 3,
            Comment = "Needed more examples",
            SubmittedOn = DateTime.Now
        });

        var averages = repository.GetAverageRatingsByCourse();

        Assert.True(averages.ContainsKey("ADO.NET"));
        Assert.Equal(4, averages["ADO.NET"], 0);

        var feedback = repository.GetAllFeedback().First(item => item.StudentName == "Alice");
        Assert.Equal("Computer Science", feedback.Department);
        Assert.Equal("Database Access", feedback.Subject);
        Assert.Equal("Dr. Smith", feedback.FacultyTeacher);
    }

    [Fact]
    public void MarkFeedbackAsRead_UpdatesFeedbackStatus()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"feedback-{Guid.NewGuid():N}.db");
        var repository = new FeedbackRepository(databasePath);

        repository.AddFeedback(new CourseFeedback
        {
            StudentName = "Alice",
            CourseName = "ADO.NET",
            Rating = 5,
            Comment = "Very useful",
            SubmittedOn = DateTime.Now
        });

        var feedback = Assert.Single(repository.GetAllFeedback());
        Assert.False(feedback.IsRead);

        repository.MarkFeedbackAsRead(feedback.Id);

        var updatedFeedback = Assert.Single(repository.GetAllFeedback());
        Assert.True(updatedFeedback.IsRead);

        repository.SetFeedbackReadStatus(feedback.Id, false);

        updatedFeedback = Assert.Single(repository.GetAllFeedback());
        Assert.False(updatedFeedback.IsRead);
    }

    [Fact]
    public void UpdateFeedback_ChangesValuesAndResetsReadStatus()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"feedback-{Guid.NewGuid():N}.db");
        var repository = new FeedbackRepository(databasePath);

        repository.AddFeedback(new CourseFeedback
        {
            StudentName = "Alice",
            CourseName = "ADO.NET",
            Rating = 5,
            Comment = "Original",
            SubmittedOn = DateTime.Now
        });

        var feedback = Assert.Single(repository.GetAllFeedback());
        repository.MarkFeedbackAsRead(feedback.Id);
        repository.UpdateFeedback(new CourseFeedback
        {
            Id = feedback.Id,
            StudentName = "Alice",
            CourseName = "ASP.NET",
            Rating = 4,
            Comment = "Updated",
            SubmittedOn = feedback.SubmittedOn
        });

        var updatedFeedback = Assert.Single(repository.GetAllFeedback());
        Assert.Equal("ASP.NET", updatedFeedback.CourseName);
        Assert.Equal(4, updatedFeedback.Rating);
        Assert.Equal("Updated", updatedFeedback.Comment);
        Assert.False(updatedFeedback.IsRead);
    }

    [Fact]
    public void DeleteFeedback_RemovesFeedback()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"feedback-{Guid.NewGuid():N}.db");
        var repository = new FeedbackRepository(databasePath);

        repository.AddFeedback(new CourseFeedback
        {
            StudentName = "Alice",
            CourseName = "ADO.NET",
            Rating = 5,
            SubmittedOn = DateTime.Now
        });

        var feedback = Assert.Single(repository.GetAllFeedback());
        repository.DeleteFeedback(feedback.Id);

        Assert.Empty(repository.GetAllFeedback());
    }
}

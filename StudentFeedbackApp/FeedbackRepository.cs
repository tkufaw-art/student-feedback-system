using Microsoft.Data.Sqlite;

namespace StudentFeedbackApp;

public class FeedbackRepository
{
    private readonly string _connectionString;

    public FeedbackRepository(string databaseFilePath)
    {
        _connectionString = $"Data Source={databaseFilePath}";
        InitializeDatabase();
    }

    public void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS CourseFeedback (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StudentName TEXT NOT NULL,
                Department TEXT NOT NULL DEFAULT '',
                CourseName TEXT NOT NULL,
                Subject TEXT NOT NULL DEFAULT '',
                FacultyTeacher TEXT NOT NULL DEFAULT '',
                Rating INTEGER NOT NULL CHECK (Rating BETWEEN 1 AND 5),
                Comment TEXT,
                SubmittedOn TEXT NOT NULL,
                IsRead INTEGER NOT NULL DEFAULT 0
            );";

        command.ExecuteNonQuery();

        using var columnsCommand = connection.CreateCommand();
        columnsCommand.CommandText = "PRAGMA table_info(CourseFeedback);";
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var reader = columnsCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        var missingColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Department"] = "TEXT NOT NULL DEFAULT ''",
            ["Subject"] = "TEXT NOT NULL DEFAULT ''",
            ["FacultyTeacher"] = "TEXT NOT NULL DEFAULT ''",
            ["IsRead"] = "INTEGER NOT NULL DEFAULT 0"
        };

        foreach (var column in missingColumns)
        {
            if (existingColumns.Contains(column.Key))
            {
                continue;
            }

            using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE CourseFeedback ADD COLUMN {column.Key} {column.Value};";
            alterCommand.ExecuteNonQuery();
        }
    }

    public void AddFeedback(CourseFeedback feedback)
    {
        if (feedback is null)
        {
            throw new ArgumentNullException(nameof(feedback));
        }

        if (string.IsNullOrWhiteSpace(feedback.StudentName))
        {
            throw new ArgumentException("Student name is required.", nameof(feedback));
        }

        if (string.IsNullOrWhiteSpace(feedback.CourseName))
        {
            throw new ArgumentException("Course name is required.", nameof(feedback));
        }

        if (feedback.Rating < 1 || feedback.Rating > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(feedback), "Rating must be between 1 and 5.");
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO CourseFeedback (StudentName, Department, CourseName, Subject, FacultyTeacher, Rating, Comment, SubmittedOn, IsRead)
            VALUES (@studentName, @department, @courseName, @subject, @facultyTeacher, @rating, @comment, @submittedOn, 0);";

        command.Parameters.AddWithValue("@studentName", feedback.StudentName.Trim());
        command.Parameters.AddWithValue("@department", feedback.Department.Trim());
        command.Parameters.AddWithValue("@courseName", feedback.CourseName.Trim());
        command.Parameters.AddWithValue("@subject", feedback.Subject.Trim());
        command.Parameters.AddWithValue("@facultyTeacher", feedback.FacultyTeacher.Trim());
        command.Parameters.AddWithValue("@rating", feedback.Rating);
        command.Parameters.AddWithValue("@comment", string.IsNullOrWhiteSpace(feedback.Comment) ? string.Empty : feedback.Comment.Trim());
        command.Parameters.AddWithValue("@submittedOn", feedback.SubmittedOn == default ? DateTime.Now : feedback.SubmittedOn);

        command.ExecuteNonQuery();
    }

    public List<CourseFeedback> GetAllFeedback()
    {
        var results = new List<CourseFeedback>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, StudentName, Department, CourseName, Subject, FacultyTeacher, Rating, Comment, SubmittedOn, IsRead
            FROM CourseFeedback
            ORDER BY SubmittedOn DESC;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new CourseFeedback
            {
                Id = reader.GetInt32(0),
                StudentName = reader.GetString(1),
                Department = reader.GetString(2),
                CourseName = reader.GetString(3),
                Subject = reader.GetString(4),
                FacultyTeacher = reader.GetString(5),
                Rating = reader.GetInt32(6),
                Comment = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                SubmittedOn = reader.GetDateTime(8),
                IsRead = reader.GetBoolean(9)
            });
        }

        return results;
    }

    public void MarkFeedbackAsRead(int feedbackId)
    {
        SetFeedbackReadStatus(feedbackId, true);
    }

    public void SetFeedbackReadStatus(int feedbackId, bool isRead)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE CourseFeedback SET IsRead = @isRead WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", feedbackId);
        command.Parameters.AddWithValue("@isRead", isRead ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public CourseFeedback? GetFeedbackById(int feedbackId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, StudentName, Department, CourseName, Subject, FacultyTeacher, Rating, Comment, SubmittedOn, IsRead
            FROM CourseFeedback
            WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", feedbackId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new CourseFeedback
        {
            Id = reader.GetInt32(0),
            StudentName = reader.GetString(1),
            Department = reader.GetString(2),
            CourseName = reader.GetString(3),
            Subject = reader.GetString(4),
            FacultyTeacher = reader.GetString(5),
            Rating = reader.GetInt32(6),
            Comment = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
            SubmittedOn = reader.GetDateTime(8),
            IsRead = reader.GetBoolean(9)
        };
    }

    public void UpdateFeedback(CourseFeedback feedback)
    {
        if (feedback is null)
        {
            throw new ArgumentNullException(nameof(feedback));
        }

        if (string.IsNullOrWhiteSpace(feedback.StudentName))
        {
            throw new ArgumentException("Student name is required.", nameof(feedback));
        }

        if (string.IsNullOrWhiteSpace(feedback.CourseName))
        {
            throw new ArgumentException("Course name is required.", nameof(feedback));
        }

        if (feedback.Rating < 1 || feedback.Rating > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(feedback), "Rating must be between 1 and 5.");
        }

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE CourseFeedback
            SET StudentName = @studentName,
                Department = @department,
                CourseName = @courseName,
                Subject = @subject,
                FacultyTeacher = @facultyTeacher,
                Rating = @rating,
                Comment = @comment,
                IsRead = 0
            WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", feedback.Id);
        command.Parameters.AddWithValue("@studentName", feedback.StudentName.Trim());
        command.Parameters.AddWithValue("@department", feedback.Department.Trim());
        command.Parameters.AddWithValue("@courseName", feedback.CourseName.Trim());
        command.Parameters.AddWithValue("@subject", feedback.Subject.Trim());
        command.Parameters.AddWithValue("@facultyTeacher", feedback.FacultyTeacher.Trim());
        command.Parameters.AddWithValue("@rating", feedback.Rating);
        command.Parameters.AddWithValue("@comment", string.IsNullOrWhiteSpace(feedback.Comment) ? string.Empty : feedback.Comment.Trim());
        command.ExecuteNonQuery();
    }

    public void DeleteFeedback(int feedbackId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM CourseFeedback WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", feedbackId);
        command.ExecuteNonQuery();
    }

    public Dictionary<string, double> GetAverageRatingsByCourse()
    {
        var results = new Dictionary<string, double>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT CourseName, AVG(Rating)
            FROM CourseFeedback
            GROUP BY CourseName
            ORDER BY CourseName;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results[reader.GetString(0)] = reader.GetDouble(1);
        }

        return results;
    }
}

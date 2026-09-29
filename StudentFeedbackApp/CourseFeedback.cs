namespace StudentFeedbackApp;

public class CourseFeedback
{
    public int Id { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string FacultyTeacher { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime SubmittedOn { get; set; }
    public bool IsRead { get; set; }
}

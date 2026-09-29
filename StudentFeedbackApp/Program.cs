using StudentFeedbackApp;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
var databasePath = Path.Combine(AppContext.BaseDirectory, "student_feedback.db");
var repository = new FeedbackRepository(databasePath);

var app = builder.Build();
app.UseSession();

app.MapGet("/", () => Results.Content(LoginPageHtml(), "text/html"));

app.MapGet("/register", () => Results.Content(RegisterPageHtml(), "text/html"));

app.MapPost("/register", async context =>
{
    var form = await context.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();
    var confirmPassword = form["confirmPassword"].ToString();

    if (!AuthService.RegisterUser(username, password, confirmPassword, out var message))
    {
        await context.Response.WriteAsync(RegisterPageHtml(message));
        return;
    }

    await context.Response.WriteAsync(LoginPageHtml(message));
});

app.MapGet("/logout", (HttpContext context) =>
{
    context.Session.Clear();
    context.Response.Cookies.Delete("studentUser");
    context.Response.Redirect("/");
});

app.MapPost("/login", async context =>
{
    var form = await context.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();

    if (!AuthService.ValidateCredentials(username, password, out var role))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync(LoginPageHtml("Invalid username or password."));
        return;
    }

    if (role == "admin")
    {
        context.Session.SetString("role", "admin");
        context.Session.SetString("username", username.Trim());
        context.Response.Redirect("/admin");
        return;
    }

    context.Session.SetString("role", "student");
    context.Session.SetString("username", username.Trim());
    context.Response.Redirect("/student");
});

app.MapGet("/admin", (HttpContext context) =>
{
    if (context.Session.GetString("role") != "admin")
    {
        return (IResult)Results.Redirect("/");
    }

    return Results.Content(AdminPageHtml(), "text/html");
});
app.MapGet("/student", (HttpRequest request) =>
{
    var username = request.HttpContext.Session.GetString("username") ?? string.Empty;
    if (request.HttpContext.Session.GetString("role") != "student" || string.IsNullOrWhiteSpace(username))
    {
        return (IResult)Results.Redirect("/");
    }

    return Results.Content(StudentPageHtml(username), "text/html");
});

app.MapGet("/feedback/{id:int}/edit", (int id, HttpRequest request) =>
{
    var username = request.HttpContext.Session.GetString("username") ?? string.Empty;
    var feedback = repository.GetFeedbackById(id);
    if (request.HttpContext.Session.GetString("role") != "student" || string.IsNullOrWhiteSpace(username))
    {
        return (IResult)Results.Redirect("/");
    }

    if (feedback is null || !string.Equals(feedback.StudentName, username, StringComparison.OrdinalIgnoreCase))
    {
        return Results.Redirect("/student");
    }

    return Results.Content(FeedbackEditPageHtml(feedback, $"/feedback/{id}/edit", "/student"), "text/html");
});

app.MapPost("/feedback/{id:int}/edit", async (int id, HttpContext context) =>
{
    var username = context.Session.GetString("username") ?? string.Empty;
    var feedback = repository.GetFeedbackById(id);
    if (context.Session.GetString("role") != "student" || string.IsNullOrWhiteSpace(username))
    {
        context.Response.Redirect("/");
        return;
    }

    if (feedback is null || !string.Equals(feedback.StudentName, username, StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/student");
        return;
    }

    var form = await context.Request.ReadFormAsync();
    var rating = int.TryParse(form["rating"].ToString(), out var parsedRating) ? parsedRating : 0;

    try
    {
        repository.UpdateFeedback(new CourseFeedback
        {
            Id = id,
            StudentName = username,
            Department = form["department"].ToString(),
            CourseName = form["courseName"].ToString(),
            Subject = form["subject"].ToString(),
            FacultyTeacher = form["facultyTeacher"].ToString(),
            Rating = rating,
            Comment = form["comment"].ToString(),
            SubmittedOn = feedback.SubmittedOn
        });
    }
    catch
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Please enter valid feedback details.");
        return;
    }

    context.Response.Redirect("/student");
});

app.MapPost("/feedback/{id:int}/delete", (int id, HttpContext context) =>
{
    var username = context.Session.GetString("username") ?? string.Empty;
    var feedback = repository.GetFeedbackById(id);
    if (context.Session.GetString("role") != "student" || string.IsNullOrWhiteSpace(username))
    {
        context.Response.Redirect("/");
        return;
    }

    if (feedback is not null && string.Equals(feedback.StudentName, username, StringComparison.OrdinalIgnoreCase))
    {
        repository.DeleteFeedback(id);
    }

    context.Response.Redirect("/student");
});

app.MapPost("/admin/feedback/{id:int}/status", async (int id, HttpContext context) =>
{
    if (context.Session.GetString("role") != "admin")
    {
        context.Response.Redirect("/");
        return;
    }

    var form = await context.Request.ReadFormAsync();
    var isRead = string.Equals(form["isRead"].ToString(), "true", StringComparison.OrdinalIgnoreCase);
    repository.SetFeedbackReadStatus(id, isRead);
    context.Response.Redirect("/admin");
});

app.MapPost("/feedback", async context =>
{
    var form = await context.Request.ReadFormAsync();
    var studentName = context.Session.GetString("username") ?? string.Empty;
    if (context.Session.GetString("role") != "student" || string.IsNullOrWhiteSpace(studentName))
    {
        context.Response.Redirect("/");
        return;
    }
    var department = form["department"].ToString();
    var courseName = form["courseName"].ToString();
    var subject = form["subject"].ToString();
    var facultyTeacher = form["facultyTeacher"].ToString();
    var ratingText = form["rating"].ToString();
    var comment = form["comment"].ToString();

    var parsedRating = int.TryParse(ratingText, out var value) ? value : 0;

    try
    {
        repository.AddFeedback(new CourseFeedback
        {
            StudentName = studentName,
            Department = department,
            CourseName = courseName,
            Subject = subject,
            FacultyTeacher = facultyTeacher,
            Rating = parsedRating,
            Comment = comment,
            SubmittedOn = DateTime.Now
        });
    }
    catch
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Please enter valid feedback details.");
        return;
    }

    context.Response.Redirect("/student");
});

app.Run();

string RegisterPageHtml(string message = "")
{
    var alert = string.IsNullOrWhiteSpace(message)
        ? string.Empty
        : $"<p style='color: red; margin-bottom: 15px;'>{message}</p>";

    return $@"
        <!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='utf-8' />
            <title>Register</title>
            <style>
                body {{ font-family: Arial, sans-serif; background: linear-gradient(135deg, #111111, #450a0a); margin: 0; padding: 40px; }}
                .theme-toggle {{ position: fixed; top: 8px; right: 8px; z-index: 10; background: #111111; color: white; border: 1px solid #b91c1c; padding: 4px 6px; border-radius: 5px; cursor: pointer; font-size: .65rem; font-weight: bold; }}
                .dark-mode {{ background: #050505 !important; color: #f3f4f6; }}
                .dark-mode .container {{ background: #18181b; color: #f3f4f6; }}
                .dark-mode h1 {{ color: white; }}
                .dark-mode input {{ background: #27272a; color: white; border-color: #52525b; }}
                .dark-mode .theme-toggle {{ background: white; color: #111111; }}
                .container {{ max-width: 420px; margin: 80px auto; background: white; padding: 30px; border-radius: 12px; box-shadow: 0 8px 20px rgba(0,0,0,0.1); }}
                h1 {{ text-align: center; color: #111111; }}
                .university {{ text-align: center; color: #b91c1c; font-size: 1.25rem; font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }}
                .field {{ margin-bottom: 16px; }}
                label {{ display: block; margin-bottom: 6px; font-weight: bold; }}
                input {{ width: 100%; padding: 12px; box-sizing: border-box; border: 1px solid #cbd5e1; border-radius: 8px; }}
                button {{ width: 100%; background: #b91c1c; color: white; border: none; padding: 12px; border-radius: 8px; cursor: pointer; font-size: 16px; }}
                .link {{ margin-top: 18px; text-align: center; }}
                .link a {{ color: #b91c1c; text-decoration: none; }}
            </style>
        </head>
        <body>
            <button class='theme-toggle' type='button' onclick='toggleTheme()'>Dark mode</button>
            <div class='container'>
                <div class='university'>Parul University</div>
                <h1>Student Registration</h1>
                {alert}
                <form method='post' action='/register'>
                    <div class='field'>
                        <label for='username'>Username</label>
                        <input id='username' name='username' required placeholder='Choose a username' />
                    </div>
                    <div class='field'>
                        <label for='password'>Password</label>
                        <input id='password' name='password' type='password' required placeholder='Enter password' />
                    </div>
                    <div class='field'>
                        <label for='confirmPassword'>Confirm Password</label>
                        <input id='confirmPassword' name='confirmPassword' type='password' required placeholder='Repeat password' />
                    </div>
                    <button type='submit'>Register</button>
                </form>
                <div class='link'><a href='/'>Back to Login</a></div>
            </div>
            <script>
                function applyTheme() {{
                    const dark = localStorage.getItem('feedback-theme') === 'dark';
                    document.body.classList.toggle('dark-mode', dark);
                    document.querySelector('.theme-toggle').textContent = dark ? 'Light mode' : 'Dark mode';
                }}
                function toggleTheme() {{
                    localStorage.setItem('feedback-theme', document.body.classList.contains('dark-mode') ? 'light' : 'dark');
                    applyTheme();
                }}
                applyTheme();
            </script>
        </body>
        </html>
    ";
}

string LoginPageHtml(string message = "")
{
    var alert = string.IsNullOrWhiteSpace(message)
        ? string.Empty
        : $"<p style='color: red; margin-bottom: 15px;'>{message}</p>";

    return $@"
        <!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='utf-8' />
            <title>Course Feedback Login</title>
            <style>
                body {{ font-family: Arial, sans-serif; background: linear-gradient(135deg, #111111, #450a0a); margin: 0; padding: 40px; }}
                .theme-toggle {{ position: fixed; top: 8px; right: 8px; z-index: 10; background: #111111; color: white; border: 1px solid #b91c1c; padding: 4px 6px; border-radius: 5px; cursor: pointer; font-size: .65rem; font-weight: bold; }}
                .dark-mode {{ background: #050505 !important; color: #f3f4f6; }}
                .dark-mode .container {{ background: #18181b; color: #f3f4f6; }}
                .dark-mode h1 {{ color: white; }}
                .dark-mode input {{ background: #27272a; color: white; border-color: #52525b; }}
                .dark-mode .credentials {{ background: #27272a; }}
                .dark-mode .theme-toggle {{ background: white; color: #111111; }}
                .container {{ max-width: 420px; margin: 80px auto; background: white; padding: 30px; border-radius: 12px; box-shadow: 0 8px 20px rgba(0,0,0,0.1); }}
                h1 {{ text-align: center; color: #111111; }}
                .university {{ text-align: center; color: #b91c1c; font-size: 1.25rem; font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }}
                .field {{ margin-bottom: 16px; }}
                label {{ display: block; margin-bottom: 6px; font-weight: bold; }}
                input {{ width: 100%; padding: 12px; box-sizing: border-box; border: 1px solid #cbd5e1; border-radius: 8px; }}
                button {{ width: 100%; background: #b91c1c; color: white; border: none; padding: 12px; border-radius: 8px; cursor: pointer; font-size: 16px; }}
                .credentials {{ background: #fef2f2; border-left: 4px solid #b91c1c; padding: 10px 12px; border-radius: 8px; margin-top: 18px; font-size: 14px; }}
                .link {{ margin-top: 18px; text-align: center; }}
                .link a {{ color: #b91c1c; text-decoration: none; }}
            </style>
        </head>
        <body>
            <button class='theme-toggle' type='button' onclick='toggleTheme()'>Dark mode</button>
            <div class='container'>
                <div class='university'>Parul University</div>
                <h1>Course Feedback Portal</h1>
                {alert}
                <form method='post' action='/login'>
                    <div class='field'>
                        <label for='username'>Username</label>
                        <input id='username' name='username' required placeholder='admin or student' />
                    </div>
                    <div class='field'>
                        <label for='password'>Password</label>
                        <input id='password' name='password' type='password' required placeholder='Enter password' />
                    </div>
                    <button type='submit'>Login</button>
                </form>
                <div class='link'><a href='/register'>Create new student account</a></div>
                <div class='credentials'>
                    <strong>Admin:</strong> admin / admin123<br />
                    <strong>Student:</strong> student / student123
                </div>
            </div>
            <script>
                function applyTheme() {{
                    const dark = localStorage.getItem('feedback-theme') === 'dark';
                    document.body.classList.toggle('dark-mode', dark);
                    document.querySelector('.theme-toggle').textContent = dark ? 'Light mode' : 'Dark mode';
                }}
                function toggleTheme() {{
                    localStorage.setItem('feedback-theme', document.body.classList.contains('dark-mode') ? 'light' : 'dark');
                    applyTheme();
                }}
                applyTheme();
            </script>
        </body>
        </html>
    ";
}

string StudentPageHtml(string username)
{
    var feedbackList = repository.GetAllFeedback()
        .Where(item => string.Equals(item.StudentName, username, StringComparison.OrdinalIgnoreCase))
        .ToList();
    var averages = repository.GetAverageRatingsByCourse();
    var totalFeedback = feedbackList.Count;
    var averageScore = totalFeedback == 0 ? 0 : feedbackList.Average(x => x.Rating);
    var topCourse = averages.Count == 0 ? "No data" : averages.OrderByDescending(x => x.Value).First().Key;

    var feedbackRows = feedbackList.Count == 0
        ? "<tr><td colspan='10'>No feedback submitted yet.</td></tr>"
        : string.Join(Environment.NewLine, feedbackList.Select(item => $@"
            <tr>
                <td>{item.StudentName}</td>
                <td>{item.Department}</td>
                <td>{item.CourseName}</td>
                <td>{item.Subject}</td>
                <td>{item.FacultyTeacher}</td>
                <td>{item.Rating}</td>
                <td>{item.Comment}</td>
                <td>{item.SubmittedOn:yyyy-MM-dd}</td>
                <td>{(item.IsRead ? "<span style='color: #16a34a; font-weight: bold;'>Read by admin</span>" : "<span style='color: #d97706; font-weight: bold;'>Waiting for admin</span>")}</td>
                <td><a href='/feedback/{item.Id}/edit'>Edit</a>
                    <form method='post' action='/feedback/{item.Id}/delete' style='display: inline; margin-left: 8px;' onsubmit='return confirm(""Delete this feedback?"");'><button type='submit'>Delete</button></form></td>
            </tr>"));

    var averageRows = averages.Count == 0
        ? "<p>No course ratings available yet.</p>"
        : string.Join(Environment.NewLine, averages.Select(item => $@"<li><strong>{item.Key}</strong>: {item.Value:F2}/5</li>"));

    return $@"
        <!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='utf-8' />
            <title>Student Dashboard</title>
            <style>
                :root {{ --panel: #ffffff; --red: #b91c1c; --red-dark: #7f1d1d; --black: #111111; --muted: #6b7280; --line: #e5e7eb; --green: #15803d; }}
                * {{ box-sizing: border-box; }}
                body {{ font-family: Arial, sans-serif; margin: 0; background: linear-gradient(135deg, #111111, #450a0a 52%, #f3f4f6); color: var(--black); }}
                .container {{ max-width: 1200px; margin: 0 auto; padding: 30px 20px 50px; }}
                .topbar {{ display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; padding-bottom: 18px; border-bottom: 1px solid rgba(37, 99, 235, 0.18); }}
                .brand {{ font-size: 2rem; font-weight: 800; color: white; letter-spacing: .02em; }}
                .brand span {{ color: #fecaca; font-size: 1rem; margin-left: 10px; font-weight: 600; }}
                .logout {{ text-decoration: none; background: var(--red); color: white; padding: 10px 16px; border-radius: 8px; font-weight: bold; transition: transform .2s, background .2s; }}
                .logout:hover {{ background: var(--red-dark); transform: translateY(-2px); }}
                .stats {{ display: grid; grid-template-columns: repeat(3, minmax(180px, 1fr)); gap: 16px; margin-bottom: 24px; }}
                .stat-card {{ background: var(--panel); padding: 18px; border-radius: 12px; border-top: 4px solid var(--red); box-shadow: 0 8px 22px rgba(0, 0, 0, 0.18); transition: transform .2s, box-shadow .2s; }}
                .stat-card:nth-child(2) {{ border-top-color: var(--black); }}
                .stat-card:nth-child(3) {{ border-top-color: #991b1b; }}
                .stat-card:hover {{ transform: translateY(-4px); box-shadow: 0 14px 28px rgba(15, 23, 42, 0.14); }}
                .stat-label {{ color: var(--muted); font-size: 0.9rem; }}
                .stat-value {{ font-size: 2rem; font-weight: 700; margin-top: 8px; }}
                .content-grid {{ display: grid; grid-template-columns: 1.15fr 0.85fr; gap: 22px; }}
                .card {{ background: rgba(255, 255, 255, 0.97); border-radius: 12px; border: 1px solid #fecaca; box-shadow: 0 8px 22px rgba(0, 0, 0, 0.16); padding: 22px; }}
                h2 {{ margin-top: 0; }}
                .field {{ margin-bottom: 15px; }}
                label {{ display: block; margin-bottom: 6px; font-weight: bold; }}
                input, textarea, select {{ width: 100%; padding: 12px; border: 1px solid var(--line); border-radius: 8px; font-size: 1rem; }}
                input:focus, textarea:focus, select:focus {{ outline: 3px solid rgba(185, 28, 28, 0.18); border-color: var(--red); }}
                button {{ background: var(--red); color: white; border: none; padding: 12px 18px; font-weight: bold; border-radius: 8px; cursor: pointer; transition: transform .2s, background .2s; }}
                button:hover {{ background: var(--red-dark); transform: translateY(-2px); }}
                table {{ width: 100%; border-collapse: collapse; margin-top: 10px; }}
                th, td {{ border-bottom: 1px solid var(--line); padding: 12px 10px; text-align: left; }}
                th {{ background: var(--black); color: white; }}
                tbody tr {{ transition: background .2s; }}
                tbody tr:hover {{ background: #fef2f2; }}
                a {{ color: var(--red); font-weight: 700; }}
                a:hover {{ color: var(--red-dark); }}
                .table-tools {{ display: flex; justify-content: flex-end; margin-bottom: 10px; }}
                .table-tools input {{ max-width: 340px; background: #ffffff; }}
                @media (max-width: 760px) {{ .topbar {{ align-items: flex-start; gap: 14px; }} .brand {{ font-size: 1.5rem; }} .stats, .content-grid {{ grid-template-columns: 1fr; }} .card {{ overflow-x: auto; }} table {{ min-width: 760px; }} }}
                .theme-toggle {{ position: fixed; top: 8px; right: 8px; z-index: 10; background: white; color: var(--black); border: 1px solid #fecaca; padding: 4px 6px; border-radius: 5px; cursor: pointer; font-size: .65rem; font-weight: bold; }}
                .dark-mode {{ background: #050505 !important; color: #f3f4f6; }}
                .dark-mode .card, .dark-mode .stat-card {{ background: #18181b; color: #f3f4f6; border-color: #3f3f46; }}
                .dark-mode input, .dark-mode textarea, .dark-mode select {{ background: #27272a; color: white; border-color: #52525b; }}
                .dark-mode th {{ background: #000000; }}
                .dark-mode td {{ border-color: #3f3f46; }}
                .dark-mode tbody tr:hover {{ background: #3f1111; }}
                .dark-mode .theme-toggle {{ background: #b91c1c; color: white; border-color: #ef4444; }}
                ul {{ margin: 0; padding-left: 18px; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='topbar'>
                    <div class='brand'>Parul University <span>Student Dashboard</span></div>
                    <button class='theme-toggle' type='button' onclick='toggleTheme()'>Dark mode</button>
                    <a class='logout' href='/logout'>Logout</a>
                </div>

                <div class='stats'>
                    <div class='stat-card'>
                        <div class='stat-label'>Total Responses</div>
                        <div class='stat-value'>{totalFeedback}</div>
                    </div>
                    <div class='stat-card'>
                        <div class='stat-label'>Average Rating</div>
                        <div class='stat-value'>{averageScore:F2}/5</div>
                    </div>
                    <div class='stat-card'>
                        <div class='stat-label'>Top Course</div>
                        <div class='stat-value'>{topCourse}</div>
                    </div>
                </div>

                <div class='content-grid'>
                    <div class='card'>
                        <h2>Submit Your Feedback</h2>
                        <form method='post' action='/feedback'>
                            <input type='hidden' name='studentName' value='{username}' />
                            <p><strong>Student:</strong> {username}</p>
                            <div class='field'>
                                <label for='department'>Department</label>
                                <input id='department' name='department' required />
                            </div>
                            <div class='field'>
                                <label for='courseName'>Course Name</label>
                                <input id='courseName' name='courseName' required />
                            </div>
                            <div class='field'>
                                <label for='subject'>Subject</label>
                                <input id='subject' name='subject' required />
                            </div>
                            <div class='field'>
                                <label for='facultyTeacher'>Faculty Teacher</label>
                                <input id='facultyTeacher' name='facultyTeacher' required />
                            </div>
                            <div class='field'>
                                <label for='rating'>Rating</label>
                                <select id='rating' name='rating'>
                                    <option value='5'>5 - Excellent</option>
                                    <option value='4'>4 - Good</option>
                                    <option value='3'>3 - Average</option>
                                    <option value='2'>2 - Poor</option>
                                    <option value='1'>1 - Very Poor</option>
                                </select>
                            </div>
                            <div class='field'>
                                <label for='comment'>Comment</label>
                                <textarea id='comment' name='comment' rows='4'></textarea>
                            </div>
                            <button type='submit'>Submit Feedback</button>
                        </form>
                    </div>

                    <div class='card'>
                        <h2>Course Averages</h2>
                        {averageRows}
                    </div>
                </div>

                <div class='card' style='margin-top: 24px;'>
                    <h2>Recent Feedback</h2>
                    <div class='table-tools'><input id='studentFeedbackSearch' placeholder='Search your feedback...' oninput='filterFeedback(""studentFeedbackSearch"", ""studentFeedbackTable"")' /></div>
                    <table id='studentFeedbackTable'>
                        <thead>
                            <tr>
                                <th>Student</th>
                                <th>Department</th>
                                <th>Course</th>
                                <th>Subject</th>
                                <th>Faculty Teacher</th>
                                <th>Rating</th>
                                <th>Comment</th>
                                <th>Date</th>
                                <th>Status</th>
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            {feedbackRows}
                        </tbody>
                    </table>
                </div>
            </div>
            <script>
                function applyTheme() {{
                    const dark = localStorage.getItem('feedback-theme') === 'dark';
                    document.body.classList.toggle('dark-mode', dark);
                    document.querySelector('.theme-toggle').textContent = dark ? 'Light mode' : 'Dark mode';
                }}
                function toggleTheme() {{
                    localStorage.setItem('feedback-theme', document.body.classList.contains('dark-mode') ? 'light' : 'dark');
                    applyTheme();
                }}
                applyTheme();
                function filterFeedback(inputId, tableId) {{
                    const term = document.getElementById(inputId).value.toLowerCase();
                    document.querySelectorAll('#' + tableId + ' tbody tr').forEach(row => {{
                        row.style.display = row.innerText.toLowerCase().includes(term) ? '' : 'none';
                    }});
                }}
            </script>
        </body>
        </html>
    ";
}

string AdminPageHtml()
{
    var feedbackList = repository.GetAllFeedback();
    var averages = repository.GetAverageRatingsByCourse();
    var totalFeedback = feedbackList.Count;
    var averageScore = totalFeedback == 0 ? 0 : feedbackList.Average(x => x.Rating);
    var bestCourse = averages.Count == 0 ? "No data" : averages.OrderByDescending(x => x.Value).First().Key;

    var feedbackRows = feedbackList.Count == 0
        ? "<tr><td colspan='9'>No feedback submitted yet.</td></tr>"
        : string.Join(Environment.NewLine, feedbackList.Select(item => $@"
            <tr>
                <td>{item.StudentName}</td>
                <td>{item.Department}</td>
                <td>{item.CourseName}</td>
                <td>{item.Subject}</td>
                <td>{item.FacultyTeacher}</td>
                <td>{item.Rating}</td>
                <td>{item.Comment}</td>
                <td>{item.SubmittedOn:yyyy-MM-dd}</td>
                <td>{(item.IsRead ? $"<span style='color: #16a34a; font-weight: bold;'>Read</span><form method='post' action='/admin/feedback/{item.Id}/status' style='margin-top: 8px;'><input type='hidden' name='isRead' value='false' /><button type='submit'>Mark as not read</button></form>" : $"<span style='color: #d97706; font-weight: bold;'>Not read</span><form method='post' action='/admin/feedback/{item.Id}/status' style='margin-top: 8px;'><input type='hidden' name='isRead' value='true' /><button type='submit'>Mark as read</button></form>")}</td>
            </tr>"));

    var averageRows = averages.Count == 0
        ? "<p>No course ratings available yet.</p>"
        : string.Join(Environment.NewLine, averages.Select(item => $@"<li><strong>{item.Key}</strong>: {item.Value:F2}/5</li>"));

    return $@"
        <!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='utf-8' />
            <title>Admin Dashboard</title>
            <style>
                :root {{ --panel: #ffffff; --red: #b91c1c; --red-dark: #7f1d1d; --black: #111111; --muted: #6b7280; --line: #e5e7eb; }}
                * {{ box-sizing: border-box; }}
                body {{ font-family: Arial, sans-serif; margin: 0; background: linear-gradient(135deg, #111111, #450a0a 52%, #f3f4f6); color: var(--black); }}
                .container {{ max-width: 1200px; margin: 0 auto; padding: 32px 20px 50px; }}
                .topbar {{ display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; padding-bottom: 18px; border-bottom: 1px solid rgba(37, 99, 235, 0.18); }}
                .brand {{ font-size: 2rem; font-weight: 800; color: white; letter-spacing: .02em; }}
                .brand span {{ color: #fecaca; font-size: 1rem; margin-left: 10px; font-weight: 600; }}
                .logout {{ text-decoration: none; background: var(--red); color: white; padding: 10px 16px; border-radius: 8px; font-weight: bold; transition: transform .2s, background .2s; }}
                .logout:hover {{ background: var(--red-dark); transform: translateY(-2px); }}
                .stats {{ display: grid; grid-template-columns: repeat(3, minmax(180px, 1fr)); gap: 16px; margin-bottom: 24px; }}
                .stat-card {{ background: var(--panel); padding: 20px; border-radius: 12px; border-top: 4px solid var(--red); box-shadow: 0 8px 22px rgba(0, 0, 0, 0.18); transition: transform .2s, box-shadow .2s; }}
                .stat-card:nth-child(2) {{ border-top-color: var(--black); }}
                .stat-card:nth-child(3) {{ border-top-color: #991b1b; }}
                .stat-card:hover {{ transform: translateY(-4px); box-shadow: 0 14px 28px rgba(15, 23, 42, 0.14); }}
                .stat-label {{ color: var(--muted); font-size: 0.9rem; }}
                .stat-value {{ font-size: 2rem; font-weight: 700; margin-top: 8px; }}
                .panel {{ background: rgba(255, 255, 255, 0.97); padding: 24px; border-radius: 12px; border: 1px solid #fecaca; box-shadow: 0 8px 22px rgba(0, 0, 0, 0.16); margin-top: 20px; }}
                table {{ width: 100%; border-collapse: collapse; margin-top: 10px; }}
                th, td {{ border-bottom: 1px solid var(--line); padding: 12px 10px; text-align: left; }}
                th {{ background: var(--black); color: white; }}
                tbody tr {{ transition: background .2s; }}
                tbody tr:hover {{ background: #fef2f2; }}
                button {{ background: var(--red); color: white; border: none; padding: 8px 12px; font-weight: bold; border-radius: 8px; cursor: pointer; transition: transform .2s, background .2s; }}
                button:hover {{ background: var(--red-dark); transform: translateY(-2px); }}
                .table-tools {{ display: flex; justify-content: flex-end; margin-bottom: 10px; }}
                .table-tools input {{ max-width: 340px; width: 100%; padding: 12px; border: 1px solid var(--line); border-radius: 8px; }}
                .table-tools input:focus {{ outline: 3px solid rgba(185, 28, 28, 0.18); border-color: var(--red); }}
                @media (max-width: 760px) {{ .topbar {{ align-items: flex-start; gap: 14px; }} .brand {{ font-size: 1.5rem; }} .stats {{ grid-template-columns: 1fr; }} .panel {{ overflow-x: auto; }} table {{ min-width: 760px; }} }}
                .theme-toggle {{ position: fixed; top: 8px; right: 8px; z-index: 10; background: white; color: var(--black); border: 1px solid #fecaca; padding: 4px 6px; border-radius: 5px; cursor: pointer; font-size: .65rem; font-weight: bold; }}
                .dark-mode {{ background: #050505 !important; color: #f3f4f6; }}
                .dark-mode .panel, .dark-mode .stat-card {{ background: #18181b; color: #f3f4f6; border-color: #3f3f46; }}
                .dark-mode input {{ background: #27272a; color: white; border-color: #52525b; }}
                .dark-mode th {{ background: #000000; }}
                .dark-mode td {{ border-color: #3f3f46; }}
                .dark-mode tbody tr:hover {{ background: #3f1111; }}
                .dark-mode .theme-toggle {{ background: #b91c1c; color: white; border-color: #ef4444; }}
                ul {{ margin: 0; padding-left: 18px; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='topbar'>
                    <div class='brand'>Parul University <span>Admin Dashboard</span></div>
                    <button class='theme-toggle' type='button' onclick='toggleTheme()'>Dark mode</button>
                    <a class='logout' href='/logout'>Logout</a>
                </div>

                <div class='stats'>
                    <div class='stat-card'>
                        <div class='stat-label'>Total Feedback</div>
                        <div class='stat-value'>{totalFeedback}</div>
                    </div>
                    <div class='stat-card'>
                        <div class='stat-label'>Average Rating</div>
                        <div class='stat-value'>{averageScore:F2}/5</div>
                    </div>
                    <div class='stat-card'>
                        <div class='stat-label'>Best Course</div>
                        <div class='stat-value'>{bestCourse}</div>
                    </div>
                </div>

                <div class='panel'>
                    <h2>Course Performance Summary</h2>
                    {averageRows}
                </div>

                <div class='panel'>
                    <h2>All Student Feedback</h2>
                    <div class='table-tools'><input id='adminFeedbackSearch' placeholder='Search student, course, or comment...' oninput='filterFeedback(""adminFeedbackSearch"", ""adminFeedbackTable"")' /></div>
                    <table id='adminFeedbackTable'>
                        <thead>
                            <tr>
                                <th>Student</th>
                                <th>Department</th>
                                <th>Course</th>
                                <th>Subject</th>
                                <th>Faculty Teacher</th>
                                <th>Rating</th>
                                <th>Comment</th>
                                <th>Date</th>
                                <th>Status</th>
                            </tr>
                        </thead>
                        <tbody>
                            {feedbackRows}
                        </tbody>
                    </table>
                </div>
            </div>
            <script>
                function applyTheme() {{
                    const dark = localStorage.getItem('feedback-theme') === 'dark';
                    document.body.classList.toggle('dark-mode', dark);
                    document.querySelector('.theme-toggle').textContent = dark ? 'Light mode' : 'Dark mode';
                }}
                function toggleTheme() {{
                    localStorage.setItem('feedback-theme', document.body.classList.contains('dark-mode') ? 'light' : 'dark');
                    applyTheme();
                }}
                applyTheme();
                function filterFeedback(inputId, tableId) {{
                    const term = document.getElementById(inputId).value.toLowerCase();
                    document.querySelectorAll('#' + tableId + ' tbody tr').forEach(row => {{
                        row.style.display = row.innerText.toLowerCase().includes(term) ? '' : 'none';
                    }});
                }}
            </script>
        </body>
        </html>
    ";
}

string FeedbackEditPageHtml(CourseFeedback feedback, string action, string backPath)
{
    var studentName = System.Net.WebUtility.HtmlEncode(feedback.StudentName);
    var department = System.Net.WebUtility.HtmlEncode(feedback.Department);
    var courseName = System.Net.WebUtility.HtmlEncode(feedback.CourseName);
    var subject = System.Net.WebUtility.HtmlEncode(feedback.Subject);
    var facultyTeacher = System.Net.WebUtility.HtmlEncode(feedback.FacultyTeacher);
    var comment = System.Net.WebUtility.HtmlEncode(feedback.Comment);
    var studentField = $"<input type='hidden' name='studentName' value='{studentName}' /><p><strong>Student:</strong> {studentName}</p>";

    return $@"
        <!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='utf-8' />
            <title>Edit Feedback</title>
            <style>
                body {{ font-family: Arial, sans-serif; background: linear-gradient(135deg, #111111, #450a0a); margin: 0; padding: 40px; }}
                .container {{ max-width: 520px; margin: 60px auto; background: white; padding: 30px; border-radius: 12px; box-shadow: 0 8px 20px rgba(0,0,0,0.1); }}
                h1 {{ color: #111111; margin-top: 8px; }}
                .university {{ color: #b91c1c; font-size: 1.25rem; font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }}
                .field {{ margin-bottom: 16px; }}
                label {{ display: block; margin-bottom: 6px; font-weight: bold; }}
                input, textarea, select {{ width: 100%; padding: 12px; box-sizing: border-box; border: 1px solid #cbd5e1; border-radius: 8px; font-size: 1rem; }}
                button {{ background: #b91c1c; color: white; border: none; padding: 12px 18px; border-radius: 8px; cursor: pointer; font-weight: bold; }}
                .theme-toggle {{ position: fixed; top: 8px; right: 8px; z-index: 10; background: #111111; color: white; border: 1px solid #b91c1c; padding: 4px 6px; border-radius: 5px; cursor: pointer; font-size: .65rem; font-weight: bold; }}
                .dark-mode {{ background: #050505 !important; color: #f3f4f6; }}
                .dark-mode .container {{ background: #18181b; color: #f3f4f6; }}
                .dark-mode h1 {{ color: white; }}
                .dark-mode input, .dark-mode textarea, .dark-mode select {{ background: #27272a; color: white; border-color: #52525b; }}
                .dark-mode .theme-toggle {{ background: white; color: #111111; }}
            </style>
        </head>
        <body>
            <button class='theme-toggle' type='button' onclick='toggleTheme()'>Dark mode</button>
            <div class='container'>
                <div class='university'>Parul University</div>
                <h1>Edit Feedback</h1>
                <form method='post' action='{action}'>
                    <div class='field'>{studentField}</div>
                    <div class='field'>
                        <label for='department'>Department</label>
                        <input id='department' name='department' value='{department}' required />
                    </div>
                    <div class='field'>
                        <label for='courseName'>Course Name</label>
                        <input id='courseName' name='courseName' value='{courseName}' required />
                    </div>
                    <div class='field'>
                        <label for='subject'>Subject</label>
                        <input id='subject' name='subject' value='{subject}' required />
                    </div>
                    <div class='field'>
                        <label for='facultyTeacher'>Faculty Teacher</label>
                        <input id='facultyTeacher' name='facultyTeacher' value='{facultyTeacher}' required />
                    </div>
                    <div class='field'>
                        <label for='rating'>Rating</label>
                        <select id='rating' name='rating'>
                            <option value='5' {(feedback.Rating == 5 ? "selected" : string.Empty)}>5 - Excellent</option>
                            <option value='4' {(feedback.Rating == 4 ? "selected" : string.Empty)}>4 - Good</option>
                            <option value='3' {(feedback.Rating == 3 ? "selected" : string.Empty)}>3 - Average</option>
                            <option value='2' {(feedback.Rating == 2 ? "selected" : string.Empty)}>2 - Poor</option>
                            <option value='1' {(feedback.Rating == 1 ? "selected" : string.Empty)}>1 - Very Poor</option>
                        </select>
                    </div>
                    <div class='field'>
                        <label for='comment'>Comment</label>
                        <textarea id='comment' name='comment' rows='5'>{comment}</textarea>
                    </div>
                    <button type='submit'>Save Changes</button>
                    <a href='{backPath}'>Cancel</a>
                </form>
            </div>
            <script>
                function applyTheme() {{
                    const dark = localStorage.getItem('feedback-theme') === 'dark';
                    document.body.classList.toggle('dark-mode', dark);
                    document.querySelector('.theme-toggle').textContent = dark ? 'Light mode' : 'Dark mode';
                }}
                function toggleTheme() {{
                    localStorage.setItem('feedback-theme', document.body.classList.contains('dark-mode') ? 'light' : 'dark');
                    applyTheme();
                }}
                applyTheme();
            </script>
        </body>
        </html>
    ";
}

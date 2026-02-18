using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRApp.API.Models;
using HRApp.API.Services;
using HRApp.Infrastructure.Data;
using System.Security.Claims;

namespace HRApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IGroqService _groqService;
        private readonly AppDbContext _context;
        private readonly ILogger<ChatController> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public ChatController(IGroqService groqService, AppDbContext context, ILogger<ChatController> logger)
        {
            _groqService = groqService;
            _context = context;
            _logger = logger;
        }

        public class ChatRequest
        {
            public string Message { get; set; } = string.Empty;
            public List<GroqMessage>? History { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var fullName = User.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
                return Unauthorized();

            // Build schema-aware system prompt
            var schemaInfo = GetDatabaseSchema();
            var systemPrompt = $@"You are an intelligent HR Database Assistant with direct SQL generation capabilities.

            CURRENT CONTEXT:
            - User Identity: {userEmail}
            - User Role: {userRole}
            - User ID: {userId}
            - Name: {fullName}

            DATABASE SCHEMA:
            {schemaInfo}

            IDENTITY LOCKDOWN (MANDATORY):
            - If Role is 'Employee', you are STRICTLY FORBIDDEN from generating SQL that filters by name, email, or any attribute other than the User ID: '{userId}'.
            - Every query on ""Employees"" should use: WHERE ""Id"" = '{userId} where applicable to ensure only their own record is accessed.'
            - Every query on ""Salaries"", ""LeaveRequests"", or ""Loans"" should use: WHERE ""EmployeeId"" = '{userId}'
            - Even if the user says 'Show profile for [Their Name]', you MUST ignore the name and use the ID '{userId}'.
            - HR Role: You are authorized to view any employee's data. The identity lockdown rules do NOT apply to you. You may generate SQL that filters by any employee's name or ID.

            ACCESS DENIAL & REFUSAL:
            - If an 'Employee' asks for information about another person (e.g., 'What is Jane's salary?'), you MUST NOT generate SQL.
            - You MUST respond: 
                {{""intent"": ""conversation"", ""response"": ""I'm sorry, as an Employee you are only authorized to access your own personal HR records.""}}

            INSTRUCTIONS:
            - Respond with: {{""intent"": ""conversation"", ""response"": ""I'm sorry, you only have authorization to access your own personal HR records.""}} if an Employee tries to access data they shouldn't.
            - NEVER generate DELETE or DROP statements
            - NEVER expose passwords or internal IDs
            - For numeric grade comparisons, use the computed column ""GradeNumber"" (e.g., ""GradeNumber"" >= 10).
            - Always use double quotes around table and column names (e.g., SELECT ""FullName"" FROM ""Employees"").
            - Parameter values can be placed directly in the SQL (we handle sanitization), but use proper escaping if needed.

            ACTIONS (HR only):
            - create_employee: Creates a new employee. Requires: fullName, email, department, grade, salary.
            - promote_employee: Promotes an employee. Requires: employeeName, newGrade (optional newSalary).

            Loan Guidelines (for reference):
            - If the user asks about loan eligibility, car loans, housing loans, or personal loans, set intent to ""loan_eligibility"", include the loan type in your response, and you can query the ""Loans"" table.
            - Car Loan: Grade 10+, Salary >= 8000 AED, no existing active car loan
            - Housing Loan: Grade 12+, Salary >= 15000 AED, 2+ years tenure, no existing housing loan  
            - Personal Loan: Any active employee, max 1x salary
            - Max loan amounts: Car ~5x salary, Housing ~10x salary
            - For hypotheticals (""what if salary was X"", ""how much do I need""), query current data and explain the calculation versus eligibility thresholds.

            RESPONSE FORMAT:
            1. Analyze the user's question
            2. If it's a data query, generate a SAFE, read-only SQL query.
            3. If it's an action, return a JSON with the appropriate intent and required fields.
            3. Return ONLY a JSON object with this exact format:
            
            For data queries:
            {{
                ""intent"": ""description of what you're doing"",
                ""sql"": ""SELECT ..."",
                ""explanation"": ""human-readable explanation of the query""
            }}

            For create_employee:
            {{
                ""intent"": ""create_employee"",
                ""fullName"": ""..."",
                ""email"": ""..."",
                ""department"": ""..."",
                ""grade"": ""..."",
                ""salary"": 12345
            }}

            For promote_employee:
            {{
                ""intent"": ""promote_employee"",
                ""employeeName"": ""..."",
                ""newGrade"": ""..."",
                ""newSalary"": 12345   // optional
            }}

            For generate_salary_certificate:
            - Generates a salary certificate PDF. Requires: employeeName.
            - HR can generate for any employee. If they want to generate a salary certificate for an employee, ask for the employee's name and use it to find the record.
            Employees can only generate for themselves (and no one else).
            - If the user says ""for me"" or ""my certificate,"" use their Name: {fullName} as the employeeName. Otherwise, use the name provided in the request. 
            {{
                ""intent"": ""generate_salary_certificate"",
                ""employeeName"": ""...""
            }}

            For general chat that doesn't require database access:
            {{
                ""intent"": ""conversation"",
                ""response"": ""your helpful response""
            }}

            Examples:
            User: ""Who earns more than 10000?""
            Response: {{""intent"": ""salary_query"", ""sql"": ""SELECT e.""FullName"", e.""Grade"", e.""Department"" FROM ""Employees"" e WHERE e.""Id"" IN (SELECT s.""EmployeeId"" FROM ""Salaries"" s WHERE s.""BaseSalary"" > 10000 AND s.""EffectiveTo"" IS NULL) AND e.""Status"" = 'Active'"", ""explanation"": ""Finding active employees with current salary above 10000""}}

            User: ""Am I eligible for a car loan?""
            Response: {{""intent"": ""check_car_loan_eligibility"", ""sql"": ""SELECT e.""Grade"", e.""GradeNumber"", (SELECT ""BaseSalary"" FROM ""Salaries"" WHERE ""EmployeeId"" = e.""Id"" AND ""EffectiveTo"" IS NULL) as current_salary, (SELECT COUNT(*) FROM ""Loans"" WHERE ""EmployeeId"" = e.""Id"" AND ""LoanType"" = 'Car' AND ""Status"" = 'Active') as active_car_loans FROM ""Employees"" e WHERE e.""Id"" = '{userId}'"", ""explanation"": ""Getting grade, salary, and existing car loans to determine eligibility""}}

            User: ""Would my maximum car loan increase if my salary doubled?""
            Response: {{""intent"": ""analyze_car_loan_scenario"", ""sql"": ""SELECT e.""Grade"", (SELECT s.""BaseSalary"" FROM ""Salaries"" s WHERE s.""EmployeeId"" = e.""Id"" AND s.""EffectiveTo"" IS NULL) as current_salary, (SELECT COUNT(*) FROM ""Loans"" WHERE ""EmployeeId"" = e.""Id"" AND ""LoanType"" = 'Car' AND ""Status"" = 'Active') as active_car_loans FROM ""Employees"" e WHERE e.""Id"" = '{userId}'"", ""explanation"": ""Getting current salary to calculate current vs doubled scenario""}}

            User: ""Hire new employee Jane Cooper as a Finance manager with Grade 11 and salary 14000""
            Response: {{""intent"": ""create_employee"", ""fullName"": ""Jane Cooper"", ""email"": ""jane.cooper@dgi.com"", ""department"": ""Finance"", ""grade"": ""Grade 11"", ""salary"": 14000}}

            User: ""Generate a salary certificate for Jane Smith""
            Response: {{""intent"": ""generate_salary_certificate"", ""employeeName"": ""Jane Smith""}}

            User: ""Promote John Doe to Grade 12 and keep the salary the same""
            Response: {{""intent"": ""promote_employee"", ""employeeName"": ""John Doe"", ""newGrade"": ""Grade 12""}}
            
            User: ""How many IT employees are there?""
            Response: {{""intent"": ""count_query"", ""sql"": ""SELECT COUNT(*) FROM ""Employees"" WHERE ""Department"" = 'IT' AND ""Status"" = 'Active'"", ""explanation"": ""Counting active IT employees""}}";

            var messages = new List<GroqMessage> { new GroqMessage { Role = "system", Content = systemPrompt } };

            if (request.History != null)
                messages.AddRange(request.History.TakeLast(10)); // Keep last 10 for context

            messages.Add(new GroqMessage { Role = "user", Content = request.Message });

            try
            {
                // Get LLM response (no function calling - pure text generation)
                var completion = await _groqService.GetChatCompletionAsync(messages, null);
                var llmResponse = completion.Choices.FirstOrDefault()?.Message?.Content;
                _logger.LogInformation("User claims: Id={UserId}, Role={Role}, Email={Email}", userIdStr, userRole, userEmail);

                if (string.IsNullOrEmpty(llmResponse))
                    return Ok(new { answer = "I didn't understand that. Could you rephrase?" });

                // _logger.LogInformation("LLM Raw Response: {Response}", llmResponse);

                // Parse the JSON response from LLM
                var (intent, sql, explanation, conversationResponse, parameters) = ParseLlmResponse(llmResponse);

                // Handle conversational queries
                if (intent == "conversation" && !string.IsNullOrEmpty(conversationResponse))
                {
                    return Ok(new { answer = conversationResponse, type = "chat" });
                }

                // Check if this is a loan eligibility query
                if (intent == "loan_eligibility")
                {
                    // Extract loan type from the parsed response or from the user message
                    string? loanType = null;
                    
                    loanType = ExtractLoanType(request.Message);
                    
                    if (loanType == "All")
                    {
                        var results = await CheckAllLoans(userId);
                        return Ok(new { answer = results, type = "loan_check_all" });
                    }
                    else
                    {
                        var loanService = new LoanService(_context);
                        var eligibility = await loanService.CheckEligibilityAsync(userId, loanType);
                        var response = FormatLoanResponse(eligibility, loanType);
                        return Ok(new { answer = response, type = "loan_check" });
                    }
                }

                if (intent == "create_employee")
                {
                    if (userRole != "HR") return Ok(new { answer = "Only HR can create employees.", type = "error" });

                    // Extract parameters (with validation)
                    if (!parameters.TryGetValue("fullName", out var fullNameObj) ||
                        !parameters.TryGetValue("email", out var emailObj) ||
                        !parameters.TryGetValue("department", out var deptObj) ||
                        !parameters.TryGetValue("grade", out var gradeObj) ||
                        !parameters.TryGetValue("salary", out var salaryObj))
                    {
                        return Ok(new { answer = "Missing required fields for creating an employee. Please provide full name, email, department, grade, and salary." });
                    }

                    var createReq = new EmployeesController.CreateEmployeeRequest
                    {
                        FullName = fullNameObj.ToString()!,
                        Email = emailObj.ToString()!,
                        Department = deptObj.ToString()!,
                        Grade = gradeObj.ToString()!,
                        Role = "Employee",
                        BaseSalary = Convert.ToDecimal(salaryObj)
                    };

                    // Reuse the CreateEmployee method (or call it via a shared service)
                    var createResult = await new EmployeesController(_context).CreateEmployee(createReq);
                    if (createResult.Result is BadRequestObjectResult bad)
                        return Ok(new { answer = $"Error: {bad.Value}", type = "error" });

                    return Ok(new { answer = $"✅ Employee {createReq.FullName} created successfully.", type = "action_success" });
                }

                // Handle promote employee
                if (intent == "promote_employee")
                {
                    if (userRole != "HR") return Ok(new { answer = "Only HR can promote employees.", type = "error" });

                    if (!parameters.TryGetValue("employeeName", out var nameObj) ||
                        !parameters.TryGetValue("newGrade", out var gradeObj))
                    {
                        return Ok(new { answer = "Please specify the employee name and new grade." });
                    }

                    string employeeName = nameObj?.ToString() ?? "";
                    string newGrade = gradeObj?.ToString() ?? "";

                    if (string.IsNullOrWhiteSpace(employeeName) || string.IsNullOrWhiteSpace(newGrade))
                        return Ok(new { answer = "Employee name and new grade cannot be empty." });

                    decimal? newSalary = parameters.TryGetValue("newSalary", out var salaryObj) ? Convert.ToDecimal(salaryObj) : null;

                    var employee = await _context.Employees.FirstOrDefaultAsync(e => e.FullName.Contains(employeeName) && e.Status == "Active");
                    if (employee == null)
                        return Ok(new { answer = $"No active employee found with name '{employeeName}'.", type = "error" });

                    var promoteRequest = new EmployeesController.PromoteRequest { NewGrade = newGrade, NewSalary = newSalary ?? 0 };
                    var promoteResult = await new EmployeesController(_context).PromoteEmployee(employee.Id, promoteRequest);
                    if (promoteResult is BadRequestObjectResult bad)
                        return Ok(new { answer = $"Error: {bad.Value}", type = "error" });

                    return Ok(new { answer = $"✅ {employee.FullName} promoted to {newGrade} successfully.", type = "action_success" });
                }

                // Handle salary certificate
                if (intent == "generate_salary_certificate")
                {
                    // Try to get the name from LLM parameters; default to empty if not found
                    string employeeName = parameters.TryGetValue("employeeName", out var nameObj) && nameObj != null 
                    ? nameObj.ToString() ?? "" 
                    : "";

                    // If the name is empty or refers to "me", use the authenticated user's full name
                    if (string.IsNullOrWhiteSpace(employeeName) || 
                        employeeName.ToLower() == "me" || 
                        employeeName.ToLower() == "my" || 
                        employeeName.ToLower() == "myself")
                    {
                        employeeName = fullName ?? "";
                    }

                    // Prevent Employee role from requesting certificates for other people
                    if (userRole == "Employee" && !string.Equals(employeeName, fullName, StringComparison.OrdinalIgnoreCase))
                    {
                        return Ok(new { 
                            answer = "Access Denied: As an employee, you are only authorized to generate your own salary certificate.", 
                            type = "error" 
                        });
                    }

                    // Proceed with database lookup using the determined name
                    var employee = await _context.Employees
                    .FirstOrDefaultAsync(e =>
                        e.Status == "Active" &&
                        e.FullName != null &&
                        EF.Functions.ILike(e.FullName, $"%{employeeName}%"));

                    if (employee == null)
                        return Ok(new { answer = $"No active employee found with name '{employeeName}'.", type = "error" });

                    return Ok(new
                    {
                        answer = $"Click the button below to download the salary certificate for {employee.FullName}.",
                        type = "certificate",
                        employeeId = employee.Id,
                        employeeName = employee.FullName,
                        employeeCode = employee.EmployeeCode
                    });
                }

                // Validate and execute SQL
                if (!string.IsNullOrEmpty(sql))
                {
                    var validationResult = ValidateSql(sql, userRole!, userId);
                    if (!validationResult.IsValid)
                    {
                        return Ok(new { answer = $"I can't execute that query for security reasons: {validationResult.Reason}", type = "error" });
                    }

                    // Apply row-level security for non-HR users
                    // sql = ApplyRowLevelSecurity(sql, userRole!, userId);

                    var (results, columns) = await ExecuteDynamicSqlAsync(sql, userId);
                    var formattedAnswer = await FormatResultsWithLlmAsync(request.Message, results, columns, explanation);
                    
                    return Ok(new 
                    { 
                        answer = formattedAnswer, 
                        type = "data",
                        sql = userRole == "HR" ? sql : null, // Only show SQL to HR
                        rowCount = results.Count,
                        data = results // Raw data for frontend tables
                    });
                }

                return Ok(new { answer = "I'm not sure how to help with that. Try asking about employees, salaries, or leave.", type = "unknown" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat processing error");
                return Ok(new { answer = "I encountered an error processing your request. Please try again.", type = "error" });
            }
        }

        private string GetDatabaseSchema()
        {
            return @"
                Tables (use double‑quoted names exactly as shown):

                ""Employees"" (
                    ""Id"" uuid,
                    ""EmployeeCode"" text,
                    ""FullName"" text,
                    ""Email"" text,
                    ""Role"" text,
                    ""Grade"" text,          -- stored as 'Grade X' format, extract number for comparisons
                    ""Department"" text,
                    ""ManagerId"" uuid,
                    ""Status"" text,         -- 'Active', 'Archived', 'Terminated'
                    ""HireDate"" timestamptz,
                    ""TerminationDate"" timestamptz
                )

                ""Salaries"" (
                    ""Id"" uuid,
                    ""EmployeeId"" uuid,
                    ""BaseSalary"" numeric,
                    ""Currency"" text,
                    ""EffectiveFrom"" timestamptz,
                    ""EffectiveTo"" timestamptz   -- NULL means current salary
                )

                ""LeaveRequests"" (
                    ""Id"" uuid,
                    ""EmployeeId"" uuid,
                    ""StartDate"" timestamptz,
                    ""EndDate"" timestamptz,
                    ""Type"" text,          -- 'Annual', 'Sick', 'Emergency', etc.
                    ""Status"" text,        -- 'Pending', 'Approved', 'Rejected'
                    ""Reason"" text,
                    ""ApprovedById"" uuid
                )

                ""Skills"" (
                    ""Id"" uuid,
                    ""Name"" text
                )

                ""EmployeeSkills"" (
                    ""EmployeeId"" uuid,
                    ""SkillId"" uuid,
                    ""Level"" text          -- 'Beginner', 'Intermediate', 'Expert'
                )

                ""LeaveSummaries"" (
                    ""EmployeeId"" uuid,
                    ""Year"" integer,
                    ""AnnualEntitlement"" integer,
                    ""UsedDays"" integer,
                    ""RemainingDays"" integer
                )

                ""Loans"" (
                    ""Id"" uuid,
                    ""EmployeeId"" uuid,
                    ""LoanType"" text,      -- 'Car', 'Housing', 'Personal'
                    ""Amount"" numeric,
                    ""InterestRate"" numeric,
                    ""TenureMonths"" integer,
                    ""MonthlyDeduction"" numeric,
                    ""Status"" text,        -- 'Active', 'PaidOff', 'Defaulted'
                    ""StartDate"" timestamptz,
                    ""EndDate"" timestamptz,
                    ""WasEligible"" boolean,
                    ""EligibilityReason"" text
                )
                ";
        }

        private (string intent, string? sql, string? explanation, string? conversationResponse, Dictionary<string, object> parameters) ParseLlmResponse(string response)
        {
            try
            {
                // Extract JSON from potential markdown code blocks
                var jsonMatch = Regex.Match(response, @"```json\s*(\{.*?\})\s*```", RegexOptions.Singleline);
                if (jsonMatch.Success)
                    response = jsonMatch.Groups[1].Value;
                else
                {
                    // Try to find bare JSON object
                    jsonMatch = Regex.Match(response, @"(\{.*\})", RegexOptions.Singleline);
                    if (jsonMatch.Success)
                        response = jsonMatch.Groups[1].Value;
                }

                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                var intent = root.GetProperty("intent").GetString() ?? "unknown";
                var sql = root.TryGetProperty("sql", out var sqlProp) ? sqlProp.GetString() : null;
                var explanation = root.TryGetProperty("explanation", out var expProp) ? expProp.GetString() : null;
                var convResponse = root.TryGetProperty("response", out var respProp) ? respProp.GetString() : null;
                var loanType = root.TryGetProperty("loan_type", out var loanProp) ? loanProp.GetString() : null;
                var parameters = new Dictionary<string, object>();

                // Extract fields that might appear in action intents
                foreach (var prop in new[] { "fullName", "email", "department", "grade", "salary", "newGrade", "newSalary", "employeeName", "loanType" })
                {
                    if (root.TryGetProperty(prop, out var value))
                    {
                        parameters[prop] = value.ValueKind switch
                        {
                            JsonValueKind.Number => value.GetDecimal(),
                            JsonValueKind.String => value.GetString()!,
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => value.GetRawText()
                        };
                    }
                }

                return (intent, sql, explanation, convResponse, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse LLM response as JSON: {Response}", response);
                // Fallback: treat as conversation
                return ("conversation", null, null, response, new Dictionary<string, object>());
            }
        }

        private (bool IsValid, string Reason) ValidateSql(string sql, string userRole, Guid userId)
        {
            // Trim and remove trailing semicolon(s) – we'll allow one at the end
            string trimmedSql = sql.Trim();
            while (trimmedSql.EndsWith(';'))
                trimmedSql = trimmedSql[..^1].TrimEnd();

            var upperSql = trimmedSql.ToUpperInvariant();

            // Block dangerous operations
            var forbidden = new[] { "DELETE", "DROP", "TRUNCATE", "UPDATE", "INSERT", "ALTER", "CREATE", "GRANT", "REVOKE" };
            foreach (var word in forbidden)
            {
                if (Regex.IsMatch(upperSql, $@"\b{word}\b"))
                    return (false, $"Operation '{word}' is not permitted");
            }

            // Must be SELECT (case-insensitive start)
            if (!upperSql.TrimStart().StartsWith("SELECT"))
                return (false, "Only SELECT queries are allowed");

            // Check for SQL injection patterns (comment sequences, and any remaining semicolons)
            if (trimmedSql.Contains("--") || trimmedSql.Contains("/*") || trimmedSql.Contains("*/"))
                return (false, "Invalid comment sequences detected");

            // Ensure no semicolons remain (if any are left, they are inside the query – dangerous)
            if (trimmedSql.Contains(';'))
                return (false, "Multiple SQL statements detected");

            return (true, "Valid");
        }

        private string ApplyRowLevelSecurity(string sql, string userRole, Guid userId)
        {
            if (userRole == "HR") return sql;

            string employeeIdStr = userId.ToString();
            
            // Wrap the LLM's query to ensure the outer filter is always applied
            return $@"
                SELECT * FROM (
                    {sql.TrimEnd(';')}
                ) AS user_query 
                WHERE (
                    -- The subquery must contain one of these columns for the filter to work
                    user_query.""Id"" = '{employeeIdStr}' OR 
                    user_query.""EmployeeId"" = '{employeeIdStr}'
                )";
        }

        private async Task<(List<Dictionary<string, object>> Results, List<string> Columns)> ExecuteDynamicSqlAsync(string sql,Guid userId)
        {
            var results = new List<Dictionary<string, object>>();
            var columns = new List<string>();

            try
            {
                string trimmedSql = sql.Trim().TrimEnd(';');
                using (_logger.BeginScope(new               Dictionary<string, object>
                {
                    ["IsSqlAudit"] = true,
                    ["UserId"] = userId
                }))
                {
                    _logger.LogInformation(
                        "User {UserId} executed SQL: {Sql} | Rows: {Count}",
                        userId,
                        trimmedSql,
                        results.Count);
                }

                // Use EF Core's raw SQL execution with Dapper-like safety
                // We use a read-only approach with parameterized safety checks already done
                
                // For PostgreSQL, we need to handle the connection directly for true dynamic SQL
                var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = trimmedSql;
                command.CommandTimeout = 30;

                using var reader = await command.ExecuteReaderAsync();
                
                // Get column names from schema
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(reader.GetName(i));
                }

                // Read rows
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.GetValue(i);
                        // Handle DB null
                        row[columns[i]] = value == DBNull.Value ? null! : value;
                    }
                    results.Add(row);
                }

                await connection.CloseAsync();
                
                // _logger.LogInformation("Executed SQL: {Sql} | Rows: {Count}", trimmedSql, results.Count);
                
                return (results, columns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SQL execution error for query: {Sql}", sql);
                throw new InvalidOperationException($"Database error: {ex.Message}");
            }
        }

        private string ExtractLoanType(string message)
        {
            var lower = message.ToLower();
            
            // Broad queries
            if (lower.Contains("any") || lower.Contains("all") || lower.Contains("what loans") || lower.Contains("available"))
                return "All";
            
            if (lower.Contains("car") || lower.Contains("vehicle") || lower.Contains("auto")) return "Car";
            if (lower.Contains("house") || lower.Contains("housing") || lower.Contains("home") || lower.Contains("mortgage")) return "Housing";
            if (lower.Contains("personal")) return "Personal";
            
            return "All"; // Default to all
        }

        private bool IsLoanQuery(string message, string intent)
        {
            var lowerMessage = message.ToLower();
            var loanKeywords = new[] { "loan", "eligible", "eligibility", "qualify", "qualification", "afford", "borrow", "lending", "finance", "financing" };
            var loanTypes = new[] { "car", "vehicle", "auto", "automotive", "housing", "house", "home", "mortgage", "personal" };
            
            return loanKeywords.Any(k => lowerMessage.Contains(k)) && 
                loanTypes.Any(t => lowerMessage.Contains(t));
        }

        private string FormatLoanResponse(LoanEligibilityResult result, string loanType)
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine($"{loanType} Loan Eligibility");
            
            if (result.IsEligible)
            {
                sb.AppendLine("✅ ELIGIBLE");
                sb.AppendLine();
                sb.AppendLine($"Maximum Amount: AED {result.MaxAmount:N0}");
                sb.AppendLine($"Suggested Tenure: {result.SuggestedTenure} months");
                sb.AppendLine($"Estimated Monthly Deduction: AED {result.SuggestedMonthlyDeduction:N0}");
                sb.AppendLine();
                sb.AppendLine("Requirements Met:");
                foreach (var req in result.RequirementsMet)
                {
                    sb.AppendLine($"• ✓ {req}");
                }
            }
            else
            {
                sb.AppendLine("❌ NOT ELIGIBLE");
                sb.AppendLine();
                sb.AppendLine($"Reason: {result.Reason}");
                sb.AppendLine();
                if (result.RequirementsMet.Any())
                {
                    sb.AppendLine("Requirements Met:");
                    foreach (var req in result.RequirementsMet)
                    {
                        sb.AppendLine($"• ✓ {req}");
                    }
                    sb.AppendLine();
                }
                sb.AppendLine("Requirements Missing:");
                foreach (var req in result.RequirementsMissing)
                {
                    sb.AppendLine($"• ✗ {req}");
                }
            }
            
            return sb.ToString();
        }

        private async Task<string> CheckAllLoans(Guid userId)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Loan Eligibility Summary");
            sb.AppendLine();
            
            var loanService = new LoanService(_context);
            var loanTypes = new[] { "Car", "Housing", "Personal" };
            var anyEligible = false;
            
            foreach (var loanType in loanTypes)
            {
                var result = await loanService.CheckEligibilityAsync(userId, loanType);
                var icon = result.IsEligible ? "✅" : "❌";
                sb.AppendLine($"{icon} {loanType} Loan: {(result.IsEligible ? "ELIGIBLE" : "Not eligible")}");
                
                if (result.IsEligible)
                {
                    anyEligible = true;
                    sb.AppendLine($"   Up to AED {result.MaxAmount:N0} (AED {result.SuggestedMonthlyDeduction:N0}/month)");
                }
                else
                {
                    var mainReason = result.RequirementsMissing.FirstOrDefault() ?? "Requirements not met";
                    sb.AppendLine($"   {mainReason}");
                }
                sb.AppendLine();
            }
            
            if (!anyEligible)
            {
                sb.AppendLine("Tip: Improve your eligibility by:");
                sb.AppendLine("• Increasing tenure (for housing loans)");
                sb.AppendLine("• Checking with HR about salary adjustments");
                sb.AppendLine("• Paying off existing loans first");
            }
            
            return sb.ToString();
        }

        private decimal? ExtractHypotheticalSalary(string message)
        {
            // Match patterns like "15000", "15,000", "15k"
            var match = Regex.Match(message, @"(?:if|to|at|was|increased to|raised to)\s+(?:AED\s*)?(\d{1,3}(?:,\d{3})+|\d+)(?:\s*k)?", RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                var salaryStr = match.Groups[1].Value.Replace(",", "");
                if (decimal.TryParse(salaryStr, out var salary))
                {
                    // Handle "15k" format
                    if (message.ToLower().Contains("k") && salary < 1000)
                        salary *= 1000;
                    return salary;
                }
            }
            
            return null;
        }

        private async Task<string> FormatResultsWithLlmAsync(string originalQuestion, List<Dictionary<string, object>> results, List<string> columns, string? explanation)
        {
            // Console.WriteLine($"[DEBUG] FormatResultsWithLlmAsync called with {results?.Count ?? 0} results");

            if (results == null || results.Count == 0)
                return "I found no matching records for your query.";

            // For small results, check if it's a loan/hypothetical query that needs special formatting
            var lowerQ = originalQuestion.ToLower();
            var isLoanQuery = lowerQ.Contains("loan") || lowerQ.Contains("eligible") || lowerQ.Contains("qualify");
            var isHypothetical = lowerQ.Contains("if") || lowerQ.Contains("would") || lowerQ.Contains("doubled") || lowerQ.Contains("increase");

            // _logger.LogInformation("isLoanQuery: {IsLoan}, isHypothetical: {IsHypo}", isLoanQuery, isHypothetical);

            var dataJson = JsonSerializer.Serialize(new { 
                rowCount = results.Count, 
                sample = results.Take(15) 
            }, new JsonSerializerOptions { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // _logger.LogInformation("Data JSON length: {Length}", dataJson.Length);
            
            string prompt;
            
            if (isLoanQuery || isHypothetical)
            {
                prompt = $@"The user is asking about loan eligibility: ""{originalQuestion}""

                COMPANY LOAN RULES:
                - Car Loan: Must be Grade 10 or higher AND Salary >= 8,000 AED. Max amount is 5x monthly salary.
                - Housing Loan: Must be Grade 12 or higher AND Salary >= 15,000 AED AND at least 2 years tenure. Max amount is 10x monthly salary.
                - Personal Loan: Any active employee qualifies. Max amount is 1x monthly salary.

                USER DATA FROM DATABASE:
                {dataJson}

                INSTRUCTIONS:
                1. Compare the user's data (Grade, Salary, Tenure) against the rules above.
                2. If they are asking a 'What if' (hypothetical), calculate the difference between their current status and the hypothetical goal.
                3. Provide a natural, encouraging response. Do not show raw JSON.
                4. If they are eligible, state the maximum amount they can borrow.
                5. Explain their eligibility clearly:
                - Which requirements they meet or don't meet
                - What max amount they qualify for
                - Any existing loans that might affect eligibility

                Hypothetical Explaination:
                1. Their current situation (grade, salary, existing loans)
                2. Answer their specific hypothetical scenario with calculations
                3. Show the math clearly (e.g., ""5x salary rule: 25,000 × 5 = 125,000 max"")
                4. Be conversational and helpful";
            }
            else // For regular data queries, use natural language
            {
                prompt = $@"You are an HR assistant summarizing database results for a user.
                User Question: ""{originalQuestion}""
                Explanation of query: {explanation}
                Number of records found: {results.Count}
                
                DATA RESULTS:
                {dataJson}

                Provide a friendly, conversational summary. Follow these guidelines:
                - If there's a single record, describe it like: ""Here is the profile for [Name]: [key details]""
                - If there are multiple records, introduce them like: ""I found {results.Count} employees:"" then list them with key attributes (e.g., name, department, grade). Use bullet points if helpful.
                - For salary data, always mention the amount and currency (AED). For example: ""The current monthly salary is 25,000 AED.""
                - If the user asked for a count (e.g., ""how many""), simply state the number.
                - Do not just repeat the JSON. Be concise but informative.
                - If there are no records, say ""No matching records found.""
                - Use a professional yet warm tone.";
            }

            try
            {
                var messages = new List<GroqMessage>
                {
                    new GroqMessage { Role = "system", Content = "You are an HR assistant that explains data in plain, natural language. Never show raw database fields." },
                    new GroqMessage { Role = "user", Content = prompt }
                };

                // Console.WriteLine("[DEBUG] Calling Groq service...");

                var completion = await _groqService.GetChatCompletionAsync(messages, null);
                var response = completion.Choices.FirstOrDefault()?.Message?.Content?.Trim();
                
                // _logger.LogInformation("FormatResultsWithLlmAsync LLM response: {Response}", response ?? "(null)");

                if (!string.IsNullOrWhiteSpace(response))
                    return response;

                // Only use fallback if LLM returns empty
                return FormatSimpleFallback(results, columns, originalQuestion);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LLM formatting failed, using fallback");
                return FormatSimpleFallback(results, columns, originalQuestion);
            }
        }

        private string FormatSimpleFallback(List<Dictionary<string, object>> results, List<string> columns, string question)
        {
            if (results.Count == 0) return "No records found.";
            
            var row = results[0];
            
            // Salary query fallback
            if (row.ContainsKey("BaseSalary") && row.TryGetValue("Currency", out var currency))
            {
                var salary = row["BaseSalary"];
                return $"Your current monthly salary is {currency} {Convert.ToDecimal(salary):N0}.";
            }
            
            // Generic single record fallback
            if (results.Count == 1)
            {
                var parts = columns.Where(c => row.TryGetValue(c, out var v) && v != null)
                                .Select(c => $"{c.Replace("\"", "").Replace("_", " ")}: {row[c]}");
                return $"Here's what I found: {string.Join(", ", parts)}.";
            }
            
            return $"Found {results.Count} records matching your query.";
        }
    }
}
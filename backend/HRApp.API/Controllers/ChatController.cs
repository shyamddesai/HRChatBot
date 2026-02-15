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

            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
                return Unauthorized();

            // Build schema-aware system prompt
            var schemaInfo = GetDatabaseSchema();
            var systemPrompt = $@"You are an intelligent HR Database Assistant with direct SQL generation capabilities.

            DATABASE SCHEMA:
            {schemaInfo}

            SECURITY RULES:
            - You are logged in as: {userEmail} (Role: {userRole})
            - Employee Role: Can only view own data (WHERE id = '{userId}')
            - HR Role: Can view all data, but respect privacy for sensitive fields
            - NEVER generate DELETE or DROP statements
            - NEVER expose passwords or internal IDs
            - For numeric grade comparisons, use the computed column ""GradeNumber"" (e.g., ""GradeNumber"" >= 10).
            - Always use double quotes around table and column names (e.g., SELECT ""FullName"" FROM ""Employees"").
            - Parameter values can be placed directly in the SQL (we handle sanitization), but use proper escaping if needed.

            Loan Guidelines (for reference):
            - If the user asks about loan eligibility, car loans, housing loans, or personal loans, set intent to ""loan_eligibility"", include the loan type in your response, and you can query the ""Loans"" table.
            - Car Loan: Grade 10+, Salary >= 8000 AED, no existing active car loan
            - Housing Loan: Grade 12+, Salary >= 15000 AED, 2+ years tenure, no existing housing loan  
            - Personal Loan: Any active employee, max 1x salary
            - Max loan amounts: Car ~5x salary, Housing ~10x salary
            - For hypotheticals (""what if salary was X"", ""how much do I need""), query current data and explain the calculation versus eligibility thresholds.

            INSTRUCTIONS:
            1. Analyze the user's question
            2. Generate a SAFE, read-only SQL query to answer it
            3. Return ONLY a JSON object with this exact format:
            {{
                ""intent"": ""description of what you're doing"",
                ""sql"": ""SELECT ..."",
                ""explanation"": ""human-readable explanation of the query""
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

                if (string.IsNullOrEmpty(llmResponse))
                    return Ok(new { answer = "I didn't understand that. Could you rephrase?" });

                _logger.LogInformation("LLM Raw Response: {Response}", llmResponse);

                // Parse the JSON response from LLM
                var (intent, sql, explanation, conversationResponse) = ParseLlmResponse(llmResponse);

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

                // Check if this is a loan eligibility query
            //    if (intent.Contains("loan") || !string.IsNullOrEmpty(parsedLoanType))
            //     {
            //         var loanType = !string.IsNullOrEmpty(parsedLoanType) 
            //             ? parsedLoanType 
            //             : ExtractLoanType(request.Message);
                    
            //         var loanService = new LoanService(_context);
                    
            //         // If user asks broadly ("any loan", "what loans"), check all types
            //         if (loanType == "All" || request.Message.ToLower().Contains("any") || request.Message.ToLower().Contains("what loans"))
            //         {
            //             var results = await CheckAllLoans(loanService, userId);
            //             return Ok(new { answer = results, type = "loan_check_all" });
            //         }
                    
            //         var eligibility = await loanService.CheckEligibilityAsync(userId, loanType);
            //         var response = FormatLoanResponse(eligibility, loanType);
            //         return Ok(new { answer = response, type = "loan_check" });
            //     }

                // Validate and execute SQL
                if (!string.IsNullOrEmpty(sql))
                {
                    var validationResult = ValidateSql(sql, userRole!, userId);
                    if (!validationResult.IsValid)
                    {
                        return Ok(new { answer = $"I can't execute that query for security reasons: {validationResult.Reason}", type = "error" });
                    }

                    // Apply row-level security for non-HR users
                    sql = ApplyRowLevelSecurity(sql, userRole!, userId);

                    var (results, columns) = await ExecuteDynamicSqlAsync(sql);
                    
                    // Format results through LLM for natural language
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

        private (string intent, string? sql, string? explanation, string? conversationResponse) ParseLlmResponse(string response)
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

                return (intent, sql, explanation, convResponse);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse LLM response as JSON: {Response}", response);
                // Fallback: treat as conversation
                return ("conversation", null, null, response);
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

            // For employees, inject WHERE clause to limit to own data
            // This is a simplified approach - in production use proper parameterized queries
            var employeeIdStr = userId.ToString();
            
            // Add employee_id filter if querying employees table
            if (sql.Contains("employees", StringComparison.OrdinalIgnoreCase))
            {
                if (sql.Contains("WHERE", StringComparison.OrdinalIgnoreCase))
                    sql = sql.Replace("WHERE", $"WHERE (id = '{employeeIdStr}') AND ", StringComparison.OrdinalIgnoreCase);
                else if (sql.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase))
                    sql = sql.Replace("GROUP BY", $"WHERE id = '{employeeIdStr}' GROUP BY", StringComparison.OrdinalIgnoreCase);
                else if (sql.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase))
                    sql = sql.Replace("ORDER BY", $"WHERE id = '{employeeIdStr}' ORDER BY", StringComparison.OrdinalIgnoreCase);
                else
                    sql += $" WHERE id = '{employeeIdStr}'";
            }

            return sql;
        }

        private async Task<(List<Dictionary<string, object>> Results, List<string> Columns)> ExecuteDynamicSqlAsync(string sql)
        {
            var results = new List<Dictionary<string, object>>();
            var columns = new List<string>();

            try
            {
                string trimmedSql = sql.Trim().TrimEnd(';');
                _logger.LogInformation("Executing SQL: {Sql}", trimmedSql);

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
                
                _logger.LogInformation("Executed SQL: {Sql} | Rows: {Count}", trimmedSql, results.Count);
                
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
            if (results.Count == 0)
                return "I found no matching records for your query.";

            // For small results, check if it's a loan/hypothetical query that needs special formatting
            var isLoanQuery = originalQuestion.ToLower().Contains("loan") || 
                            originalQuestion.ToLower().Contains("eligible") ||
                            originalQuestion.ToLower().Contains("qualify");
            
            var isHypothetical = originalQuestion.ToLower().Contains("if") || 
                                originalQuestion.ToLower().Contains("what if") ||
                                originalQuestion.ToLower().Contains("would") ||
                                originalQuestion.ToLower().Contains("doubled") ||
                                originalQuestion.ToLower().Contains("increase") ||
                                originalQuestion.ToLower().Contains("how much") ||
                                originalQuestion.ToLower().Contains("need");

            // For small results with loan/hypothetical, still use LLM for better explanation
            if (results.Count <= 5 && !isLoanQuery)
            {
                var summary = $"Found {results.Count} result(s):\n\n";
                foreach (var row in results)
                {
                    summary += "• " + string.Join(", ", row.Select(kv => $"{kv.Key}: {kv.Value}")) + "\n";
                }
                return summary;
            }

            var dataJson = JsonSerializer.Serialize(new { columns, rows = results.Take(20) });
            
            string prompt;
            
            if (isLoanQuery && isHypothetical)
            {
                prompt = $@"The user asked a hypothetical loan question: ""{originalQuestion}""

                Their current data from database:
                {dataJson}

                Explain:
                1. Their current situation (grade, salary, existing loans)
                2. Answer their specific hypothetical scenario with calculations
                3. Show the math clearly (e.g., ""5x salary rule: 25,000 × 5 = 125,000 max"")
                4. Be conversational and helpful

                Guidelines:
                - Car loans: max ~5x salary, need Grade 10+, Salary 8000+
                - Housing: max ~10x salary, need Grade 12+, Salary 15000+, 2+ years tenure
                - Personal: max 1x salary, any active employee";
            }
            else if (isLoanQuery)
            {
                prompt = $@"The user asked about loan eligibility: ""{originalQuestion}""

                Their data:
                {dataJson}

                Explain their eligibility clearly:
                - Which requirements they meet or don't meet
                - What max amount they qualify for
                - Any existing loans that might affect eligibility

                Use these rules:
                - Car: Grade 10+, Salary 8000+, max 5x salary (capped at 100k)
                - Housing: Grade 12+, Salary 15000+, 2+ years tenure, max 10x salary (capped at 500k)
                - Personal: Any active employee, max 1x salary";
            }
            else
            {
                prompt = $@"Original question: ""{originalQuestion}""
                Query explanation: {explanation}
                Found {results.Count} records. Here's a sample:

                {dataJson}

                Provide a concise, helpful summary. If salary data is present, note the currency (AED). 
                Be conversational but professional.";
            }

            try
            {
                var completion = await _groqService.GetChatCompletionAsync(
                    "You are an HR assistant explaining loan eligibility and employee data.", 
                    prompt, 
                    null
                );
                return completion.Choices.FirstOrDefault()?.Message?.Content ?? "Data retrieved successfully.";
            }
            catch
            {
                return $"Found {results.Count} records. Sample: {JsonSerializer.Serialize(results.First())}";
            }
        }
    }
}
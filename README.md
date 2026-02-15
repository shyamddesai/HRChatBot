# AI-powered Conversational HR Assistant 
A full‑stack HR management system with an AI‑powered conversational assistant. Built for a technical interview, the application provides separate views for HR administrators and employees, complete employee lifecycle management, and a Groq‑based chatbot that understands natural language, executes safe SQL, and performs actions like hiring, promotion, and certificate generation.

---

## Key Highlights
- **Full‑stack implementation** – React frontend, ASP.NET Core Web API backend, PostgreSQL database.
- **JWT authentication** with role‑based access (HR / Employee).
- **AI‑powered conversational HR assistant** using Groq:
  - Structured intent detection (query, action, document).
  - Persistent chat history for contextual follow‑ups.
  - Guardrails and SQL validation for safe data access.
  - Multi‑turn workflows (hire, promote, generate certificate).
  - SQL audit logging with user traceability.
- **Employee management**: create, edit, promote, archive/restore, and view salary history.
- **Salary certificate PDF generation** – professional letterhead template with dynamic data.

---

## Technologies Used
| Layer          | Technology                                      |
|----------------|-------------------------------------------------|
| Frontend       | React, TypeScript, TanStack Query, Tailwind CSS |
| Backend        | ASP.NET Core 8 Web API, Entity Framework Core   |
| Database       | PostgreSQL with pgvector extension               |
| Authentication | JWT (custom, no ASP.NET Identity)                |
| AI             | Groq API (LLM‑powered intent & SQL generation)   |
| PDF Generation | QuestPDF                                         |
| Logging        | Serilog (console + file)                         |

---

## Features
### Core HR Management
- **Employee CRUD** – Create, read, update, and deactivate (soft‑delete) employees.
- **Promotion** – Dedicated action with optional salary update.
- **Restore** – Reactivate archived employees (hire date and optionally salary is updated).
- **Salary History** – View all salary records per employee.
- **Salary Certificate** – Generate a professional PDF certificate (template-based).
- **Role‑based Views** – HR sees all employees; employees see only their own profile.

### AI Chatbot
- **Natural Language Queries** – Ask about employees, salaries, departments, loans, etc.
- **Dynamic SQL Generation** – LLM generates safe, read‑only SQL based on the database schema.
- **Action Intents** – HR can create/promote employees or generate certificates conversationally (multi‑turn workflows).
- **SQL Validation** – Queries are validated for safety before execution (blocks dangerous keywords, ensures SELECT only).
- **Audit Logging** – All executed SQL is logged to file with user ID and timestamp.

### Additional Features
- **Loan Eligibility Check** – Simple business rules evaluate car/housing/personal loan eligibility.
- **Search & Filters** – Employee table supports search, department filter, and sorting.
- **Secure Authentication** – JWT with role claims; tokens expire after configurable time.
- **RAG Ready** – `Documents` table with `vector(768)` column (pgvector) for future policy search.

---

## Getting Started
### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/)
- [PostgreSQL](https://www.postgresql.org/) with pgvector extension (or use the provided Docker image)
- [Groq API Key](https://console.groq.com/) (for chatbot functionality)

### Backend Setup
1. Clone the repository:
   ```bash
   git clone https://github.com/shyamddesai/HRChatBot.git
   cd HRChatBot/backend
   ```

2. Configure `appsettings.json`:
   - Set your PostgreSQL connection string.
   - Add your Groq API key under `Groq:ApiKey`.
   - Adjust JWT settings (`Key`, `Issuer`, `Audience`, `ExpireMinutes`).

3. Apply database migrations:
   ```bash
   dotnet ef database update --project HRApp.Infrastructure --startup-project HRApp.API
   ```

4. Run the backend:
   ```bash
   dotnet run --project HRApp.API
   ```
   The API will be available at `http://localhost:5049`.

### Frontend Setup
1. In a new terminal, navigate to the frontend folder:
   ```bash
   cd HRChatBot/frontend
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Start the development server:
   ```bash
   npm run dev
   ```
   The app will open at `http://localhost:5173`.

### Database Seeding
The backend seeds initial data automatically on first run (see `DbInitializer.cs`). Default users:
- HR Admin: `admin@hr.com` / `admin@hr.com`
- Employee: `john.doe@company.com` / `john.doe@company.com`
- Employee: `jane.smith@company.com` / `jane.smith@company.com`

---

## Configuration
### JWT
In `appsettings.json`:
```json
"Jwt": {
  "Key": "your-256-bit-secret-key",
  "Issuer": "HRApp",
  "Audience": "HRAppClient",
  "ExpireMinutes": 120
}
```

### Groq
```json
"Groq": {
  "ApiKey": "your-groq-api-key"
}
```

### PostgreSQL with pgvector
The easiest way to get a PostgreSQL instance with pgvector is Docker:
```bash
docker run --name postgres-hr -e POSTGRES_PASSWORD=password -e POSTGRES_DB=HRAppDb -p 5432:5432 -d pgvector/pgvector:pg15
```

---

## Architecture Overview
- **Backend** follows a clean architecture with separate projects for API, Core (entities), and Infrastructure (DbContext, migrations, seeding).
- **Chatbot** uses a single `POST /api/chat` endpoint. The LLM receives a system prompt with the database schema, security rules, and examples. It returns a JSON structure containing either an SQL query (for data requests) or action parameters (for creates/promotions/certificates). The backend validates the SQL, executes it, and optionally formats the results with a second LLM call for natural language.
- **Security** is layered:
  - The system prompt instructs the LLM to include the user’s ID in `WHERE` clauses for employee‑level queries.
  - All SQL is validated against a blacklist of dangerous keywords and checked for comment sequences or multiple statements.
  - A read‑only database user is recommended for production.
- **Audit logging** uses Serilog to write all executed SQL to daily‑rotated files, including the user ID and timestamp.

---

## Security Considerations
- **SQL Injection**: The validation layer (`ValidateSql`) blocks non‑`SELECT` statements, dangerous keywords, and comment sequences. Parameter values are placed directly in the SQL by the LLM – we rely on the validation as a second line of defence.
- **Row‑level Security**: The system prompt instructs the LLM to add `WHERE` clauses for employees. This is reinforced by the validation layer (which could be extended to parse and enforce such rules).
- **JWT**: Tokens are signed with a symmetric key; expiration is enforced.
- **Production Hardening**: Use a dedicated database user with minimal privileges, enable HTTPS, and consider rate‑limiting the chat endpoint.

---

## License
This project was created for a technical interview and is not intended for production use without further review. No license is implied.

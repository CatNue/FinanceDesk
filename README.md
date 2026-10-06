# FinanceDesk - Billing & Payments System
ASP.NET Core MVC (.NET 8) + C# + Entity Framework Core + MySQL

## Quick start
1. Install the **.NET 8 SDK** and start **MySQL** (MySQL Server 8, or XAMPP/MariaDB).
2. Unzip, then double-click **Run.bat** (or run `dotnet run` inside the project folder).
3. The browser opens automatically. The database `financedesk`, all tables and the starter
   data are created on first run - no SQL to import.

Default logins: `admin` / `Admin@123`  and  `cashier` / `Cashier@123`  (change them after first login).

## Database settings
Edit `ConnectionStrings:DefaultConnection` in `appsettings.json` if your MySQL user/password differ
(default: user `root`, password as shown in that file). To avoid keeping the password in the file you can
instead set the environment variable `ConnectionStrings__DefaultConnection`.

## Password reset
"Forgot password" emails a reset link, so configure the `Smtp` section in `appsettings.json`.
The link is only shown on screen in the Development environment.
Without email, an administrator can reset any account under Users -> Reset password.

## Suggested demo flow
Students -> add a few students | Invoices -> New / Batch Generate |
Payments -> Record Payment (try partial, over-payment, auto-match) |
Receipts -> reprint / download | Overdue -> late fees + reminders | Reports -> Excel export

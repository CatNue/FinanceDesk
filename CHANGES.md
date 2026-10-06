# Bug-fix log

## Security
- Forgot-password: the reset link was displayed on screen to anyone when SMTP was not configured (account takeover by typing a username). Now shown in Development only.
- Deactivated, deleted or re-roled users kept their access until the login cookie expired (8 h, or 30 days with Remember me). The cookie is now re-validated against the database on every request (Program.cs).
- Failed login with a username longer than 60 chars crashed with a database error (audit log column overflow). Now truncated.
- Malformed stored password hash threw an exception at login; now treated as a failed login.
- Emails are now unique across accounts (forgot-password picked an arbitrary account when two shared an email).

## Billing logic
- Invoice numbers used 6 hex chars; large batch generation could hit the unique index and fail the whole batch. Now 12.
- Editing an invoice whose discount had since been deactivated silently removed the discount. The current discount stays selectable.
- Creating an invoice / batch with a missing or inactive fee caused a null-reference error. Now a validation message.
- Batch generation with no matching students now says so instead of "0 invoice(s) generated".
- Editing an invoice no longer accepts an amount of 0 (it would show as "Paid").
- Reminder channel is validated (anything other than Email/SMS was treated as SMS).
- Downloading a receipt no longer increments the "Times Printed" counter.
- Discount type must be Percentage or Fixed.
- Export Collections no longer breaks when dates are missing; reversed date ranges are swapped.
- Student page: Total Billed / Total Paid included archived invoices while Balance did not. All three now exclude archived invoices.

## UI / permissions
- Cashiers saw Edit / Archive / Delete / Resolve / Batch buttons that only led to "Access denied". Hidden for non-admins.
- Cashiers saw Amount / Discount fields on the invoice form that were ignored. Replaced with a note.
- Updating your profile reset a "Remember me" login to a session cookie. Cookie settings are now preserved.
- Student create/edit: Id is no longer bindable on create; student number is trimmed.

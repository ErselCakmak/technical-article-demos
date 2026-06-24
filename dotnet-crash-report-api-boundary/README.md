# .NET Crash Report API Boundary Demo

Public-safe demo for the article:

https://erselcakmak.com/articles/why-i-moved-mail-sending-behind-an-api

The goal is to keep SMTP and infrastructure details out of released desktop clients.
The desktop app submits a report payload to a stable API endpoint, while the server owns
mail delivery, credentials, retries, and future provider changes.

This repository uses placeholder URLs and keys only.

## Run

```bash
dotnet run --project src/CrashReportBoundaryDemo
```

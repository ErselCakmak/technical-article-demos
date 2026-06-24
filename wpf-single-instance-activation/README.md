# WPF Single Instance Activation Demo

Public-safe demo for the article:

https://erselcakmak.com/articles/making-single-instance-activation-reliable-in-a-wpf-app

This sample shows the shape of a resilient single-instance handoff:

- A mutex decides which process owns the main instance.
- A named pipe is used as the fast path for forwarding activation arguments.
- A small file queue is used as a fallback when pipe delivery fails.

It is intentionally small and does not contain production application code.

## Run

```bash
dotnet run --project src/SingleInstanceActivationDemo
```

Open a second terminal and run the same command with an argument:

```bash
dotnet run --project src/SingleInstanceActivationDemo -- "sample.project"
```

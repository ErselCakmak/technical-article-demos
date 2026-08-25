# Graph-Aware Clipboard — Architecture Tests

Public-safe companion project for:

https://erselcakmak.com/articles/copy-paste-is-a-graph-problem

This project is an independent teaching model written with neutral canvas objects.
It does not contain production source code, product types, internal schemas, customer
data, or company-specific business rules.

## What it demonstrates

- Versioned polymorphic clipboard payloads
- A complete old-to-new identity map built before graph mutation
- Internal references remapped to pasted nodes
- Valid external references preserved in the destination
- Missing external references cleared instead of left dangling
- Compound geometry translated together
- Source objects left unchanged by the copy/paste round trip

## Run

```bash
dotnet test tests/GraphAwareClipboard.Tests/GraphAwareClipboard.Tests.csproj
```

## Project layout

```text
src/GraphAwareClipboard/                 Generic clipboard graph model
tests/GraphAwareClipboard.Tests/         Focused architecture tests
```

The sample is intentionally small and designed for learning, not direct production use.

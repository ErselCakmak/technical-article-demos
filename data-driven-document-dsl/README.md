# Data-Driven Document DSL — Architecture Tests

Public-safe companion project for the architecture lesson:

https://erselcakmak.com/articles/a-small-dsl-for-data-driven-document-generation

The core architecture and original design discussed in the article are credited to
[Mustafa Şentürk](https://www.linkedin.com/in/mustafa-senturk-ub9901/).

This repository is an independent teaching model. It was written from scratch with
neutral document concepts and contains no production source code, product command
names, internal schemas, customer data, or company-specific business rules.

## What the tests demonstrate

- A compact DSL can compile blocks into replayable instructions.
- Each iteration opens a fresh value scope.
- Inner values can shadow an alias without destroying the outer value.
- Nested blocks produce a deterministic Cartesian product.
- The evaluator writes through a replaceable document target, so a planning target
  can validate the same script without mutating a real document.

The deliberately small teaching language supports only two instructions:

```text
for region in North South
  for view in Summary Detail
    page "<region> - <view>"
  end
end
```

## Run the tests

```bash
dotnet test tests/DocumentDslLesson.Tests/DocumentDslLesson.Tests.csproj
```

## Project layout

```text
src/DocumentDslLesson/                 Generic compiler and evaluator
tests/DocumentDslLesson.Tests/         Architecture behavior tests
```

The sample is designed for learning and discussion, not direct production use.

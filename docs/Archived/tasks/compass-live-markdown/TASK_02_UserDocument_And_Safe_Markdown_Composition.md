# TASK 02 — Add UserDocument and Safe Markdown Composition

## Goal

Create an Application-layer document abstraction so user-facing prose and Markdown never leak into Core/domain code.

## UserDocument

Add an immutable presentation model equivalent to:

```csharp
public sealed record UserDocument(
    string Markdown,
    string? Title = null);
```

Keep it in an Application/presentation-oriented assembly, not Core.

## Required dependency direction

```text
structured domain state
    ↓
Application document factory
    ↓
UserDocument
    ↓
ViewModel
    ↓
InformationDocumentView
```

Forbidden:

```text
Core -> Markdown
Firmware domain -> Markdown
MAVLink decoder -> Markdown
```

## Safe Markdown builder

Introduce a small MissionPlanner-owned composition helper.

It should make these operations convenient:

```text
Heading
Paragraph
Bullet
Inline code
Simple table
Note/warning block
Escaped dynamic text
```

Do not create a generic rich-document framework.

## Escaping

Dynamic values may contain Markdown syntax.

Escape values from:

```text
STATUSTEXT
device names
firmware names
parameter descriptions
parameter values represented as text
exceptions
vehicle/user supplied labels
```

Handle at minimum:

```text
*
_
`
[
]
#
|
\
```

and multiline text.

Parameter names and raw values should normally use inline-code formatting.

## No raw HTML requirement

MissionPlanner-generated documents should not require embedded HTML.

If LiveMarkdown supports raw HTML, generated documents must not depend on it.

## Tests

Add deterministic tests for:

- escaping;
- headings;
- table generation;
- code values;
- multiline dynamic text;
- empty/null optional data.

## Acceptance criteria

- Core remains presentation-neutral.
- Application-layer document generation is safe and testable.
- ViewModels do not build large Markdown strings directly.

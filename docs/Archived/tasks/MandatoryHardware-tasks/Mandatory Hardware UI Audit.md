# Mandatory Hardware UI Audit

The complete eleven-tab Mandatory Hardware workspace was checked for visual and
interaction consistency.

- All tabs use the existing `ExtendedTabView`, `SlimTabHeaderView`, start-side
  placement, status projection, and lifecycle content.
- The four new pages use XAML and code-behind, scroll vertically, and share the
  established progress, title, italic status, error, surface-card, and action
  patterns.
- Parameter pages present metadata options with `SelectField`, numeric values
  with `TextField`, and an explicit Apply button. Diagnostic HW ID uses read-only
  rows and a Refresh action.
- Safety guidance is visually distinguished without introducing hard-coded
  light-theme surfaces; existing semantic surface styles support both themes.
- New page content uses a small responsive edge margin and a maximum desktop
  width, avoiding the prior fixed 100-pixel margins on narrow windows while
  keeping long desktop rows readable.
- Empty, disconnected, loading, success, validation, and failure states remain
  visible in the same content region and do not change tab/header alignment.


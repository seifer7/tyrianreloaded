# Repository guidance for coding agents

## Current task boundary

This branch is for a full C# conversion, but its initial setup explicitly does
**not** authorize converting game code. For setup-only work, edit only the
requested planning/tracking files. Start implementation only when a later user
request authorizes the conversion.

## Before conversion work

1. Read `README.md`, `CSHARP_CONVERSION_WORKFLOW.md`, and
   `CONVERSION_PROGRESS.md`.
2. Inspect the relevant C implementation and its callers, declarations,
   build configuration, and tests before translating behavior.
3. Preserve the C version as the compatibility reference until the relevant
   C# behavior is implemented and verified.
4. Add every new source, test, project, and build file to the progress
   inventory. Update the inventory if files or functions are added, removed,
   split, merged, or intentionally left native.

## Conversion standards

- Port behavior, not syntax. Preserve observable gameplay, input, rendering,
  audio, file formats, command-line options, save data, network behavior, and
  error reporting.
- Use clear, idiomatic C# types and ownership. Do not imitate unsafe C pointer
  patterns when managed representations can preserve the same behavior.
- Take care with fixed-width arithmetic, signedness, overflow, array bounds,
  bit operations, timing, binary serialization, and any behavior that relied
  on C integer promotions or memory layout.
- Keep game logic separate from SDL/platform integration where practical.
  Preserve the SDL 2 capabilities of the existing builds; do not choose or
  introduce a replacement framework without documenting its compatibility
  impact.
- Keep the data extractor's formats, ordering, validation, and command-line
  behavior compatible with the existing tool.
- Do not add original Tyrian game assets or other unlicensed runtime data.
- Avoid unrelated cleanup or behavior changes during a port.

## Progress and verification

- A function is converted only after its C# implementation is integrated,
  relevant tests or equivalent checks pass, and compatibility has been
  reviewed. A mechanical translation alone is not completion.
- Mark a file complete only after it has a verified C# counterpart, has been
  explicitly replaced, or has a documented and verified reason to remain
  unchanged. Do not mark files complete merely because they were inspected.
- Update `CONVERSION_PROGRESS.md` in the same change as the conversion. Keep
  function checkboxes, file dispositions, and summary counts consistent.
- Validate the narrowest relevant tests while iterating, then run the full
  C# build and test suite at conversion milestones and before declaring the
  port complete.
- Do not remove the native baseline or its build support until the final
  compatibility and release gates in `CSHARP_CONVERSION_WORKFLOW.md` pass.

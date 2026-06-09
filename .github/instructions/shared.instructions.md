---
applyTo: "DocQA.Shared/**"
---

# Shared DTO Conventions

- DTOs are plain C# records or classes — no business logic, no EF attributes
- Use `required` properties or constructor parameters; avoid nullable reference types on required fields
- Naming: `{Entity}Dto` for read models, `{Verb}{Entity}Request` for write inputs, `{Verb}{Entity}Response` for write outputs
- All IDs are `int`; all timestamps are `DateTime` (UTC)

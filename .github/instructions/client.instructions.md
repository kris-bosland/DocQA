---
applyTo: "DocQA.Client/**"
---

# Client-Side Conventions

## Blazor WASM
- All UI is C# — no JavaScript interop unless absolutely unavoidable
- Pages go in `Pages/`, shared components in `Shared/`
- Use `@inject ApiClient ApiClient` — never call `HttpClient` directly from a component

## ApiClient
- `ApiClient` (`Services/ApiClient.cs`) is the single typed wrapper around `HttpClient`
- Add a method to `ApiClient` for every new API endpoint rather than adding raw HTTP calls in components

## State & lifecycle
- Use `OnInitializedAsync` for data loading; show a loading indicator while awaiting
- Use `StateHasChanged()` only when updating state from outside the render cycle (e.g., callbacks)

## Navigation
- Navigate with `NavigationManager.NavigateTo()`; use relative paths

## Page contracts

**`Index.razor` (`/`)**
- Table columns: FileName, UploadedAt, FileSizeBytes, [Open] [Delete] buttons
- File input must use `accept=".txt,.pdf"`
- On upload success: add document to list and clear the file input

**`DocumentChat.razor` (`/document/{Id:int}`)**
- Route parameter: `[Parameter] public int Id { get; set; }`
- Append the user's message to the UI immediately on submit (optimistic); append the assistant reply when the response arrives
- Empty state message: "Ask a question about this document to get started."

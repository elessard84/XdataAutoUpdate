# AGENTS.md — Civil 3D 2026 Development Rules

## Purpose

This repository targets Autodesk AutoCAD 2026 / Civil 3D 2026.

The coding agent must prioritize:

1. Autodesk API correctness
2. User-requested scope
3. Compilation correctness
4. Minimal, maintainable implementation
5. Verification over plausibility

Never invent Autodesk APIs.

---

## Target Environment

Use these defaults unless the repository or user explicitly requires something else:

- AutoCAD 2026
- Civil 3D 2026
- C#
- Visual Studio 2022 or newer
- Windows 11 x64
- .NET 10
- Target Framework: `net10.0-windows`
- Platform: x64
- Deployment: NETLOAD and/or Autodesk AutoLoader bundle
- Excel workflows: ClosedXML / XLSX when explicitly required

Do not silently downgrade the target framework.

If the existing project targets another framework, inspect the project before changing it and explain the discrepancy.

---

## Local Civil 3D 2026 API Documentation

The local Civil 3D 2026 .NET API documentation is available at:

`D:\RAG\Civil3D_API_2026\civapidocs_named\`

This documentation is the primary source for Civil 3D-specific API verification.

There are also original GUID-named source files at:

`D:\RAG\Civil3D_API_2026\civapidocs.com\`

Prefer `civapidocs_named` for normal work.

Use the original folder only if required to resolve a missing or ambiguous renamed document.

---

## Autodesk API Verification Policy

Before using a Civil 3D-specific API member in new code, verify it in the local documentation.

Do not rely on model memory for Civil 3D API signatures.

Verify as applicable:

- exact class/type
- namespace
- declaring type
- method/property/constructor name
- exact C# signature
- parameter names and types
- parameter order
- return type
- static vs instance
- overload
- collection element type
- inheritance when required to justify a member

A plausible Autodesk API name is not evidence that it exists.

A member from another Civil 3D version is not automatically valid for Civil 3D 2026.

---

## Documentation Search Strategy

Prefer the following order:

### 1. Search by readable filename

```powershell
Get-ChildItem "D:\RAG\Civil3D_API_2026\civapidocs_named" `
  -Filter "*PointDescriptionKeySetCollection*Add*" `
  -File
```

### 2. Search the documentation index

If `index.csv` exists:

```powershell
Import-Csv "D:\RAG\Civil3D_API_2026\civapidocs_named\index.csv" |
    Where-Object {
        $_.Title -like "*PointDescriptionKeySetCollection.Add*"
    }
```

### 3. Search HTML contents

When the filename is not enough:

```powershell
Get-ChildItem "D:\RAG\Civil3D_API_2026\civapidocs_named" `
    -Filter "*.htm" `
    -File `
    -Recurse |
    Select-String -Pattern "PointDescriptionKeySetCollection\.Add" |
    Select-Object -ExpandProperty Path -Unique
```

### 4. Read the exact member page

Once a candidate member page is found, inspect that specific file before using the member in code.

---

## Exact-Member Rule

A Class, Members, Methods, Properties, overload-list, guide, or sample page may identify a useful API member, but it does not automatically prove the complete signature.

If a specific member is used in code, prefer its member-specific API reference page.

Do not infer a full signature from a summary page when an exact member page is available.

---

## Neutral Search Rule

When verifying an unknown fact, do not put the predicted answer into the search query.

Bad:

`GridSurface ExtractContoursAt return ObjectIdCollection`

Good:

`GridSurface ExtractContoursAt Double ContourSmoothingType Int32`

Search to discover the answer, not to confirm a guess.

---

## High-Risk Civil 3D API Areas

Use extra verification for:

- Description Keys
- Pressure Networks
- Pipe Networks
- Styles and style collections
- Corridors
- Surfaces
- Labels
- Profile/ProfileView APIs
- Alignment collections
- object creation
- `Add(...)`
- `Create(...)`
- constructors
- collection enumeration
- indexers
- inherited members
- APIs returning `ObjectId` or `ObjectIdCollection`

Never transfer members between similar-looking types.

Examples of dangerous assumptions:

- `PipeNetwork` vs `PressurePipeNetwork`
- `Pipe` vs `PressurePipe`
- `PointDescriptionKeySet` vs `PointDescriptionKeySetCollection`
- `Profile` vs `ProfileView`
- `Style` vs `StyleCollection`

---

## Trusted AutoCAD Foundation

The following basic AutoCAD patterns may be used without repeatedly searching the Civil 3D documentation unless the user explicitly asks for strict retrieval-only verification:

- `Application.DocumentManager.MdiActiveDocument`
- `Document.Database`
- `Document.Editor`
- `Database.TransactionManager`
- `TransactionManager.StartTransaction()`
- `Transaction.GetObject(ObjectId, OpenMode)`
- `Transaction.Commit()`
- `Transaction.Abort()`
- `OpenMode.ForRead`
- `OpenMode.ForWrite`
- `ObjectId`
- `ObjectIdCollection`
- `DBObject`
- `CommandMethodAttribute`
- `Editor.WriteMessage(...)`
- `HostApplicationServices.WorkingDatabase`

Do not extend this list by analogy.

Civil 3D-specific APIs are not covered by this trusted foundation.

---

## Object Creation Rule

Never assume an Autodesk constructor exists.

Before writing:

```csharp
new SomeAutodeskType(...)
```

verify the exact constructor.

Before using:

```csharp
Add(...)
Create(...)
```

verify the exact member-specific signature.

If a creation method returns `ObjectId`, verify that return type before using it.

If the returned object must be opened in a transaction, use the verified object type.

---

## Collection Rule

Never assume collection behavior.

Before iterating a Civil 3D collection, verify:

- whether it is enumerable
- its element type
- whether elements are `ObjectId`
- whether an indexer returns an object or an ID
- whether a separate `Get...Ids()` method is required

Do not infer `IEnumerable<ObjectId>` from `Count`, `Item[]`, or collection naming.

---

## Inheritance Rule

Do not use an inherited member unless the relevant inheritance chain is known or verified.

This is especially important for members such as:

- `Name`
- `Description`
- `ObjectId`
- style-related members
- entity properties

If a member comes from a base class, verify that the target type actually inherits from that base class.

---

## Scope Discipline

Do exactly what the user asks.

Do not turn:

- an API verification request into a full plugin
- a coding question into an Excel exporter
- a single-file edit into a project rewrite
- a compiler fix into a refactor
- a documentation lookup into unrelated implementation

If the user asks for only one file or method, modify only that scope unless a required dependency makes a broader change unavoidable.

Explain any unavoidable broader change before making it.

---

## Repository First

Before changing code:

1. Inspect the repository structure.
2. Inspect the existing `.csproj`.
3. Inspect existing working patterns.
4. Reuse working Autodesk API calls already present in the repository when appropriate.
5. Avoid replacing working code solely because another style appears cleaner.

Existing code that compiles and works in Civil 3D 2026 is strong local evidence.

---

## Build Discipline

After meaningful code changes, build the project.

Preferred command:

```powershell
dotnet build
```

If the solution or project requires a specific command, use the repository's actual build command.

When a build fails:

1. read the exact compiler error
2. identify the failing file and line
3. fix one root cause at a time
4. rebuild
5. do not replace a disproved Autodesk API with another guessed API

Compiler errors are evidence that the attempted code is invalid.

They are not proof of what the replacement API should be.

Search the documentation before replacing uncertain Autodesk APIs.

---

## Compilation Checks

Before considering work complete, inspect for:

- undefined variables
- missing `using` directives
- ambiguous type names
- incorrect Autodesk namespaces
- invalid constructors
- invalid overloads
- incorrect generic types
- wrong collection element types
- missing assembly references
- inaccessible members
- incorrect return types

Do not claim code is guaranteed to compile unless a build actually succeeded.

---

## Namespace Conflicts

Use fully qualified names when needed.

Common examples:

```csharp
System.Exception
Autodesk.AutoCAD.DatabaseServices.DBObject
Autodesk.AutoCAD.DatabaseServices.Transaction
Autodesk.AutoCAD.ApplicationServices.Application
```

Avoid ambiguous `DBObject` references when both AutoCAD and Civil namespaces are imported.

---

## Project File Rules

For Civil 3D 2026 projects, preserve working Autodesk assembly references from the repository.

Typical references may include:

- `accoremgd.dll`
- `acdbmgd.dll`
- `acmgd.dll`
- `AecBaseMgd.dll`
- `AeccDbMgd.dll`

Do not invent Autodesk NuGet package names.

Autodesk managed references should normally use:

```xml
<Private>False</Private>
```

Default target when creating a new Civil 3D 2026 project:

```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows</TargetFramework>
  <PlatformTarget>x64</PlatformTarget>
  <Platforms>x64</Platforms>
  <ImplicitUsings>disable</ImplicitUsings>
  <Nullable>disable</Nullable>
</PropertyGroup>
```

Only enable WPF when WPF is actually required.

---

## Excel Rules

Only introduce Excel/ClosedXML if the user asks for Excel functionality.

Do not turn unrelated Civil 3D tasks into spreadsheet workflows.

When Excel is required:

- use XLSX unless told otherwise
- default to ClosedXML
- validate worksheet names
- validate required columns
- handle blank values
- validate numeric values
- report invalid rows
- use normal disposal patterns

Do not call nonexistent APIs such as `XLWorkbook.Close()`.

---

## PackageContents.xml

Generate Autodesk AutoLoader packaging only when requested or required by the task.

Default company metadata when needed:

```xml
<CompanyDetails
  Name="Etienne Lessard"
  Url="https://www.lessard.xyz"
  Email="support@lessard.xyz" />
```

Default author:

`Etienne Lessard`

Do not leave placeholder GUIDs.

Do not invent package attributes or runtime identifiers.

Preserve existing working package configuration when modifying an existing app.

---

## Git and GitHub Safety

OpenCode may inspect and use Git normally.

Before major edits:

```powershell
git status
```

Review existing uncommitted user changes before editing.

Never discard or overwrite unrelated user changes.

Prefer reviewing:

```powershell
git diff
```

before and after substantial edits.

Do not:

- force-push
- reset hard
- delete branches
- rewrite history
- merge to protected branches
- publish releases
- create or push commits

unless the user explicitly requests the corresponding action.

If asked to commit, use a concise commit message describing the actual change.

If GitHub CLI (`gh`) is available, it may be used for repository, issue, and pull-request workflows when requested.

Do not create or modify GitHub issues, pull requests, releases, or remote branches without explicit user instruction.

---

## Error Handling

At command boundaries, use appropriate exception handling.

When namespace ambiguity exists, prefer:

```csharp
catch (System.Exception ex)
```

Do not silently swallow exceptions.

Report useful errors through the AutoCAD editor where appropriate.

Do not use exception handling to hide invalid API usage.

---

## User-Provided Working Code

If the user says an API call already compiles and works in Civil 3D 2026:

- preserve it unless a change is necessary
- do not "correct" it from memory
- do not replace it solely because another pattern looks more elegant

If a change is necessary, verify the replacement first.

---

## Verification Output

When API certainty matters, distinguish:

### VERIFIED
Supported by the local Civil 3D 2026 documentation.

### TRUSTED LOCAL
Supported by existing working project code or the trusted AutoCAD foundation.

### UNVERIFIED
Not supported strongly enough to use safely.

If a required Civil 3D API remains unverified, do not invent code for that part.

---

## Final Audit Before Completion

Before finishing a Civil 3D coding task, check:

### Scope
- Did I do only what was requested?
- Did I introduce unrelated functionality?

### API
- Are all new Civil 3D-specific members verified?
- Did I verify exact signatures for creation and collection APIs?
- Did I assume inheritance?
- Did I assume a return type?
- Did I transfer a member from a similar class?

### Code
- Does the code build?
- Are namespaces/imports correct?
- Are existing working patterns preserved?
- Are there any placeholders or fake implementations?

### Repository
- Did I preserve unrelated user changes?
- Did I inspect `git diff`?
- Did I avoid destructive Git operations?

---

## Core Principle

For Autodesk development:

**Evidence beats plausibility.**

Use this workflow:

```text
UNDERSTAND SCOPE
→ INSPECT REPOSITORY
→ IDENTIFY REQUIRED CIVIL 3D API
→ SEARCH LOCAL DOCS
→ VERIFY EXACT MEMBER
→ IMPLEMENT
→ BUILD
→ FIX VERIFIED ERRORS
→ REVIEW DIFF
```

Never:

```text
GUESS API
→ WRITE PLAUSIBLE CODE
→ HOPE IT COMPILES
```

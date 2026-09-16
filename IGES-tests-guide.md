# IGES import and export tests — how they work, and how to add one

**Scope:** the IGES tests in the `FileIO` project. Implementation lives in
`FileIO\IGES\IgesImportBase.cs` and `FileIO\IGES\IgesExportBase.cs`; the folder-to-fixture mapping
is in `FileIO\Rhino.Testing.Configs.xml`.

The IGES suites are the STEP suites' pattern applied to a second format, and everything the
`STEP-tests-guide.md` says about the shared machinery holds here unchanged: the sidecar oracle
format and its present-keys-only assertion, the measured keys (`units` through `bbox`), exact
count comparison with `max(|expected| × 1e-8, 1e-6)` slack on the mass properties and bounding
box, the `#` / `!` / `bak` file name conventions, the `[Explicit]` per-fixture `Regenerate`
baseline author, the future/large folder semantics, and the `Optional`
`private_models\models\IGESfile*\` second locations. This file covers only what is IGES-specific.

## The fixtures

| Fixture | Runs by default? | Folder |
| --- | --- | --- |
| `IgesImport` | yes | `models\IGESfile\` |
| `IgesExport` | yes | `models\IGESfile\` + `models\IGESfile-export\` |
| `IgesImportFuture` | no, `[Explicit]` | `models\IGESfile-future\` |
| `IgesImportLarge` | no, `[Explicit]` | `models\IGESfile-large\` (git-ignored apart from baselines) |
| `IgesExportFuture` | no, `[Explicit]` | `models\IGESfile-export-future\` |
| `IgesExportLarge` | no, `[Explicit]` | `models\IGESfile-large\` |

Model extensions: `.igs`, `.iges` (plus `.3dm` in the export folders). Sidecars:
`<model>.expected.txt` for import, `<model>.exported.txt` for export.

## Environment variables

The exact set the STEP suites have, on the IGES namespaces, so regenerating one format never
quietly rewrites another:

| Import | Export |
| --- | --- |
| `MX_IGES_REGEN` | `MX_IGESEXPORT_REGEN` |
| `MX_IGES_REGEN_FIELDS` | `MX_IGESEXPORT_REGEN_FIELDS` |
| `MX_IGES_REGEN_DRYRUN` | `MX_IGESEXPORT_REGEN_DRYRUN` |
| `MX_IGES_REGEN_LOG` | `MX_IGESEXPORT_REGEN_LOG` |
| `MX_IGES_LOG` | `MX_IGESEXPORT_LOG` |
| — | `MX_IGESEXPORT_KEEP` |
| — | `MX_IGESEXPORT_REQUIRE_BASELINE` |

Comparison slack: `MX_IGES_RELTOL` / `MX_IGES_ABSTOL`, shared by import and export like STEP's.

## IGES-specific behaviour worth knowing

**The reader has no options API.** RhinoCommon exposes the IGES writer (`FileIgs.Write`) but not
the reader, so import goes through `RhinoDoc.Import()` into the same pinned millimetre / 0.001
document the STEP suite uses, and runs with the reader's defaults. Those defaults are part of what
the baselines pin.

**IGES arrives as loose faces.** Rhino's IGES reader does not join trimmed surfaces into
polysurfaces - a six-face box imports as six one-face breps with `solids 0`. This is Rhino's
long-standing IGES behaviour, not a suite artifact, and the baselines record it. Expect `breps` to
count faces, and `solids` to be 0 for most models.

**The structural checks** (what a model with no export sidecar gets, the counterpart of STEP's
Part 21 checks) come from the fixed-format layout of every ASCII IGES file:

- every record is exactly 80 characters;
- column 73 holds the section letter and the sections run S, G, D, P, T in order;
- each section's sequence numbers (columns 74-80) count up from 1 without gaps;
- Directory Entry records come in pairs;
- the single Terminate record's four counts (`S… G… D… P…`) match the sections actually written.

A truncated or interleaved write fails these before any geometry is compared. On top of them the
round trip asserts the file re-imports into at least one object.

**⚠ The write options in the sidecar are requests, not yet effects (RH-98710).** `FileIgs.Write` currently
ignores its `FileIgsWriteOptions` argument: the native writer builds its options from the
plugin's current options and never reads the Export dictionary (only units and tolerance follow a
headless document). The sidecar option keys - `igesversion`, `surfacetype`, `solidtype`,
`meshtype`, `splitclosedsurfaces`, `simplifycurves` - are parsed, pinned and written into every
baseline anyway, so the day the plumbing is fixed, every baseline that pins a non-default option
diffs loudly instead of drifting silently. Until then, all committed export baselines record the
writer's default behaviour (which is also what a headless export genuinely does). The corpus
deliberately contains no model whose name claims a non-default write mode.

## The corpus

Every committed model in `models\IGESfile\` is *generated* - written by Rhino's own IGES writer
from geometry stated in `FileIO\IGES\AuthorIgesCorpus.cs`. That makes the corpus unencumbered,
reviewable as code, and reproducible; the trade-off is that it exercises the reader only on
Rhino's own IGES dialect. Files from other originators (CATIA, NX, SolidWorks) belong in the
corpus too as they become available with clean redistribution rights - or in
`private_models\models\IGESfile\` when they cannot be published.

To re-author the corpus (only after changing `AuthorIgesCorpus.cs`):

```bash
MX_IGES_AUTHOR_OUT_DIR=models dotnet test --filter "FullyQualifiedName~AuthorIgesCorpus"
MX_IGES_REGEN=* dotnet test --filter "FullyQualifiedName~FileIO.IgesImport.Regenerate"
MX_IGESEXPORT_REGEN=* dotnet test --filter "FullyQualifiedName~FileIO.IgesExport.Regenerate"
```

Then **read every regenerated sidecar before committing** - regeneration records what Rhino does
today, and committing an unreviewed baseline enshrines whatever bug it contains.

`models\IGESfile-export\rhino-native-mix-iges.3dm` is the export suite's purpose-made source,
authored by the same file: a solid, a capped cylinder, an extrusion, an open surface, a curve, a
point, a mesh and a block inserted twice. Its baseline reads as a list of what the IGES writer
does to each - most notably `srcmeshes 1` against `meshes 0` (the writer skips meshes under the
default `meshtype none`) and `srcinstances 2` against `instances 0` (IGES has no assembly
structure here; blocks flatten).

## Adding a test

Exactly as in the STEP guide, with the IGES spellings:

- **Import:** drop the `.igs` in `models\IGESfile\`, regenerate with
  `MX_IGES_REGEN=<name>`, review, trim to the keys worth pinning, commit model + sidecar.
- **Export, IGES source:** nothing to do - `IgesExport` already round trips the import corpus.
- **Export, Rhino-native source:** drop the `.3dm` in `models\IGESfile-export\` (or better, add it
  to `AuthorIgesCorpus.cs` so it is generated), regenerate with `MX_IGESEXPORT_REGEN=<name>`,
  review the two columns, commit.
- A model that imports or round trips *wrongly* goes in the matching `-future` folder with a
  hand-written baseline describing the wanted result.

## Running

```bash
MSBuildEnableWorkloadResolver=false dotnet test FileIO/FileIO.csproj -f net48 --no-build --filter "FullyQualifiedName~FileIO.IgesImport"
MSBuildEnableWorkloadResolver=false dotnet test FileIO/FileIO.csproj -f net48 --no-build --filter "FullyQualifiedName~FileIO.IgesExport"
```

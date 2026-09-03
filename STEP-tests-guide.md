# STEP import and export tests — how they work, and how to add one

**Scope:** the 57 STEP tests in the `FileIO` project. Implementation lives in
`FileIO\STEP\StepImportBase.cs` and `FileIO\STEP\StepExportBase.cs`; the folder-to-fixture mapping is in
`FileIO\Rhino.Testing.Configs.xml`.
The repository `readme.md` covers the same ground in condensed form (§ "To add a new STEP import
test" and § "To add a new STEP export test").

---

## The 57

| Fixture | Tests | Runs by default? | Folder |
| --- | --- | --- | --- |
| `StepImport` | 27 (26 models + `ThereAreDataDrivenModels`) | yes | `models\STEPfile\` |
| `StepExport` | 30 (29 models + `ThereAreDataDrivenModels`) | yes | `models\STEPfile\` + `models\STEPfile-export\` |
| `StepImportFuture` | — | no, `[Explicit]` | `models\STEPfile-future\` |
| `StepImportLarge` | — | no, `[Explicit]` | `models\STEPfile-large\` |
| `StepExportFuture` | — | no, `[Explicit]` | `models\STEPfile-export-future\` |
| `StepExportLarge` | — | no, `[Explicit]` | `models\STEPfile-large\` |

The export count is higher than the import count because `StepExport` scans the whole import corpus
*plus* the two `.3dm` files that happen to sit in `models\STEPfile\AP214\`, *plus*
`models\STEPfile-export\`.

Each fixture also carries an `[Explicit]` `Regenerate` test, which is the baseline-authoring tool
rather than a test.

---

# Import tests (`StepImport`)

## What one test does

For each `.stp` / `.step` / `.p21` found under `models\STEPfile\` (recursively):

1. Create a **headless document pinned to millimetres, tolerance 0.001**. Pinned deliberately — the
   importer converts the file's own units into the document's, so a baseline recorded on one machine
   is only meaningful if every other machine imports into the same unit system.
2. `FileStp.Read` with `JoinSurfaces = true`, `LimitFaces = false`. The face cap is off because the
   point of the large-assembly suite is that nothing gets silently dropped.
3. Assert the read returned `true` and produced at least one object.
4. Measure the document.
5. Compare against the sidecar `<model>.stp.expected.txt`.

## What it measures

Two tiers, gathered into `StepMetrics`.

**Document level**

| Key | Meaning |
| --- | --- |
| `units` | The document's unit system after import. |
| `objects` | Top-level objects. A block instance counts as **one**. Deliberately not `doc.Objects.Count`, which also counts objects living inside block definitions. |
| `instances` | Top-level block instances. |
| `blockdefs` | Block definitions in the document. |
| `layers` | Layer count. |

**Leaf geometry**

The walker recurses into block definitions and composes the transforms as it goes, so *an assembly
that arrives as nested blocks measures the same as the same assembly arriving flat*. Over those
leaves:

| Key | Meaning |
| --- | --- |
| `breps` `extrusions` `surfaces` `meshes` `subds` `curves` `points` `other` | Type counts. `Extrusion` derives from `Surface`, so it is matched first. |
| `solids` | Leaves that are closed (`Brep.IsSolid`, `Mesh.IsClosed`, …). |
| `invalid` | Leaves failing `IsValid`. Normally `0`. |
| `area` | Sum of `AreaMassProperties` over breps, surfaces and meshes. |
| `volume` | Sum of `VolumeMassProperties` over the solids only. |
| `bbox` | Union of the leaf bounding boxes, with the instance transform applied. |

Two details worth knowing:

- **`invalid` explains itself.** When the count differs the run names each offender — geometry type,
  bounding-box centre, and the `IsValidWithLog` reason — on the progress stream, in `MX_STEP_LOG`,
  and in the failure message, up to ten of them. "Expected 0 but was 1" over a 2000-brep assembly
  tells you nothing on its own.
- **Measurement is demand-driven.** `Measure(doc, wanted)` only computes area, volume and bbox if the
  oracle actually asks for them. Deleting `area` and `volume` from a sidecar makes that model cheap.

Area and volume are measured on a *placed* copy when the instance transform is not the identity.
They are invariant under the rigid transforms a STEP assembly normally carries, but nothing
guarantees the transform is rigid.

## The oracle format

A STEP file has nowhere to keep expected values the way a `.3dm` keeps them in its Notes, so the
oracle lives beside the model as `<model>.stp.expected.txt`:

```
STEP IMPORT
units Millimeters
objects 1
instances 1
blockdefs 9
layers 2
breps 18
extrusions 0
surfaces 0
meshes 0
subds 0
curves 0
points 0
other 0
solids 18
invalid 0
area 141644.82132659463
volume 765931.74634135619
bbox -10.000000000000028,0,-7.0000000000000853 to 189.99999999999997,150,79.999999999999972
```

**Only the keys actually present are asserted.** That is the central design point: a model can be
pinned loosely (counts only) or tightly (down to the volume) with no code change.

- The file must open with the line `STEP IMPORT`.
- Lines starting `#` are comments, and they survive regeneration.
- An unknown key is an error, not a warning.
- Counts compare **exactly**. `area`, `volume` and each of the six `bbox` coordinates compare within
  `max(|expected| × 1e-8, 1e-6)`. Both terms are overridable with `MX_STEP_RELTOL` and
  `MX_STEP_ABSTOL` — the relative term carries large models, the absolute one keeps a coordinate
  that lands on zero from needing an exact hit.

## Adding an import test

1. **Drop the file in `models\STEPfile\`.** Subfolders are scanned, so `AP214\` or any other
   grouping works.

2. **Generate its baseline.**

   ```bash
   MX_STEP_REGEN=mymodel.stp dotnet test --filter "FullyQualifiedName~StepImport.Regenerate"
   ```

   `MX_STEP_REGEN` takes comma-separated substrings of file names, or `*` for the whole folder.
   Nothing is ever regenerated without it. Add `MX_STEP_REGEN_DRYRUN=1` to see the before/after
   without writing.

3. **Read the generated file before committing it.** Regeneration records what Rhino does *today*.
   If that is wrong, you have just enshrined a bug as the expected result. Delete any keys you do not
   want pinned — a later regeneration preserves whatever keys the file already declares, so a model
   trimmed to counts stays trimmed.

4. **Commit the model and its `.expected.txt` together.**

### Conventions

- A filename beginning `#` is skipped by the scanner.
- A filename beginning `!` is *expected to fail* — the test asserts that the comparison throws.
- Anything ending `bak` is ignored.
- On failure the imported document is saved beside the model as `#name.3dm`, so it can be opened and
  looked at. (The `#` keeps the debug file from becoming a test case of its own.)

### If the model does not import correctly

It belongs in `models\STEPfile-future\`, not `models\STEPfile\`. Baselines there are written by hand,
or taken from a Rhino that got the model right: **they describe the wanted result, not the current
one.** Regenerating one replaces the goal with the bug, which is why the fixture is `[Explicit]` and
why `MX_STEP_REGEN_DRYRUN=1` exists. When a model there comes up green it has been fixed — move it
and its `.expected.txt` into `models\STEPfile\` so that it starts guarding the fix.

### The large folder

`models\STEPfile-large\` holds assemblies of hundreds of megabytes. The fixture is `[Explicit]`
because one model can take minutes and gigabytes of memory, and the models themselves are not
committed — the folder is git-ignored apart from the baselines, so on most machines the fixture finds
nothing and stays quiet. New baselines there default to **counts and bounding box only**: computing
area and volume over a full vehicle assembly costs far more than the import does. Regenerate an
individual model with `MX_STEP_REGEN_FIELDS=ALL` when the extra confidence is worth the wait. Debug
model writing is off for this fixture.

### Import environment variables

| Variable | Effect |
| --- | --- |
| `MX_STEP_REGEN` | Comma-separated substrings of file names to regenerate; `*` means all. Nothing happens without it. |
| `MX_STEP_REGEN_FIELDS` | `ALL` writes every key, `COUNTS` writes everything but area and volume. Unset keeps the keys the existing sidecar declares, and uses the fixture default for a new one. |
| `MX_STEP_REGEN_DRYRUN` | `1` reports the before/after without writing anything. |
| `MX_STEP_REGEN_LOG` | Where the before/after report is appended. Defaults to `%TEMP%\mx_step_regen_report.txt`. |
| `MX_STEP_LOG` | Where each run's measurements and import time are appended. Defaults to `%TEMP%\mx_step.txt`. |
| `MX_STEP_RELTOL` / `MX_STEP_ABSTOL` | Comparison slack. Defaults `1e-8` and `1e-6`. Shared with the export suite. |

---

# Export tests (`StepExport`)

## What one test does

Export is tested as a **round trip**. That is what makes it checkable without hand-authoring a
reference STEP file for every model — and comparing the two ends against each other is more
informative than either alone.

1. **Open the source.** A `.3dm` opens with `RhinoDoc.OpenHeadless` and keeps the units it was
   authored in, because the exporter writes in document units and that is part of what is under test.
   A STEP source is imported into the same pinned-millimetre document `StepImport` uses, so both
   suites measure the same numbers for the same file.
2. **Measure the source** → the `src*` keys.
3. **`FileStp.Write`** into a per-run GUID temp directory, with the options the sidecar pins.
4. **Part 21 structural checks** — see below.
5. **Read the written file back** into a fresh millimetre document.
6. **Measure that** → the unprefixed keys.
7. Compare both ends against `<model>.exported.txt`.

## The Part 21 checks

These need no baseline, and are what a model with no sidecar gets:

- `FileStp.Write` returned `true`.
- A file exists at the output path and is non-empty.
- Its first line starts `ISO-10303-21`.
- Its **last non-empty line** starts `END-ISO-10303-21`. This is the one that catches a truncated
  write — the exporter throwing half way, or running out of disk, still leaves a file that opens and
  still reports a plausible header.
- Its header declares a non-empty `FILE_SCHEMA`. Part 21 lets the header wrap anywhere, so the reader
  joins up to 400 header lines before matching rather than looking for `FILE_SCHEMA` at the start of
  one.
- The file imports again and produces at least one object.

## What it measures

The same eighteen `StepMetrics` keys as the import suite, **twice**, plus the write options:

```
STEP EXPORT
schema AP214                  <- write options, pinned rather than inherited
export2dcurves false
exportblack true
splitclosedsurfaces false
fileschema AUTOMOTIVE_DESIGN  <- from the written file's header, substring match
srcunits Millimeters          <- the model as it went in
srcobjects 10
srcinstances 2
srcblockdefs 1
srclayers 5
srcbreps 5
srcextrusions 1
srcmeshes 1
srccurves 2
srcpoints 1
srcsolids 6
srcinvalid 0
srcarea 3034.8671465679704
srcvolume 4350.4422848424047
srcbbox -2,-6,0 to 74,82,15
units Millimeters             <- the model as it came back
objects 1
instances 1
blockdefs 3
layers 4
breps 6
extrusions 0
meshes 0
curves 2
points 1
solids 5
invalid 0
area 2434.8671428461462
volume 3450.4422798799737
bbox -2,-6.00000000000001,-5.5511151231257827E-16 to 73.999999999999986,82,15
```

**Reading the two columns beside each other is the point — the difference between them *is* what the
export did.** From the baseline above (`rhino-native-mix.3dm`) you can read straight off that the
extrusion became a brep, the mesh was dropped, and ten top-level objects collapsed into a single
block instance.

### The option keys

| Key | Values | Note |
| --- | --- | --- |
| `schema` | `AP203`, `AP214`, `AP214_CC2`, `AP242` (and the `SF_*` spellings) | Suite default is **AP214**, because that is what most of the corpus is and what most callers ask for. The NIST FTC models are AP203 sources; they are still written back out as AP214. |
| `export2dcurves` | `true` / `false` (`1` / `0`) | Default `false`. |
| `exportblack` | `true` / `false` | Default `true`. |
| `splitclosedsurfaces` | `true` / `false` | Default `false`. Some receiving systems need it. |
| `fileschema` | any substring | Compared by **containment** against the written header, so a baseline can pin just `AUTOMOTIVE_DESIGN` without the bracketed version stamp. |

`FileStpWriteOptions` itself defaults to AP203 with `ExportBlack` on. Nothing here inherits those:
every option is pinned, written into every baseline, and therefore visible in the diff if a Rhino
default ever moves.

## The folders

`StepExport` scans **both**:

| Folder | Why |
| --- | --- |
| `models\STEPfile\` | Shared with `StepImport`. A file worth guarding on the way in is worth guarding on the way out, so the whole import corpus is round tripped for free. Its two `.3dm` files are picked up here too, since the export scanner accepts `.3dm`. |
| `models\STEPfile-export\` | `.3dm` sources. |

The second folder exists because **a STEP source can only ever hand the writer geometry a STEP
*reader* produced** — trimmed breps and blocks. The writer has to cope with everything a Rhino
document can contain, and several of those cases are only reachable from a `.3dm`:

| Case | Why it is worth a model |
| --- | --- |
| Extrusions | Rhino's own lightweight solid. The writer has to expand it. |
| Open and untrimmed surfaces | Not every export is a closed solid. |
| Closed surfaces | Exercises `splitclosedsurfaces`. |
| Curves and points | Written as STEP geometric curve sets, or dropped. |
| Blocks, nested blocks | Assembly structure, and whether it survives the trip. |
| Layers, colours, black objects | Exercises `exportblack`. |
| Meshes | The exporter **skips** them deliberately. A part-mesh, part-brep model pins that. |
| Annotation | Also skipped deliberately. |
| Non-manifold breps | Written as sets of unjoined surfaces rather than as a solid. |

There is one such model today: `models\STEPfile-export\rhino-native-mix.3dm`. It is *generated*
rather than drawn — `FileIO\STEP\AuthorExportSource.cs` is the readable statement of what is in it and
the way to extend it. It carries a solid, a capped cylinder, an extrusion, an open surface, curves, a
point, a mesh and a block inserted twice, and its baseline records what happens to each.

## Adding an export test

**For a STEP source: nothing to do.** It is already round tripped by virtue of living in
`models\STEPfile\`.

**For Rhino-native geometry:**

1. Put the `.3dm` in `models\STEPfile-export\` — or, better for something small and describable, add
   it to `AuthorExportSource.cs` so it is generated.

2. **Generate its baseline.**

   ```bash
   MX_STEPEXPORT_REGEN=mymodel.3dm dotnet test --filter "FullyQualifiedName~StepExport.Regenerate"
   ```

3. **Read the sidecar, comparing the two columns.** A baseline that records a loss you did not intend
   is telling you something — file it rather than committing it.

4. **To test a non-default schema**, edit `schema AP242` into the sidecar by hand. Regeneration
   preserves the options an existing file declares, so a model pinned to AP242 stays on AP242.

5. Commit the model and its `.exported.txt`.

### If the round trip is wrong

The model belongs in `models\STEPfile-export-future\` — `StepExportFuture`, `[Explicit]`, same
semantics as the import future folder: the baseline describes the wanted result, not the current one.
When it comes up green, move it and its `.exported.txt` into `models\STEPfile-export\`.

### The large folder

`StepExportLarge` shares the uncommitted `models\STEPfile-large\` assemblies with `StepImportLarge`,
and costs roughly three times as much: an import, an export and a second import. `[Explicit]`, counts
and bbox by default, no debug output — keeping the written STEP file and saving what it read back
would add a gigabyte of writes to a failure that is already slow.

### Export environment variables

They mirror the import ones exactly, on their own names, so that regenerating one suite never quietly
rewrites the other.

| Variable | Effect |
| --- | --- |
| `MX_STEPEXPORT_REGEN` | Comma-separated substrings; `*` means all. Nothing happens without it. |
| `MX_STEPEXPORT_REGEN_FIELDS` | `ALL` or `COUNTS`, overriding both the existing file's keys and the fixture default. |
| `MX_STEPEXPORT_REGEN_DRYRUN` | `1` reports without writing. |
| `MX_STEPEXPORT_REGEN_LOG` | Defaults to `%TEMP%\mx_step_export_regen_report.txt`. |
| `MX_STEPEXPORT_LOG` | Per-run measurements. Defaults to `%TEMP%\mx_step_export.txt`. |
| `MX_STEPEXPORT_KEEP` | `1` keeps the written STEP file beside the model even on success. |
| `MX_STEPEXPORT_REQUIRE_BASELINE` | `1` turns a missing sidecar into a failure. |
| `MX_STEP_RELTOL` / `MX_STEP_ABSTOL` | Shared with the import suite. |

---

## One asymmetry between the two suites

**Import fails hard on a missing baseline. Export does not.**

A model with no `.exported.txt` still runs, and still gets everything in the Part 21 list above. It
simply asserts nothing about the geometry, and says so on the progress stream:

```
[MXSTEPEX]  mymodel.3dm  no baseline: Part 21 invariants only, nothing about the geometry was asserted.
```

That is so dropping a `.3dm` in the folder gives you something immediately. Set
`MX_STEPEXPORT_REQUIRE_BASELINE=1` to turn it into a failure instead.

On failure the written STEP file is kept beside the model as `#name.exported.stp` and the read-back
document is saved as `#name.exported.3dm`, so both ends of the failure can be opened. The `#` is the
scanner's skip marker, so neither becomes a test case.

---

## Shared plumbing

The export sidecar reuses `StepOracle`'s parser, formatter and comparison **wholesale**. Two small
generalisations make that possible:

- `StepOracle.Read` takes the incipit line and the known-key vocabulary as parameters, so the export
  suite parses `STEP EXPORT` files with its own keys through the same code.
- `StepOracle.Check` takes an `IEnumerable<KeyValuePair<string, string>>` rather than a whole file, so
  one caller can assert the `src`-stripped subset against the source measurement and another the
  remainder against the round trip.

Both suites share `StepImporter.Measure`, which is why the two columns of an export baseline are
directly comparable with an import baseline for the same file.

## Running the suites

Inside the Rhino source tree, open `rhino\src4\BuildSolutions\Rhino.sln` and use Test Explorer, as
`readme.md` describes for the mesh suites.

Outside it, against an installed Rhino:

```bash
MSBuildEnableWorkloadResolver=false dotnet test FileIO/FileIO.csproj -f net48 --no-build --filter "FullyQualifiedName~FileIO.StepImport"
```

```bash
MSBuildEnableWorkloadResolver=false dotnet test FileIO/FileIO.csproj -f net48 --no-build --filter "FullyQualifiedName~FileIO.StepExport"
```

The out-of-tree run needs no extra setup: `FileIO\Rhino.Testing.Configs.xml` is committed, and it
points the harness at an installed Rhino and at the `models\` folders.

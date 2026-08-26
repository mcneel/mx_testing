# STEP import testing — handoff

**Branch:** `rhino-9.x` · **Written:** 2026-08-26 · **Status:** code complete, never executed

This document is the starting point for continuing the STEP import test work on another machine.
It covers what was built, why it was built that way, what is verified, what is not, and the
environment traps that cost time on the first machine.

---

## 1. What this is

The repo (`mx_testing`) is an NUnit data-driven test suite for Rhino geometry operations. Each
fixture scans one or more folders of models, runs an operation, and compares the result against an
oracle stored with the model. The task was to extend that framework to STEP file conversion:
import folders of `.stp` / `.step` files and check that the results are correct.

Nothing about the existing mesh fixtures was changed except one shared helper (see §3).

---

## 2. How to pick this up on the new machine

The work is **uncommitted** on branch `rhino-9.x`. Get it across by committing and pushing:

```bash
git add .gitignore readme.md MxTests/ models/STEPfile-future/ && git status
```

Review before committing — see §7 for what should and should not go into git.

Then, on the new machine, read this file, then `readme.md` (the STEP section), then
`MxTests/StepImportBase.cs`.

**The suite has never been run.** The single most valuable next action is §8, step 1.

---

## 3. Files

### New

| File | What it holds |
| --- | --- |
| `MxTests/StepImportBase.cs` | Everything: `StepMetrics`, `StepOracleFile`, `StepOracle` (parse/format/compare), `StepImporter` (headless import + measurement), `StepImportRunner` (run + regeneration), `AnyStepFixture<T>` (folder-scanning fixture base). ~700 lines. |
| `MxTests/StepImport.cs` | The verified fixture. Runs by default. |
| `MxTests/StepImportFuture.cs` | Models that do not import correctly yet. `[Explicit]`. |
| `MxTests/StepImportLarge.cs` | Very large assemblies. `[Explicit]`. |
| `models/STEPfile-future/readme.md` | Explains the future-folder convention; also lets git track the otherwise-empty folder. |
| `STEP-testing-handoff.md` | This file. |

### Modified

| File | Change |
| --- | --- |
| `MxTests/SetupFixture.cs` | `ScanFolders` gained an overload taking file extensions. The old two-argument signature still exists and delegates with `.3dm`, so every existing fixture is untouched. |
| `MxTests/Rhino.Testing.Configs.xml` | Six new `ModelDirectory` entries (three fixtures × repo + `private_models`). |
| `.gitignore` | Ignores the large STEP assemblies, keeps their baselines. |
| `readme.md` | New section: "To add a new STEP import test", the key table, the three-folder table, the environment variable table. |

### Moved

`models/STEPfile/Large Assemblies/` → `models/STEPfile-large/`

This was necessary: the folder scanner uses `SearchOption.AllDirectories`, so leaving the
assemblies nested inside `models/STEPfile/` would have swept ~1 GB of models into every default
test run. Same-volume rename of untracked files, trivially reversible.

---

## 4. Design — the decisions and why

### 4.1 What "correct" means

Rejected: comparing against a reference `.3dm` (needs a hand-authored reference per STEP file, and
`.3dm` goes through Git LFS). Rejected: validity-checks-only (catches nothing when geometry drifts
between Rhino versions).

**Chosen: a measured baseline plus validity**, mirroring how the mesh fixtures keep expected areas
in the model's Notes. A STEP file has nowhere to put Notes, so the oracle is a sidecar text file:

```
models/STEPfile/AP214/as1-ac-214.stp
models/STEPfile/AP214/as1-ac-214.stp.expected.txt
```

```
STEP IMPORT
# comments and YT/discourse links go here
units Millimeters
objects 18
breps 18
solids 15
invalid 0
area 128944.9427
volume 61233.882
bbox -50,-30,0 to 210,84.5,60
```

**Only the keys actually present are asserted.** This is the central design property — it lets one
model be pinned loosely (counts only) and another tightly (down to the volume) with no code change,
the same way the mesh oracles make the `closed`/`overlap` flags optional.

### 4.2 Leaf geometry

Type counts, `solids`, `invalid`, `area`, `volume` and `bbox` are measured over *leaf* geometry —
block instances expanded recursively, instance transforms applied. An assembly that arrives as
nested blocks therefore measures the same as the same assembly arriving flat. `objects`,
`instances`, `blockdefs` and `layers` are document-level counts on top of that.

Rationale: STEP assemblies arrive as blocks in some Rhino versions and flat in others; a metric
that flips meaning between them is a bad oracle.

### 4.3 Units are pinned in code

`StepImporter` forces `UnitSystem.Millimeters` and tolerance `0.001` on the headless document
before importing. The importer converts the file's own units into the document's, so a baseline
recorded on one machine is only meaningful if every machine imports into the same unit system.
`units` is also an assertable key so a mismatch is diagnosable rather than mysterious.

**If you change these constants, every baseline is invalidated.**

### 4.4 Lazy measurement

`StepImporter.Measure` takes the set of keys the oracle asked for and computes only those. Mass
properties over a full vehicle assembly cost far more than the import itself, so an oracle that
does not mention `area` or `volume` does not pay for them. This is what makes `StepImportLarge`
affordable — its baselines default to counts and bounding box only.

### 4.5 Tolerances

Counts are exact. `area`, `volume` and each `bbox` coordinate use
`max(|expected| × 1e-8, 1e-6)` — relative term for large models, absolute floor so a coordinate at
zero does not need an exact hit. Overridable with `MX_STEP_RELTOL` / `MX_STEP_ABSTOL`.
This mirrors `MeshSplit`'s `Math.Max(expected * 10e-8, ModelAbsoluteTolerance)`.

### 4.6 Conventions carried over from the mesh fixtures

- `#` filename prefix → model skipped (handled in `SetupFixture.ScanFolders`).
- `!` filename prefix → model expected to fail (`Assert.Throws<AssertionException>`).
- `*bak` files excluded — this is why `io1-ug-214PlusLayers.stpbak` is not picked up.
- Failed comparison saves what was imported next to the model as `#name.3dm` for inspection,
  the analogue of `MeasuredBase` writing a DEBUG-layer file. Off for `StepImportLarge`, where the
  write would itself take minutes.
- `[Explicit] Regenerate()` per fixture, gated on an environment variable, mirroring `MX_REGEN`.

### 4.7 Extension matching is done in managed code

`ScanFolders` enumerates `*` and filters on `Path.GetExtension`, rather than passing `*.stp` as a
search pattern. On Windows a three-letter search pattern also matches longer extensions, so
`*.stp` would have matched `.stpbak`. Do not "simplify" this back into a search pattern.

---

## 5. The three folders

| Folder | Fixture | Runs by default | Holds | Currently |
| --- | --- | --- | --- | --- |
| `models\STEPfile\` | `StepImport` | yes | Verified models. Import correctly and must keep doing so. | 22 models (in `AP214\` subfolder) |
| `models\STEPfile-future\` | `StepImportFuture` | no, `[Explicit]` | Models that do not import correctly yet. | 0 models |
| `models\STEPfile-large\` | `StepImportLarge` | no, `[Explicit]` | Assemblies of hundreds of MB. | 8 models, ~1 GB |

Subfolders are scanned, so grouping inside `models\STEPfile\` (as `AP214\` does) works.

### The future folder

This is the counterpart of `models\MeshBooleanUnion-future\` and friends. **One deliberate
difference:** the mesh `-future` folders are referenced by nothing at all — they are parking lots,
with no fixture and no config entry, so they cannot be run. The STEP one is wired to an
`[Explicit]` fixture instead, so it stays out of a normal run but can be executed on demand:

```bash
dotnet test --filter "FullyQualifiedName~StepImportFuture"
```

Green there means the model has been fixed — move it and its `.expected.txt` into
`models\STEPfile\`, where it starts guarding the fix.

**Trap:** a future model's `.expected.txt` describes what the import *should* produce, not what it
currently does. Running `Regenerate` on that folder replaces the goal with the bug. Use
`MX_STEP_REGEN_DRYRUN=1` to look without writing.

---

## 6. Environment variables

| Variable | Effect |
| --- | --- |
| `MX_STEP_REGEN` | Comma-separated substrings of filenames to regenerate; `*` means all. Nothing happens without it. |
| `MX_STEP_REGEN_FIELDS` | `ALL` writes every key, `COUNTS` everything but area/volume. Unset keeps the keys the existing sidecar declares, and uses the fixture default for a new one (`AllKeys` for `StepImport`/`StepImportFuture`, `CountKeys` for `StepImportLarge`). |
| `MX_STEP_REGEN_DRYRUN` | `1` reports before/after without writing. |
| `MX_STEP_REGEN_LOG` | Where the before/after report is appended. Default `%TEMP%\mx_step_regen_report.txt`. |
| `MX_STEP_LOG` | Where each run's measurements and import time are appended. Default `%TEMP%\mx_step.txt`. |
| `MX_STEP_RELTOL` / `MX_STEP_ABSTOL` | Override the comparison tolerances. |

---

## 7. What goes into git

- **Do commit:** the four `MxTests/Step*.cs` files, the config, `.gitignore`, `readme.md`, this
  file, `models/STEPfile-future/readme.md`, and every `.expected.txt`.
- **Do commit (your call):** `models/STEPfile/AP214/` — the STEP files are ~2.5 MB total. If you do,
  consider adding `*.stp`/`*.step` to `.gitattributes` for LFS, alongside the existing `*.3dm` rule.
  Not done here because it changes LFS setup repo-wide.
- **Do not commit:** `models/STEPfile-large/` STEP files (~1 GB). `.gitignore` already handles this —
  verified with `git check-ignore` that the `.stp` files are ignored and their `.expected.txt`
  baselines are not.

---

## 8. Next steps, in order

1. **Prove the harness runs at all.** Everything else depends on this.
   ```bash
   dotnet test --filter "FullyQualifiedName~StepImport.ThereAreDataDrivenModels"
   ```
   Expect: passes, 22 models found. If Rhino core fails to start, see §10.

2. **Confirm `FileStp.Read` works in headless Rhino.** This is the largest unproven assumption in
   the whole design. Dry-run one small model:
   ```bash
   set MX_STEP_REGEN=io1-ug-214.stp
   set MX_STEP_REGEN_DRYRUN=1
   dotnet test --filter "FullyQualifiedName~StepImport.Regenerate"
   ```
   Expect: a `===== REGEN io1-ug-214.stp =====` block with plausible measurements.
   If `FileStp.Read` returns false or throws under headless Rhino, the fallback is
   `RhinoDoc.Import(path, options)` — but that routes through the file-import plugin machinery and
   is less deterministic, so the baselines would need regenerating if you switch. See §9.

3. **Generate the AP214 baselines.**
   ```bash
   set MX_STEP_REGEN=*
   dotnet test --filter "FullyQualifiedName~StepImport.Regenerate"
   ```

4. **Read every generated `.expected.txt` before committing it.** Regeneration records whatever
   Rhino currently produces; confirming that is actually *correct* is human work and is the part
   that gives the suite its value. Trim each file to the keys worth pinning.

5. **Run the suite for real** and confirm it is green.
   ```bash
   dotnet test --filter "FullyQualifiedName~StepImport"
   ```

6. **Populate `models/STEPfile-future/`** with known-bad STEP models and hand-written baselines
   describing the wanted result.

7. **Large assemblies, when you have the patience.** Expect minutes and gigabytes per model.
   ```bash
   set MX_STEP_REGEN=*
   dotnet test --filter "FullyQualifiedName~StepImportLarge"
   ```

---

## 9. API facts already established

Verified by reflecting over `C:\Program Files\Rhino 8\System\RhinoCommon.dll` — no need to
re-derive these:

- `Rhino.FileIO.FileStp.Read(string, RhinoDoc, FileStpReadOptions) → bool`
- `FileStpReadOptions` has exactly three properties: `JoinSurfaces`, `LimitFaces`, `MaxFaceCount`.
  The code sets `JoinSurfaces = true, LimitFaces = false` — a face cap would silently truncate
  exactly the models it matters most for.
- `RhinoDoc.CreateHeadless(string)` exists; `ModelUnitSystem` and `ModelAbsoluteTolerance` are
  both settable.
- `InstanceObject.InstanceXform` / `.InstanceDefinition`, `InstanceDefinition.GetObjects()` — used
  for the leaf-geometry walk. Parent transform composes as `parentXform * instance.InstanceXform`.
- `AreaMassProperties.Compute` has `Brep` / `Surface` / `Mesh` overloads;
  `VolumeMassProperties.Compute` has the same three. Neither has a `SubD` overload — SubD is
  counted but contributes no mass properties (STEP does not produce SubDs anyway).
- `Extrusion` derives from `Surface`, so the pattern-match arm for `Extrusion` must come first.
- `Rhino.DocObjects.Environment` collides with `System.Environment`. Every environment-variable
  call in this codebase must be written `System.Environment.GetEnvironmentVariable(...)` —
  `MeasuredBase.cs` does the same. This is a compile error, not a warning, because the project
  sets `TreatWarningsAsErrors`.

---

## 10. Environment traps hit on the first machine

The first machine could not run the suite. Recorded here so the time is not spent twice.

- **`MxTests` does not restore standalone on this branch.** `dotnet restore` fails with
  `NU1605: Detected package downgrade: RhinoCommon from 9.0.25350.305-wip to 8.34.26223.11001` —
  `MxTests.csproj` asks for `RhinoCommon 8.*` while `Rhino.Testing 9.0.5-beta → Rhino.Inside
  9.0.10-beta` wants RhinoCommon 9. The project is set up for the **in-Rhino build**: when
  `..\..\..\..\RhinoProjectPropertySheets\Rhino.CS.Dll.props` exists, `InRhino=True` and it uses a
  `ProjectReference` to `RhinoCommon.csproj` instead of the package. **Build inside the Rhino
  source tree**, or fix the standalone pin to RhinoCommon 9.
- **The project targets `net48;net10.0-windows`.** A machine with only the .NET 8 SDK fails at
  restore with `NETSDK1045` before it even gets to the framework selection — restore evaluates all
  target frameworks. `-f net48` does not avoid it; you need `-p:TargetFrameworks=net48` too, or
  just install a .NET 10 SDK.
- **Verification was done with a throwaway project** that compiled the new `.cs` files against
  `Rhino.Testing 9.0.5-beta` + `RhinoCommon 9.0.25350.305-wip` on `net8.0-windows`, with
  `TreatWarningsAsErrors` and `WarningLevel 999` to match the real project. This is a good trick
  for a fast syntax/semantic check without the Rhino source tree.
- **That throwaway project could not start Rhino.** NUnit cannot even load the test assembly unless
  `RhinoCommon.dll` is resolvable, but copying `RhinoCommon.dll` into the output directory defeats
  Rhino.Inside's resolver — it then probes the app directory for sibling assemblies and fails on
  `RhinoWindows.dll`. Rhino core itself started fine (~8 s), so the blocker was purely the
  scratch project's assembly resolution, not the test code. **Do not repeat this; use the real
  project inside the Rhino source tree.**

---

## 11. Verification status

**Verified on the first machine:**

- Compiles clean against RhinoCommon 9 under `TreatWarningsAsErrors` + `WarningLevel 999`.
- NUnit discovers the fixture and its test cases — 23 cases for `StepImport` against the AP214
  folder (22 models + `ThereAreDataDrivenModels`). Folder scanning, `TestCaseSource` and fixture
  wiring all work.
- All six `ModelDirectory` entries resolve to the intended absolute paths, with the right model
  counts per fixture (22 / 0 / 8).
- `.gitignore` behaves: `git check-ignore` confirms the large `.stp` files are ignored while their
  `.expected.txt` baselines and the future-folder readme are not.

**Not verified — assume nothing:**

- `FileStp.Read` under headless Rhino has never executed. See §8 step 2.
- No `.expected.txt` baseline exists anywhere yet, so **every STEP test currently fails** with the
  "has no baseline" message. That is by design — the message names the exact regeneration command.
- The leaf-geometry walk, the transform composition order, the mass-property sums and the oracle
  round-trip (`Format` → `Read` → `Check`) have all been reasoned about but never run against real
  data.

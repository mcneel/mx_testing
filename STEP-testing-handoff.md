# STEP import testing — status

**Branch:** `step-testing` · **Written:** 2026-08-26 · **Updated:** 2026-08-26 (export suite added)
**Status:** running and green against Rhino 9 WIP 9.0.26238.6303. 21 verified import models, 1 known
bug, and a 24-model export round trip suite (§15).

This document covers what was built, why it was built that way, what is verified, what is not, and
the environment traps that cost time on each machine.

**What changed on the second machine:** the suite was built, executed and proven for the first
time. `FileStp.Read` works under headless Rhino. All 21 importable AP214 baselines were generated
and reviewed, `StepImport` is green in about four seconds, and the first real bug the suite found —
`d2-db-214.stp` — has been moved to `models\STEPfile-future\`. Two harness defects were fixed
along the way (§12).

---

## 1. What this is

The repo (`mx_testing`) is an NUnit data-driven test suite for Rhino geometry operations. Each
fixture scans one or more folders of models, runs an operation, and compares the result against an
oracle stored with the model. The task was to extend that framework to STEP file conversion:
import folders of `.stp` / `.step` files and check that the results are correct.

Nothing about the existing mesh fixtures was changed except one shared helper (see §3).

---

## 2. How to run it

Inside the Rhino source tree, open `rhino\src4\BuildSolutions\Rhino.sln` and use Test Explorer, as
`readme.md` describes for the mesh suites. That is the setup the project is built for.

Outside the Rhino source tree it also runs now, against an installed Rhino — see §10.3 for the
three local files that make that work. With those in place:

```bash
MSBuildEnableWorkloadResolver=false dotnet build MxTests/MxTests.csproj -p:TargetFrameworks=net48
MSBuildEnableWorkloadResolver=false dotnet test MxTests/MxTests.csproj -f net48 --no-build --filter "FullyQualifiedName~MxTests.StepImport"
```

Read this file, then `readme.md` (the STEP section), then `MxTests/StepImportBase.cs`.

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

`models/STEPfile/AP214/d2-db-214.stp` → `models/STEPfile-future/AP214/d2-db-214.stp`

Done with `git mv` on the second machine, once running the suite showed the file does not import
at all. See §13.

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
| `models\STEPfile\` | `StepImport` | yes | Verified models. Import correctly and must keep doing so. | 21 models (in `AP214\`), all baselined and green |
| `models\STEPfile-future\` | `StepImportFuture` | no, `[Explicit]` | Models that do not import correctly yet. | 1 model: `AP214\d2-db-214.stp` (§13) |
| `models\STEPfile-large\` | `StepImportLarge` | no, `[Explicit]` | Assemblies of hundreds of MB. | 8 models, ~1.1 GB, all baselined (§14) |

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
- **Do not commit:** `models/STEPfile-large/` STEP files (~1.1 GB). `.gitignore` handles this —
  verified with `git check-ignore` that the models are ignored and their `.expected.txt` baselines
  are not.

  The rule was widened on the second machine. It used to read `models/STEPfile-large/*` with
  re-inclusions beside it, which only works while the models sit directly in that folder: put them
  in a subfolder — which the scanner supports, and which `models/STEPfile/AP214/` already does —
  and git stops descending, so the re-inclusion never matches and the baselines get ignored along
  with the gigabyte. It now excludes recursively and re-admits the subfolders explicitly. Both
  layouts were checked with `git check-ignore`.
- **Do not commit (second machine):** `MxTests/Directory.Build.targets` and `MxTests/local.app.config`
  (§10.3). Both are in `.git/info/exclude`. They hard-code one machine's Rhino path and version and
  would break the in-Rhino build for everyone else.
- **Decide before pushing:** the `RhinoSystemDirectory` change in `MxTests/Rhino.Testing.Configs.xml`
  from Rhino 8 to Rhino 9 WIP. It is tracked, and on a Rhino 9 branch it is arguably the right
  value, but it is a shared file and it is also the one thing here that is purely local.

---

## 8. Next steps, in order

Steps 1–6 of the original plan are done (§11). What is left:

1. **Decide whether `d2-db-214.stp` gets a YouTrack issue** and put the RH number in a comment in
   `models\STEPfile-future\AP214\d2-db-214.stp.expected.txt`. See §13 for the diagnosis.

2. **Pin the loose baselines.** Every `.expected.txt` in `models\STEPfile\AP214\` currently carries
   the full key set, including `area`, `volume` and `bbox` recorded to full double precision. That
   is the strictest possible setting and the right default while the numbers are fresh, but it also
   means a legitimate improvement in the STEP reader shows up as 21 failures. Consider trimming the
   seven `boxy_with_*` files — identical geometry, they differ only in the GD&T annotation carried
   alongside — down to counts, and leaving the `as1-*` / `io1-*` families fully pinned.

3. **Decide what to do about the three large assemblies that import one invalid brep** (§14).
   Their baselines currently pin `invalid 1`, which records a defect as expected behaviour.

4. **Add more future models.** One known-bad file is a thin `-future` folder. STEP bugs from
   YouTrack with attached files are the natural source.

5. ~~**Consider export.**~~ **Done — see §15.**

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

## 10. Environment

### 10.1 Traps hit on the first machine

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

### 10.2 What the second machine has

Installed: Rhino 9 WIP **9.0.26238.6303**, .NET SDKs 7.0.405 / 8.0.101 / 10.0.400.

There **is** a Rhino source tree, at `F:\Users\scottd\Documents\GitHub\rhino` (branch `9.x`), and it
already contains its own checkout of this repository as the submodule
`src4\rhino4\Plug-ins\MxTesting` — sitting on an older commit, with none of the STEP work. That is
the path `MxTests.csproj` resolves `..\..\..\..\RhinoProjectPropertySheets\Rhino.CS.Dll.props`
against, so a clone at that location gets `InRhino=True` and the supported build.

None of that helps yet: **the tree has no built Rhino.** There is no `src4\bin`, and the only
`RhinoCommon.dll`s in it are vendored antiques. `InRhino=True` switches to a `ProjectReference` on
`RhinoCommon.csproj`, which needs the native core built too, so the in-Rhino path costs a full
Rhino build before it runs a single test. The clone the work actually lives in is the standalone
one at `F:\Users\scottd\Documents\GitHub\mx_testing`, where `InRhino=False` and §10.3 applies.

The source tree earns its keep anyway: `src4\rhino4\Plug-ins\io_STEP\import_STEP\` is the importer,
and reading it is what turned §13 from a hypothesis into a root cause.

### 10.3 Making the standalone build work

Three local, **untracked** files, all listed in `.git/info/exclude` so they cannot be committed by
accident. Delete all three to get the stock in-Rhino behaviour back.

| File | Why |
| --- | --- |
| `MxTests/Directory.Build.targets` | Bumps the `RhinoCommon 8.*` pin to `9.0.26237.15343-beta`, the closest published build to the installed Rhino, which clears the `NU1605` downgrade. Also pins `Grasshopper` to the same build with `ExcludeAssets="all"` — `Grasshopper` arrives transitively from `Rhino.Testing` with an *exact* dependency on RhinoCommon, so without this the fix trades `NU1605` for `NU1608`. Finally, it copies `local.app.config` over the generated `MxTests.dll.config` after every build. |
| `MxTests/local.app.config` | The important one. See below. |
| `MxTests/Rhino.Testing.Configs.xml` (tracked, edited) | `RhinoSystemDirectory` moved from `C:\Program Files\Rhino 8\System\` to `C:\Program Files\Rhino 9 WIP\System\`. This *is* tracked — the file's own comment invites the local override, but do not push the change without deciding it is right for the branch. |

**The assembly-resolution problem, solved.** §10.1 records that NUnit cannot load `MxTests.dll`
unless `RhinoCommon.dll` is resolvable, and that copying it into the output directory does not
work. The in-Rhino build never hits this because the property sheet redirects `OutputPath` into
Rhino's own bin folder, where `RhinoCommon.dll` is already a sibling.

The standalone fix is to bind straight to the installed Rhino with `<codeBase>` and copy nothing:

```xml
<dependentAssembly>
  <assemblyIdentity name="RhinoCommon" publicKeyToken="552281e97c755530" culture="neutral" />
  <bindingRedirect oldVersion="0.0.0.0-9.9.9.9" newVersion="9.0.26238.6303" />
  <codeBase version="9.0.26238.6303" href="file:///C:/Program Files/Rhino 9 WIP/System/RhinoCommon.dll" />
</dependentAssembly>
```

`Rhino.UI` and `Eto` need the same treatment. The versions must match the installed Rhino exactly:

```powershell
[Reflection.AssemblyName]::GetAssemblyName('C:\Program Files\Rhino 9 WIP\System\RhinoCommon.dll')
```

The test output confirms it worked — each run prints
`RhinoCommon: C:\Program Files\Rhino 9 WIP\System\RhinoCommon.dll`, so the real Rhino assembly is
loaded in place, and Rhino.Inside's resolver is left to handle the rest.

One more thing: **build and test `net48`.** `-p:TargetFrameworks=net48` to build, `-f net48` to
test. See §10.5 for why `net10.0-windows` is not usable here.

**Obsolete, kept because the symptom is memorable:** every build initially had to be run with
`MSBuildEnableWorkloadResolver=false`, because the .NET 10 SDK's workload manifests were
inconsistent — `Microsoft.NET.Workload.Emscripten.Current` was at `8.0.1` where the mono toolchain
manifest required `10.0.111` — and every build died in the SDK resolver with `MSB4242` before it
reached the project at all. Reinstalling the .NET 10 SDK fixed it; the manifest is now `10.0.111`
and `dotnet workload list` resolves cleanly. If `MSB4242 SDK Resolver Failure` reappears, that
environment variable is the quick way past it, and repairing the SDK is the real fix.

### 10.5 net10.0-windows does not work here

Worth knowing, because it is the runtime Rhino 9 actually ships on and so looks like the more
faithful target. `MxTests.dll.config` is a .NET Framework mechanism — .NET Core ignores
`bindingRedirect` and `codeBase` entirely — so the §10.3 fix does nothing for `net10.0-windows`,
and discovery finds zero tests exactly as it did before.

Copying `RhinoCommon.dll`, `Rhino.UI.dll` and `Eto.dll` from Rhino's System folder into
`bin\Debug\net10.0-windows\` *does* make discovery work — all tests are found. But the test host
then **crashes hard** at Rhino startup, before any model is touched: even
`ThereAreDataDrivenModels`, which imports nothing, takes the host down. No managed exception is
raised, the socket to the runner simply closes, which points at the native side rather than at
anything catchable. This is the same shape of failure §10.1 records from the first machine.

If those three DLLs are still sitting in the net10 output, delete them — they leave that target in
a worse state than not working, because the tests are discovered and then abort:

```bash
rm bin/Debug/net10.0-windows/RhinoCommon.dll bin/Debug/net10.0-windows/Rhino.UI.dll bin/Debug/net10.0-windows/Eto.dll
```

Making this target work properly needs an `AssemblyLoadContext` resolver installed before NUnit
reflects over the assembly, which is the same chicken-and-egg problem `codeBase` solves on `net48`.
Not attempted. `net48` compiles against the same RhinoCommon and drives the same installed Rhino,
so the coverage loss is the runtime, not the API.

### 10.4 Diagnosing "no tests discovered"

`dotnet test --list-tests` reporting zero tests means the assembly failed to load, and the default
output does not say why. This gets the real reason:

```xml
<RunSettings><NUnit>
  <InternalTraceLevel>Verbose</InternalTraceLevel>
  <DumpXmlTestDiscovery>true</DumpXmlTestDiscovery>
</NUnit></RunSettings>
```

Run with `-s <that file>`, then read `_SKIPREASON` in `bin\Debug\net48\Dump\D_MxTests.dll.dump`.
That is what turned a silent "0 tests" into
`Could not load file or assembly 'RhinoCommon, Version=…'`.

---

## 11. Verification status

**Verified on the first machine:**

- Compiles clean against RhinoCommon 9 under `TreatWarningsAsErrors` + `WarningLevel 999`.
- NUnit discovers the fixture and its test cases. Folder scanning, `TestCaseSource` and fixture
  wiring all work.
- All six `ModelDirectory` entries resolve to the intended absolute paths.
- `.gitignore` behaves: `git check-ignore` confirms the large `.stp` files are ignored while their
  `.expected.txt` baselines and the future-folder readme are not.

**Verified on the second machine — the suite has now actually run:**

- `FileStp.Read` **works under headless Rhino.** This was the largest unproven assumption in the
  design and it needed no fallback. `RhinoDoc.CreateHeadless` + `FileStp.Read` imports the AP214
  models in about 0.1 s each; all 21 together, measured and compared, take four seconds.
- `StepImport` is **green: 22 cases, 21 models plus `ThereAreDataDrivenModels`.**
- The `.stpbak` exclusion works — `io1-ug-214PlusLayers.stpbak` is not picked up, confirming §4.7.
- Regeneration works, including `MX_STEP_REGEN`, `MX_STEP_REGEN_DRYRUN` and `MX_STEP_REGEN_LOG`.
- The leaf-geometry walk and the transform composition order are right. The evidence is
  `as1-ac-214.stp`: 1 top-level instance over 9 nested block definitions, walked down to 18 leaf
  breps, and its five sibling files — the same assembly exported by different CAD systems, arriving
  with different block structures — measure the same 18 breps, 18 solids and the same volume to
  within 1e-9 relative. A wrong transform order would have scattered the bounding boxes.
- The mass-property sums are consistent: the seven `boxy_with_*` files differ only in which GD&T
  annotation they carry, and all seven measure byte-identical area and volume. Five of the six
  `io1-*` files agree on 21106.0585 mm² / 78179.5807 mm³ from five different exporters.
- The oracle round-trip (`Format` → `Read` → `Check`) works: every baseline was written by
  `Regenerate` and then read back and asserted by a normal run.
- `StepImportFuture` behaves as designed: `[Explicit]`, ignored by a normal run, and red on demand
  with the diagnosis in the failure message.

- `StepImportLarge` runs and is baselined — see §14 for what it found.

**Still not verified:**

- The `!` expected-to-fail filename prefix. No STEP model uses it yet.
- `private_models\` model directories. Not present on this machine.
- `SaveDebugModel`, the `#name.3dm` written beside a model when a comparison fails. No comparison
  has failed yet.

---

## 12. Harness defects found by running it

Both fixed in `MxTests/StepImportBase.cs`.

**Regeneration aborted the whole folder on the first un-importable model.** `RegenerateOracle`
called `Assert.Fail` inline, so `MX_STEP_REGEN=*` stopped at `d2-db-214.stp` and silently left the
last eight models without baselines — with a report that looked like a normal single failure. It
now returns a `RegenOutcome`, the caller walks the whole folder, and the failures are collected and
reported together: *"Regenerated 21 baseline(s). 1 model(s) could not be imported."* Failures are
written to `MX_STEP_REGEN_LOG` too, so a partial run is legible afterwards.

**`objects` did not mean what `readme.md` said it meant.** The readme documents it as "top level
objects in the document, a block instance counts as one", but the code used `doc.Objects.Count`,
which also counts every object living inside a block definition. `as1-ac-214.stp` therefore
reported `objects 19` for a document Rhino shows as a single block instance: 1 top-level instance
plus the 18 objects distributed across its 9 definitions. Beyond being confusing, it broke the §4.2
promise that a nested assembly measures the same as a flat one, and it would have made the key
impossible to hand-write for a `-future` model. `Measure` now counts what the enumerator yields.
The nesting is still pinned, by `instances` and `blockdefs`.

**`invalid` was unactionable.** Not a defect so much as a gap that only shows up at scale: the
large assemblies produced `invalid 1` over two to three thousand breps, and the number alone gives
you no way to find the object or learn what is wrong with it. `Measure` now records, for up to
`StepMetrics.MaxInvalidReports` leaves, the geometry type, the centre of its bounding box and the
text of `IsValidWithLog`. The reports go to `MX_STEP_LOG` on every run, and into the failure
message when an asserted `invalid` count differs — `expected 0 but was 1` over a 2000-brep assembly
is not a useful thing to read. The oracle format is unchanged: `invalid` is still a plain count.

---

## 13. The first bug the suite found: `d2-db-214.stp`

Now at `models\STEPfile-future\AP214\d2-db-214.stp`, with a hand-written baseline stating the
wanted result. **Rhino 9.0.26238 imports nothing from it**: `FileStp.Read` returns `true` and the
document ends up with zero objects. `JoinSurfaces` and `LimitFaces` make no difference, and it
behaves identically in a full Rhino and in a headless one.

The file does contain a solid, reachable from the product definition:

```
#988  = MANIFOLD_SOLID_BREP('*SOL1', #987)                          -- 9 ADVANCED_FACEs
#1001 = ADVANCED_BREP_SHAPE_REPRESENTATION('*MASTER', (#988, #1000), #996)
#1139 = SHAPE_DEFINITION_REPRESENTATION(#20, #1001)
```

What separates it from the files that do import is that its single `PRODUCT_DEFINITION_SHAPE` #20
carries four representations rather than one — the `*MASTER` brep above, plus three
`DRAUGHTING_MODEL`s (`#1140`, `#1141`, `#1142`). `f1-db-214.stp` has the same `*MASTER` structure,
one `SHAPE_DEFINITION_REPRESENTATION`, and imports correctly.

**Root cause**, in `src4\rhino4\Plug-ins\io_STEP\import_STEP\StepToRhino.cpp`:

```cpp
// GetRepresentation(), ~line 3408
ON_SimpleArray<stp_shape_definition_representation*> sdr_list;
GetSDRUsedInList(pds, sdr_list);
if (sdr_list.Count() != 1)
  return 0;                                   // four SDRs here, so it gives up

// its caller, ~line 4887
stp_representation* rep = GetRepresentation(pd_array[i]);
if (!rep)
  return false;                               // and that abandons the whole file
```

So the importer is not choosing the wrong representation of the four. It refuses to choose at all
unless there is exactly one, and one product definition failing that test abandons the entire
import — which is why the failure is total and silent while `FileStp.Read` still reports success.
The same source file already has `GetProductDefSDRs()`, which does cope with a product shape
carrying several SDRs, so the fix is probably to pick the `ADVANCED_BREP_SHAPE_REPRESENTATION` out
of the list rather than to require the list to hold one entry. Note the function guards
`pds_list.Count() != 1` the same way one block earlier, so a product with several
`PRODUCT_DEFINITION_SHAPE`s presumably fails identically.

This is a static reading of the importer that matches the observed behaviour exactly. It has not
been stepped through in a debugger — there is no Rhino build in the source tree to debug (§10.2).

`f1-db-214.stp` is worth mentioning as the control: it holds three `MANIFOLD_SOLID_BREP`s and Rhino
imports one. That is correct — the other two belong to
`SHAPE_REPRESENTATION('explicit geometry branch', …)` form-feature branches, not to the `*MASTER`
shape the product resolves to. Its baseline pins the single solid.

---

## 14. The large assemblies

All eight import. Nothing failed, nothing needed a face cap, and every one of them arrives as a
single top-level block instance over a deep definition tree — which is also the clearest
confirmation that the §12 `objects` fix behaves at scale, since the old metric would have reported
two to three thousand.

| Model | Import | Breps | Solids | Block defs | Invalid |
| --- | ---: | ---: | ---: | ---: | ---: |
| `Ai-14R.stp` | 116 s | 2075 | 2054 | 285 | 0 |
| `Cruise_Assembly.stp` | 24 s | 2822 | 2789 | 84 | **1** |
| `NissanGT-R.STEP` | 129 s | 531 | 526 | 138 | **1** |
| `Rocky_House.stp` | 11 s | 165 | 161 | 50 | 0 |
| `ROTOR-201NAL-Z7.STEP` | 3 s | 94 | 94 | 46 | 0 |
| `Scania-8x4.stp` | 138 s | 1451 | 1400 | 228 | 0 |
| `Scania-Engine-V8-XT-Turbo.step` | 130 s | 1330 | 1295 | 187 | **1** |
| `UMC-500_SS_Solid_Model_2019-06_r1.stp` | 22 s | 348 | 338 | 183 | 0 |

A full pass — import, measure, compare against all eight baselines — is 9m43s. Baselines are counts
and bounding box only, no mass properties, per §4.4.

`Ai-14R.stp` is also the only model in the whole suite that imports anything other than breps: 344
curves and 114 points alongside its 2075 breps. Worth knowing that the non-brep counters are not
dead weight.

### The three invalid breps

Found by the new `invalid` reporting (§12). One bad brep out of hundreds or thousands, in three
files, and two of them are the **same defect**:

- `NissanGT-R.STEP` — `brep.m_E[428]`: `edge.m_vi[]=(344,335) but edge.IsClosed() is true`
- `Scania-Engine-V8-XT-Turbo.step` — `brep.m_E[289]`: `edge.m_vi[]=(252,251) but edge.IsClosed() is true`

An edge whose curve is geometrically closed while still carrying two distinct vertices. Two
unrelated assemblies, different exporters, identical failure — that is a repeatable importer defect,
not a quirk of one file, and it is the most promising lead in this section.

`Cruise_Assembly.stp` is a different and more ordinary fault: `brep.m_L[1]`, consecutive trims
whose ends disagree by 2.7e-6 in u.

**These baselines pin `invalid 1` on purpose.** That records a defect as the expected value, which
is uncomfortable but is the honest choice: it catches the count going to 2, and it goes red when
the importer is fixed, which is exactly when someone should look. Each of the three `.expected.txt`
files carries a comment saying so and naming its offending brep, so the number cannot be misread as
"fine". Comments survive regeneration — `StepOracle.Write` copies them from the old file — so this
does not evaporate the next time the baselines are refreshed.

---

## 15. STEP export — the round trip suite

**Added:** 2026-08-26, same branch, same machine, same Rhino 9 WIP 9.0.26238.6303.
**Status:** green. 24 models, 25 tests, ~10 s. `FileStp.Write` works headless.

This is §8.5 of the plan above, built. Nothing in the import suite changed except one small
refactor (§15.6).

### 15.1 What it does

Open the source model → measure it → `FileStp.Write` → check the written file is a well formed
Part 21 file → `FileStp.Read` it back → measure that → compare both ends against a sidecar.

Comparing the two ends against each other is the whole idea, and it is why the sidecar carries the
same eighteen keys twice: `srcbreps` against `breps`, `srcarea` against `area`. The difference
between the two columns *is* what the export did, readable straight off the file without running
anything. That is a better oracle than a hand-authored reference STEP file (which nobody would
maintain) and a much better one than "the writer returned true".

### 15.2 Files

| File | What it holds |
| --- | --- |
| `MxTests/StepExportBase.cs` | `StepExportOptions` (the write options, parsed from and formatted into the sidecar), `StepExportOracle` (the key vocabulary and the `src` split), `StepExportResult`, `StepExporter` (open source / write / read the Part 21 header), `StepExportRunner` (run + regeneration), `AnyStepExportFixture<T>`. ~700 lines. |
| `MxTests/StepExport.cs` | The everyday fixture. Runs by default. |
| `MxTests/StepExportFuture.cs` | Models that do not survive the trip yet. `[Explicit]`. |
| `MxTests/StepExportLarge.cs` | The big assemblies. `[Explicit]`. |
| `MxTests/AuthorExportSource.cs` | `[Explicit]` fixture that authors `rhino-native-mix.3dm`. See §15.5. |
| `models/STEPfile-export/rhino-native-mix.3dm` | The purpose-made `.3dm` source, plus its readme. |
| `models/STEPfile-export-future/readme.md` | Empty folder, convention documented. |
| 24 × `*.exported.txt` | The baselines. |

`Rhino.Testing.Configs.xml` gained eight `ModelDirectory` entries, `.gitignore` two lines
(`*#*.stp` for the kept-on-failure output, and re-admitting `*.exported.txt` under
`models/STEPfile-large/`), `readme.md` a section.

### 15.3 The corpus, and why it overlaps the import one

`StepExport` scans `models\STEPfile\` as well as `models\STEPfile-export\`. A file worth guarding
on the way in is worth guarding on the way out, the models are already there and already
characterised, and the cost is 10 s. That folder also already held two `.3dm` files
(`as1-ac-214.3dm`, `io1-ug-214PlusLayers.3dm`) which the export scanner picks up as sources — a
small bonus, since one of them carries extrusions.

Schema and option coverage is spread across the corpus rather than multiplied over it. Running
every model through all four schemas would be 96 tests to learn very little; instead five models
are pinned away from the default and the rest hold it:

| Model | Pinned to | Written `FILE_SCHEMA` |
| --- | --- | --- |
| `as1-ug-214.stp` | `AP203` | `CONFIG_CONTROL_DESIGN` |
| `f1-db-214.stp` | `AP214_CC2` | `AUTOMOTIVE_DESIGN_CC2` |
| `io1-ug-214.stp` | `AP242` | `AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF { 1 0 10303 442 3 1 4 }` |
| `boxy_with_cylindricity.stp` | `splitclosedsurfaces true` | — |
| `io1-ec-214.stp` | `export2dcurves true` | — |

All four schemas therefore have a model, and the `boxy_with_*` family — seven files of identical
geometry differing only in GD&T annotation — finally earns its duplication by donating one member
to an option.

### 15.4 What the round trip actually preserves

Reviewed across all 24 baselines. The answer is: essentially everything.

- **Counts.** `objects`, `instances`, `blockdefs`, `breps`, `solids`, `curves`, `points` come back
  identical on every STEP source. The 18-brep / 9-blockdef `as1-*` assemblies survive intact,
  including under AP203.
- **Bounding boxes.** Identical to machine precision everywhere — worst case 8.2e-13 mm absolute
  over a 190 mm span, most models 1e-14 or exact.
- **Area.** Matches to 1e-16 relative on the assemblies.
- **Volume.** Matches to ~1e-12 relative on the assemblies. The exception is the `boxy_with_*`
  family, where volume moves by **2.65e-7 relative** (174839.178 → 174839.225) while its area and
  bounding box are exact. That is a mass-property integration difference over a re-parameterised
  cylindrical face, not lost geometry. It is above the suite's 1e-8 comparison tolerance, which is
  why it is worth naming here — but the baselines pin the *round-tripped* value, which is
  deterministic, so nothing is fragile about it.
- **Layers** are the one count that moves, and it moves in both directions: `io1-ca-214.stp` goes
  2 → 1, `as1-ac-214.3dm` goes 1 → 2, `rhino-native-mix.3dm` goes 5 → 4. Layer structure through
  STEP follows the product structure rather than the source document, so this is expected rather
  than wrong — but it is now pinned, so a change to it will be noticed.
- **Meshes are dropped**, as the exporter intends. See §15.5.

### 15.5 `rhino-native-mix.3dm`, and the mesh finding

A STEP source can only ever hand the writer what a STEP reader produced: trimmed breps and blocks.
Everything else a Rhino document can hold is unreachable that way, so `models\STEPfile-export\`
exists for `.3dm` sources and `rhino-native-mix.3dm` is the first of them — a solid box, a capped
cylinder (a closed lateral face, for `splitclosedsurfaces`), an extrusion, an open untrimmed
surface, an arc, a black line (for `exportblack`), a point, a mesh box, and a block inserted twice,
across five layers.

It is **generated, not drawn**: `MxTests/AuthorExportSource.cs` is `[Explicit]`, refuses to run
without `MX_STEPEXPORT_AUTHOR_OUT` naming its output, and is the readable statement of what is in a
file that Git LFS otherwise makes opaque. Extend the model there, re-run it, regenerate the
baseline, review the diff, commit all three.

Its baseline immediately records the one behaviour worth having a model for. The exporter skips
meshes on purpose — `WriteSTEPfile.cpp` counts them into `SkippedMeshCount` and moves on — and the
sidecar shows it three ways at once:

```
srcmeshes 1      meshes 0
srcarea   3034.8671465679704    area   2434.8671428461462     (-600 mm2)
srcvolume 4350.4422848424047    volume 3450.4422798799737     (-900 mm3)
```

600 mm² and 900 mm³ are exactly the surface area and volume of the 15 × 10 × 6 mesh box. The
extrusion also behaves as it must: `srcextrusions 1` → `extrusions 0`, `srcbreps 5` → `breps 6`.

### 15.6 The one change to existing code

`StepOracle` was made reusable rather than copied. Four additions, all backward compatible, all
existing call sites untouched:

- `PathFor(modelPath, suffix)` beside `PathFor(modelPath)`
- `Read(path, incipit, knownKeys)`, with the old one-argument form delegating to it
- `Check(filename, entries, actual)` taking the entries rather than the whole file — which is what
  lets one sidecar be asserted twice, once per measurement
- `Write(incipit, old, lines)` beside the key-driven overload

`StepMetrics`, `StepImporter` and `StepImportRunner.RegenOutcome` are used as they stand. The
import suite is unchanged and still green (21 models); the whole default run is 2510 tests, green,
56 s.

### 15.7 Deliberate differences from the import suite

- **A missing sidecar is not a failure.** Import fails without a baseline, because measuring an
  import against nothing tells you nothing. Export still has real assertions with no baseline at
  all — the write returned true, the file is non-empty, opens with `ISO-10303-21;`, declares a
  `FILE_SCHEMA`, ends with `END-ISO-10303-21;`, and reads back into at least one object. So a model
  dropped into the folder is smoke-tested immediately, the run says
  `no baseline: Part 21 invariants only` on the progress stream, and
  `MX_STEPEXPORT_REQUIRE_BASELINE=1` turns the omission into a failure for anyone who wants CI
  strictness.
- **Separate environment variables** (`MX_STEPEXPORT_*`), so that regenerating one suite never
  quietly rewrites the other's baselines. The tolerances are shared, since they live on
  `StepOracle`.
- **The write options are pinned, not inherited.** `FileStpWriteOptions` defaults to `SF_203` with
  `ExportBlack = true`; the suite defaults to AP214 and writes all four flags into every baseline,
  so a moved default shows up as a diff instead of silently changing what is under test.

### 15.8 Next steps

1. **Delete `MxTests/_AuthorExportSource.cs`.** It is a one-line stub left behind by the session
   that wrote this; the real fixture is `AuthorExportSource.cs`.
2. **Run `StepExportLarge` once.** The eight assemblies are on disk and the fixture finds them, but
   a full pass was not attempted — expect roughly three times §14's 9m43s, and expect the write
   step to be where the surprises are.
3. **Put a model in `models\STEPfile-export-future\`.** It is empty, which means the folder
   convention is documented but untested. STEP export bugs from YouTrack with attached models are
   the natural source.
4. **Decide about the `boxy_with_*` volume drift** (§15.4). 2.65e-7 relative is small enough to
   ignore and large enough to be a real difference; it may be worth a YouTrack issue and a comment
   in the baselines.
5. **Extend `rhino-native-mix.3dm`.** Non-manifold breps and annotation are both documented as
   special cases in the exporter and neither is in the model yet. SubD is another gap, though it is
   not clear the writer handles it at all.
6. **Consider `exportblack false`.** The model has a black curve for it, but no baseline pins the
   option off, so the branch is unexercised.

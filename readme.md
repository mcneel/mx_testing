# Mesh intersections unit tests #

### :dart: Goals ###

This is a an NUnit 3.7 testing project for Mesh Intersection.

Its purpose is to run tests in Visual Studio, without the need of a big setup. In fact, right now there is no automatic testing system before PR are automatically merged.

YOUR TESTS ADDITIONS ARE WELCOME!

### :microscope: General usage ###

1. In Visual Studio, open the Rhino.sln solution. It's located in `rhino\src4\BuildSolutions`.
1. In Visual Studio, choose `Tests -> Run All Tests`.
1. If the Test panel does not show up, you can open it using `Tests -> Test Explorer`.
1. You can explore the project in the Solution Explorer panel (`Ctrl+Alt+L`): you can find the projects in `Solution (Rhino) -> Unit Tests`.
1. There is a `MxTests` project and a `RhinoCommonDelayed` project. The first one is loaded by the testing framework by reflection and does NOT directly use RhinoCommon. This allows to set up hooks and other features to make sure that RhinoCommon is loaded properly, before `RhinoCommonDelayed` is loaded.
1. There is a setting file located at `MxTests -> MxTests.testsettings.xml`. There is generally no need to modify any settings.

### :new: New test setup ###
The `MxTests.testsettings.xml` file contains settings for model lookup, and directories are specified in the `<ModelDirectory>` tag.

**General notes on test files:**
1. When the model is placed in a watched directory, the testing system will automatically load the file, perform splits, and check that the areas and properties match the specifications.
1. When a file in a watched folder starts with an exclamation mark, `!`, the test will run but will be expected to fail. It is considered a failure of this test, the fact that the test later does not fail. 
1. On the contrary, when the name begins with a hash sigh `#`, the test will be skipped. |
1. When a test fails and that wasn't expected, a debug file with the resulting geometry is created, and its name will be prefixed by the hash sign `#`. The result is added to the `DEBUG` layer. |


#### To add a new `_MeshIntersect` test: ####
1. Create a .3dm model with the geometry.
1. Purge all redundant geometry, plug-in data, materials, etc that does not need to be in the model to speed up file loading during each test run.
1. Keep two or more intersecting meshes.
   / Alternatively, some curves can be kept. They will be transformed into extrusion meshes with the same logic that applies to the MeshIntersect command.
1. Using the `_Notes` command, type notes following exactly this pattern:
    1. The first line should be "MEASURED INTERSECTION"
	1. Any line that begins with # will be considered a comment
	1. A list of the length of intesection curves sorted smallest to largest
	1. After each value the open/closed flag can be used.
```
MEASURED INTERSECTIONS
# This is a comment

1.025 closed perforation
2.025 open overlap
3.025
```
5. The open/closed and the perforation/overlap combos are optional, but the first is required if the second is explicited.
1. When the model is placed in a watched directory, the testing system will automatically load the file, perform intersections, and check that the lenghs and properties match the specifications.

#### To add a new `_MeshSplit` test: ####
1. Create a .3dm model with the geometry.
1. Purge all redundant geometry, plug-in data, materials, etc that does not need to be in the model to speed up file loading during each test run.
1. Keep two layers: 
    1. by convention, they should be called `A` and `B`. Any other combination of the first two layers in the file will also work.
	1. The first layer will contain the meshes to be split
	1. The second layer the meshes that do the splitting.
	1. Any other layers will be ignored.
1. Using the `_Notes` command, type use the following pattern:
    1. The first line should be "MEASURED SPLITS"
	1. Any line that begins with # will be considered a comment
	1. A list of the areas of the resulting meshes sorted smallest to largest
	1. After each value the open/closed flag can be used.
```
MEASURED SPLITS
# This is another comment
# You can link to discourse and YT here: https://discourse.mcneel.com/t/mesh-split-for-sneeze-cfd-not-working/99761
# RH-57844

152382.474 closed
564.53861
```
When the model is placed in a watched directory, the testing system will automatically load the file, perform splits, and check that the areas and properties match the specifications.

#### To add a new `_MeshBooleanxxxx` test: ####
This logic is used for `MeshBooleanUnion`, `MeshBooleanDifference`, `MeshBooleanSplit`
1. Create a .3dm model with the geometry.
1. Purge all redundant geometry, plug-in data, materials, etc that does not need to be in the model to speed up file loading during each test run.
1. Keep two layers: 
    1. By convention, they should be called `A` and `B`. Any other combination of the first two layers in the file will also work.
	1. The first layer will contain the meshes to be operated on.
	1. The second layer the meshes that operate on the first layer
	1. Any other layers will be ignored.
1. Using the `_Notes` command, type use the following pattern:
    1. The first line should be `AREA`
	1. Any line that begins with `#` will be considered a comment
	1. A list of the areas of the resulting meshes sorted smallest to largest
	1. Optionally, after each value the `open/closed` flag can be used.
	1. If the open/closed flag is used also optional mesh properties from the *What* command can be put in brackets. 
```
AREAS
# This is another comment
# You can link to discourse and YT here: https://discourse.mcneel.com/t/mesh-split-for-sneeze-cfd-not-working/99761
# RH-57844

467.95837
564.53861 closed
152382.474 closed [Valid mesh. Closed double precision polygon mesh: 40 vertices, 19 faces (3 n-gons) Bounding box: (-17.7135,-10.7285,0 to 13.1529,15.6631,26.5192)]
```

When the model is placed in a watched directory, the testing system will automatically load the file, perform splits, and check that the areas and properties match the specifications.

#### To add a new STEP import test: ####
STEP tests work like the ones above, except that the input is a `.stp` / `.step` file rather than a `.3dm`, so the expected values cannot live in the model's Notes. They live in a sidecar text file instead, named after the model:

```
as1-ac-214.stp
as1-ac-214.stp.expected.txt
```

1. Drop the STEP file in `models\STEPfile\`, the verified folder (see the `StepImport` entries in `MxTests.Rhino.Testing.Configs.xml`). Subfolders are scanned too, so `models\STEPfile\AP214\` and any other grouping you like both work.
1. Generate its baseline: run the `StepImport.Regenerate` test (it is `[Explicit]`, so it only runs when selected) with the environment variable `MX_STEP_REGEN` set to a substring of the file name, or to `*` for every model in the folder.
1. **Read the generated file before committing it.** Regeneration records whatever Rhino currently produces; it is your job to confirm that is right.
1. Trim the file down to the values you actually want to pin. Only the keys present are asserted, so a model can be held loosely or tightly.

The sidecar looks like this:
```
STEP IMPORT
# This is a comment
# You can link to discourse and YT here: https://discourse.mcneel.com/...
# RH-12345

units Millimeters
objects 18
instances 3
blockdefs 3
layers 4
breps 18
solids 15
invalid 0
area 128944.9427
volume 61233.882
bbox -50,-30,0 to 210,84.5,60
```

Available keys:

| Key | Meaning |
| --- | --- |
| `units` | Unit system the model was imported into. Always `Millimeters`: the test pins the document's units so baselines mean the same thing on every machine. |
| `objects` | Top level objects in the document. A block instance counts as one. |
| `instances` | Top level block instances. |
| `blockdefs` | Block definitions in the document. |
| `layers` | Layers in the document. |
| `breps`, `extrusions`, `surfaces`, `meshes`, `subds`, `curves`, `points`, `other` | Counts of leaf geometry, that is, after block instances are expanded. An assembly that arrives nested measures the same as the same assembly arriving flat. |
| `solids` | Leaf geometry that is a closed solid. |
| `invalid` | Leaf geometry that fails `IsValid`. Normally `0`. When it is not, the run names the offenders - geometry type, centre of the bounding box, and the `IsValidWithLog` reason - on the progress stream, in `MX_STEP_LOG`, and in the failure message if the count differs from the baseline. Up to ten are listed. |
| `area` | Total area of the leaf surfaces, breps and meshes. |
| `volume` | Total volume of the leaf solids. |
| `bbox` | Bounding box of everything, as `minX,minY,minZ to maxX,maxY,maxZ`. |

Counts are compared exactly. `area`, `volume` and each `bbox` coordinate are compared within `max(|expected| * 1e-8, 1e-6)`; both terms can be overridden with `MX_STEP_RELTOL` and `MX_STEP_ABSTOL`.

The `#` (skip) and `!` (expected to fail) file name prefixes work exactly as they do for the `.3dm` tests. When a test fails unexpectedly, whatever was imported is saved beside the model as `#name.3dm` so it can be opened.

**The three folders.** STEP models are split the same way the mesh boolean suites are split, one fixture per folder:

| Folder | Fixture | Runs by default | Holds |
| --- | --- | --- | --- |
| `models\STEPfile\` | `StepImport` | yes | The verified models. Everything here imports correctly and is expected to keep doing so. |
| `models\STEPfile-future\` | `StepImportFuture` | no, `[Explicit]` | Models that do not import correctly yet. Their baselines say what the import *should* produce. |
| `models\STEPfile-large\` | `StepImportLarge` | no, `[Explicit]` | Assemblies of hundreds of megabytes. |

`StepImportFuture` is the counterpart of the `-future` folders next to the mesh suites, and it is meant to be run on purpose - select it in Test Explorer, or use `--filter "FullyQualifiedName~StepImportFuture"`. A model that comes up green there has been fixed: move it and its `.expected.txt` into `models\STEPfile\` so that it starts guarding the fix. Because its baselines describe the wanted result rather than the current one, regenerating one replaces the goal with the bug; use `MX_STEP_REGEN_DRYRUN=1` to look without writing.

`StepImportLarge` is kept out of a normal run because a single one of its models can take minutes and gigabytes of memory. Those models are not committed either - `models\STEPfile-large\` is git-ignored apart from the baselines - and the fixture simply finds nothing and stays quiet when they are absent. New baselines there default to counts and bounding box, with no mass properties, because computing area and volume over a full vehicle assembly costs far more than the import does. Regenerate one with `MX_STEP_REGEN_FIELDS=ALL` if the extra confidence is worth the wait.

**Regeneration environment variables:**

| Variable | Effect |
| --- | --- |
| `MX_STEP_REGEN` | Comma separated substrings of file names to regenerate; `*` means all. Nothing happens without it. |
| `MX_STEP_REGEN_FIELDS` | `ALL` writes every key, `COUNTS` writes everything but area and volume. Unset keeps the keys the existing sidecar already declares, and uses the fixture default for a new one. |
| `MX_STEP_REGEN_DRYRUN` | `1` reports the before/after without writing anything. |
| `MX_STEP_REGEN_LOG` | Where the before/after report is appended. Defaults to `%TEMP%\mx_step_regen_report.txt`. |
| `MX_STEP_LOG` | Where each run's measurements and import time are appended. Defaults to `%TEMP%\mx_step.txt`. |

#### To add a new STEP export test: ####

Export is tested as a **round trip**: open the source model, write it out through Rhino's STEP writer, read the result back in, and compare both ends. That is what makes it checkable without hand-authoring a reference STEP file for every model - and comparing the two ends against each other is more informative than either alone, because the difference between them *is* what the export did.

The sidecar is named `.exported.txt`, so an import baseline and an export baseline sit side by side without colliding:

```
as1-ac-214.stp
as1-ac-214.stp.expected.txt     <- StepImport
as1-ac-214.stp.exported.txt     <- StepExport
```

1. Drop the source model in `models\STEPfile-export\` if it is a `.3dm`. STEP sources need nothing: `StepExport` already scans `models\STEPfile\`, so every model the import suite guards is round tripped too.
1. Generate its baseline: run the `StepExport.Regenerate` test (`[Explicit]`) with `MX_STEPEXPORT_REGEN` set to a substring of the file name, or to `*` for the whole folder.
1. **Read the generated file before committing it.** The `src*` keys are the model as it went in and the unprefixed keys are the model as it came back; a baseline that records a loss you did not intend is telling you something.
1. Trim it to what you actually want to pin, and set the write options you want to cover.

The sidecar looks like this:

```
STEP EXPORT
# RH-12345

schema AP214
export2dcurves false
exportblack true
splitclosedsurfaces false
fileschema AUTOMOTIVE_DESIGN

srcobjects 10
srcmeshes 1
srcarea 3034.8671465679704

objects 1
meshes 0
area 2434.8671428461462
```

Beyond the import keys, which all work here on both sides of the trip, an export sidecar has:

| Key | Meaning |
| --- | --- |
| `schema` | Which schema to write: `AP203`, `AP214`, `AP214_CC2` or `AP242`. Defaults to `AP214` - note that `FileStpWriteOptions` itself defaults to AP203, which the suite deliberately does not inherit. |
| `export2dcurves`, `exportblack`, `splitclosedsurfaces` | The remaining `FileStpWriteOptions` flags. All three are pinned in every baseline rather than inherited, so a moved default shows up as a diff. |
| `fileschema` | What the written file's `FILE_SCHEMA` header actually declares. Compared by containment, so a baseline may pin the whole stamp (`AP242_MANAGED_MODEL_BASED_3D_ENGINEERING_MIM_LF { 1 0 10303 442 3 1 4 }`) or just `AP242`. |
| `src` + any import key | The measurement of the **source**, before the export. `srcbreps`, `srcarea`, `srcbbox` and so on. |
| any import key | The same measurement of what came **back** from the round trip. |

A model with no sidecar still runs, and still gets the checks that need no baseline: `FileStp.Write` returned true, it wrote a non-empty file, the file opens with `ISO-10303-21;`, declares a `FILE_SCHEMA`, ends with `END-ISO-10303-21;`, and imports again into at least one object. The run says so on the progress stream - `no baseline: Part 21 invariants only` - and asserts nothing about the geometry. Set `MX_STEPEXPORT_REQUIRE_BASELINE=1` to turn a missing sidecar into a failure instead.

The `#` and `!` file name prefixes work as everywhere else. When a test fails, the STEP file that was written is kept beside the model as `#name.exported.stp` and what it read back is saved as `#name.exported.3dm`, so both ends of the failure can be opened. `MX_STEPEXPORT_KEEP=1` keeps the written file even on success.

**The folders:**

| Folder | Fixture | Runs by default | Holds |
| --- | --- | --- | --- |
| `models\STEPfile\` | `StepExport` | yes | Shared with `StepImport`. A file worth guarding on the way in is worth guarding on the way out. |
| `models\STEPfile-export\` | `StepExport` | yes | `.3dm` sources. The only way to put Rhino-native geometry - extrusions, blocks, open surfaces, meshes - through the writer, since a STEP source can only ever hand it what a STEP reader produced. |
| `models\STEPfile-export-future\` | `StepExportFuture` | no, `[Explicit]` | Models that do not survive the round trip yet. |
| `models\STEPfile-large\` | `StepExportLarge` | no, `[Explicit]` | Shared with `StepImportLarge`. Three times the cost, since it is an import, an export and a second import. |

`models\STEPfile-export\rhino-native-mix.3dm` is generated rather than drawn: `MxTests\AuthorExportSource.cs` is the readable statement of what is in it and the way to extend it. It carries a solid, a capped cylinder, an extrusion, an open surface, curves, a point, a mesh and a block inserted twice, and its baseline records what happens to each.

**Regeneration environment variables** mirror the import ones exactly, on their own names so that regenerating one suite never quietly rewrites the other: `MX_STEPEXPORT_REGEN`, `MX_STEPEXPORT_REGEN_FIELDS`, `MX_STEPEXPORT_REGEN_DRYRUN`, `MX_STEPEXPORT_REGEN_LOG`, `MX_STEPEXPORT_LOG`. The comparison tolerances are shared with the import suite: `MX_STEP_RELTOL` and `MX_STEP_ABSTOL`.


### Notes on inner mechanics ###

- Internally, all tests use: `NUnit.Framework.Assert.IsTrue`, `NUnit.Framework.Assert.AreEqual`, `NUnit.Framework.Assert.IsEmpty`, etc.
- You can debug tests using `Tests -> Debug All Tests`.

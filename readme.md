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

#### To add a new `_MeshIntersect` test: ####
1. Create a .3dm model with the geometry.
1. Purge all redundant geometry, plug-in data, materials, etc that does not need to be in the model to speed up file loading during each test run.
1. Keep two or more intersecting meshes.
   / Alternatively, some curves can be kept. They will be transformed into extrusion meshes with the same logic that applies to the MeshIntersect command.
1. Using the `_Notes` command, type notes following exactly this pattern:
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
1. Keep two layers: by convention, they should be called `A` and `B`. Any other combination will also work.
   / The first layer will contain the meshes to be split, the second the meshes that split.
1. Using the `_Notes` command, type notes following exactly this pattern:
```
MEASURED SPLITS
# This is another comment
# You can link to discourse and YT here: https://discourse.mcneel.com/t/mesh-split-for-sneeze-cfd-not-working/99761
# RH-57844

152382.474 closed
564.53861
```
5. The open/closed combos are optional.
1. When the model is placed in a watched directory, the testing system will automatically load the file, perform splits, and check that the areas and properties match the specifications.

| :warning: When a file in a watched folder starts with an exclamation mark, `!`, the test will run but will be expected to fail. It is considered a failure of this test, the fact that the test later does not fail. On the contrary, when the name begins with a hash sigh `#`, the test will be skipped. |
| :-- |
| :gem: When a test fails and that wasn't expected, a debug file with the resulting geometry is created, and its name will be prefixed by the hash sign `#`. The result is added to the `DEBUG` layer. |

### Notes on inner mechanics ###

- Internally, all tests use: `NUnit.Framework.Assert.IsTrue`, `NUnit.Framework.Assert.AreEqual`, `NUnit.Framework.Assert.IsEmpty`, etc.
- You can debug tests using `Tests -> Debug All Tests`.

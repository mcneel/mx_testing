# STEP models that do not import correctly yet #

The counterpart of `MeshBooleanUnion-future` and friends, for the `StepImport` suite.

A model belongs here when its `.expected.txt` says what the import **should** produce and Rhino does
not produce it. These are bugs waiting to be fixed, not regressions to guard, so the `StepImportFuture`
fixture is `[Explicit]`: a normal Run All Tests never touches it.

Run it deliberately to see where things stand:

```
dotnet test --filter "FullyQualifiedName~StepImportFuture"
```

A model that comes up **green has been fixed**. Move it and its `.expected.txt` into
`models\STEPfile\`, where it will start guarding the fix from then on.

The baselines in this folder are written by hand, or copied from a Rhino that got the model right.
They describe the wanted result, not the current one, so regenerating one replaces the goal with the
bug. Use `MX_STEP_REGEN_DRYRUN=1` to see what Rhino currently produces without writing anything.

See the repository readme for the file format and the full set of keys.

## What is in here ##

| Model | Symptom |
| --- | --- |
| `AP214\d2-db-214.stp` | Imports nothing at all. `FileStp.Read` returns true and the document has zero objects, although the file holds a `MANIFOLD_SOLID_BREP` on its `*MASTER` shape representation. Its product definition shape carries three `DRAUGHTING_MODEL`s alongside the brep representation, which the file's baseline comments explain in full. |
| `NIST_FTC\nist_ftc_06_asme1_rd.stp` | Imports all 144 faces of a `CLOSED_SHELL` but leaves one of them open. Exactly one naked edge remains -- the top rim of a 7.1374 mm cylindrical hole -- so the brep is not a solid and its volume measures 0. Nothing was built where the mating boundary should be. The file's baseline comments give the edge indices and coordinates. |

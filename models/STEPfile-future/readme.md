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

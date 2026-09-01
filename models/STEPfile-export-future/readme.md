# Models that do not survive a STEP export round trip yet #

The export counterpart of `models\STEPfile-future\`, for the `StepExport` suite.

A model belongs here when its `.exported.txt` says what the round trip **should** produce and Rhino
does not produce it. These are bugs waiting to be fixed, not regressions to guard, so the
`StepExportFuture` fixture is `[Explicit]`: a normal Run All Tests never touches it.

Run it deliberately to see where things stand:

```
dotnet test --filter "FullyQualifiedName~StepExportFuture"
```

A model that comes up **green has been fixed**. Move it and its `.exported.txt` into
`models\STEPfile-export\`, where it will start guarding the fix from then on.

The baselines in this folder are written by hand, or copied from a Rhino that got the model right.
They describe the wanted result, not the current one, so regenerating one replaces the goal with the
bug. Use `MX_STEPEXPORT_REGEN_DRYRUN=1` to see what Rhino currently produces without writing
anything.

Note that "the export is wrong" and "the re-import is wrong" both land here, because the round trip
cannot tell them apart on its own. Say which one it is in a comment at the top of the sidecar. If
the *source* is a STEP file that already fails to import, the model belongs in
`models\STEPfile-future\` instead — there is no export bug to see until the import works.

## What is in here ##

Nothing yet.

See the repository readme for the file format and the full set of keys.

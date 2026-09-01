# Source models for the STEP export round trip #

The `StepExport` fixture takes a model, writes it out through Rhino's STEP writer, reads the result
back in, and compares both ends against a `.exported.txt` sidecar.

It scans two folders. `models\STEPfile\` is the first: a file worth guarding on the way in is worth
guarding on the way out, so the whole import corpus gets round tripped for free. **This** folder is
the second, and it exists for the models that folder cannot hold — `.3dm` sources.

## Why `.3dm` sources matter ##

A STEP source can only ever hand the writer geometry a STEP reader produced: trimmed breps, and
blocks. The writer has to cope with everything a Rhino document can contain, and several of those
cases are only reachable from a `.3dm`:

| Case | Why it is worth a model |
| --- | --- |
| Extrusions | Rhino's own lightweight solid. The writer has to expand it. |
| Open and untrimmed surfaces | Not every export is a closed solid. |
| Closed surfaces | Exercises the `splitclosedsurfaces` option, which some receiving systems need. |
| Curves and points | Written as STEP geometric curve sets, or dropped. |
| Blocks, nested blocks | Assembly structure, and whether it survives the trip. |
| Layers, colours, black objects | Exercises the `exportblack` option. |
| Meshes | The exporter **skips** them (`WriteSTEPfile.cpp` counts them as `SkippedMeshCount` and moves on). A model that is part mesh, part brep pins that behaviour. |
| Annotation | Also skipped, deliberately. A model carrying dimensions pins that. |
| Non-manifold breps | Written as sets of unjoined surfaces rather than as a solid. |

Drop a `.3dm` here, generate its baseline, review it, commit both.

## Adding a model ##

```
MX_STEPEXPORT_REGEN=mymodel.3dm dotnet test --filter "FullyQualifiedName~StepExport.Regenerate"
```

Then read the `.exported.txt` it wrote before committing it. Its `src*` keys are the model as it
went in and its unprefixed keys are the model as it came back; the difference between the two
columns is exactly what the export did, so a baseline that records a loss you did not intend is
telling you something.

If the round trip is wrong and the model is a bug rather than a regression to guard, it belongs in
`models\STEPfile-export-future\` instead.

See the repository readme for the file format and the full set of keys.

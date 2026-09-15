# OBJ import and export tests — how they work, and how to add one

**Scope:** the OBJ tests in the `FileIO` project. Implementation lives in
`FileIO\WavefrontObj\ObjImportBase.cs` and `ObjExportBase.cs`. (The folder is `WavefrontObj`,
not `OBJ`, because a folder named `obj` inside a project IS MSBuild's intermediate directory on a
case-insensitive filesystem, and sources inside it are silently excluded from compilation.)

Fifth format on the shared pattern: fixtures `ObjImport`/`ObjExport` (+ `[Explicit]`
`-future`/`-large`), folders `models\OBJfile*`, extension `.obj` (+ `.3dm` in export folders),
namespaces `MX_OBJ_*` / `MX_OBJEXPORT_*`, slack `MX_OBJ_RELTOL`/`MX_OBJ_ABSTOL`.

## OBJ-specific behaviour worth knowing

**Both directions go through the managed RhinoCommon API** — `FileObj.Read` /
`FileObj.Write` with explicit options, never `RhinoDoc.Import`/`Export`: the OBJ plugin builds its
options from persisted machine Settings and never reads the dictionaries, and its Settings
defaults diverge from the API defaults (`MapZtoY` plugin=true vs API=false — a bare headless
`doc.Export` writes Y-up-flipped geometry on a virgin machine). The reader and writer are fully
managed C#, so **every option is a real effect** — the sidecar option keys are effective pins,
the full inverse of the IGES situation. `FileObj.Write` returns `WriteFileResult`, not bool. Two
landmines the runner hard-codes: `WriteOptions.SuppressAllInput = true`, and `ExportOpenMeshes`
stays true (false plus open geometry raises a message box). One reader nuance: its return value
reports whether objects were added, not whether the parse was clean.

**The import corpus is hand-authored text** — the only format where the committed models are
written by hand rather than generated: the text is the reviewable source, and it exercises dialect
features Rhino's writer never emits (negative/relative face indices, comma decimal separators,
trailing-backslash line continuations, byte-range vertex colors, `cstype bspline` free-form
blocks). The `quirks-continuation.obj` baseline's area 52.5 is the arithmetic proof that both the
continuation and the comma-decimal tolerance work.

**The structural checks** (no-baseline floor) come from the writer's own emission grammar: the
`# Rhino` header, CRLF discipline, ASCII-only bytes, the emitted keyword vocabulary,
v/vn/vt field arity, face-index validity against the v/vt/vn totals, per-line ref-format
consistency, and `.mtl` sidecar coherence (mtllib exists, every `usemtl` is declared by a
`newmtl`; and no mtllib at all when `exportmaterialdefinitions false` is pinned).

**Loss profile pinned by `rhino-native-mix-obj.3dm`:** blocks flatten (srcinstances 2 → 0), text
and dimensions are silently dropped (srcother 2 → 0), breps/extrusions arrive as meshes, and the
reader merges the mesh bodies. Area survives to meshing tolerance.

## Running

```bash
MSBuildEnableWorkloadResolver=false dotnet test FileIO/FileIO.csproj -f net48 --no-build --filter "FullyQualifiedName~FileIO.ObjImport"
MSBuildEnableWorkloadResolver=false dotnet test FileIO/FileIO.csproj -f net48 --no-build --filter "FullyQualifiedName~FileIO.ObjExport"
```

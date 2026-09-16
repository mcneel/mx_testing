# glTF import and export tests - how they work, and how to add one

**Scope:** the glTF tests in the `FileIO` project (`FileIO\Gltf\`). Eighth format on the shared
pattern: fixtures `GltfImport`/`GltfExport` (+ `[Explicit]` `-future`/`-large`), folders
`models\GLTFfile*`, extensions `.gltf` and `.glb`, namespaces `MX_GLTF_*` / `MX_GLTFEXPORT_*`.

## ⚠ Rhino cannot read the glTF Rhino writes

This is the finding that shapes the whole suite. Verified 2026-09-15 on Rhino 9 WIP 9.0.26258:

- every file written by `FileGltf.Write` comes back from `RhinoDoc.Import` as **false with zero
  objects**, in both `.gltf` and `.glb` form;
- a hand-authored minimal glTF - one triangle, one buffer, no extensions - **imports fine**, so
  the reader works and the plugin loads;
- it is not the KHR extensions the exporter declares: stripping `extensionsUsed` from a
  Rhino-written file does not help either.

Consequences for the suite, all deliberate:

1. **The import corpus is hand-authored**, like OBJ's. Rhino-written files cannot be import test
   material while they do not import.
2. **The export suite does not round trip.** It asserts the source measurement and the structural
   checks and stops. The flag is `GltfExportRunner.RoundTripBroken`; flip it to false when the
   reader is fixed - the read-back arm is written and waiting, and every export baseline will then
   need regenerating to gain its round-trip column.
3. **Rhino's own output is parked in `models\GLTFfile-future\`**, which is exactly what the
   future tier is for: models that do not import correctly yet. `gltf-box-no-extensions.gltf` in
   that folder is the probe that rules out the extensions.

## The structural checks

glTF has two on-disk forms and the extension picks the grammar:

- **`.glb`** is a binary container. The header declares the file's own length, and the chunks must
  tile the file exactly, so truncation is caught arithmetically rather than by guesswork. The
  first chunk must be the JSON chunk, and every chunk length must be 4-byte aligned.
- **`.gltf`** is bare JSON: braces and brackets must balance when string literals are ignored, the
  asset block must declare version 2.0, and every `uri` must be a `data:` URI - a .gltf that
  references a missing `.bin` is useless to whoever receives it.

Both forms additionally assert that the Draco extension appears if and only if
`usedracocompression` is pinned true, so the option cannot silently stop working.

## The corpus

`FileIO\Gltf\AuthorGltfCorpus.cs` still authors Rhino-written files - they are the evidence in
the `-future` folder - but the four models the import suite actually runs are hand-authored JSON:
a minimal triangle, an indexed quad, two meshes in one file, and a mesh carrying normals
alongside positions.

## Running

```bash
MSBuildEnableWorkloadResolver=false dotnet test FileIO/FileIO.csproj -f net48 --no-build --filter "FullyQualifiedName~FileIO.GltfImport"
MSBuildEnableWorkloadResolver=false dotnet test FileIO/FileIO.csproj -f net48 --no-build --filter "FullyQualifiedName~FileIO.GltfExport"
```

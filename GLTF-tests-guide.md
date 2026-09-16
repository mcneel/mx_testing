# glTF import and export tests - how they work, and how to add one

**Scope:** the glTF tests in the `FileIO` project (`FileIO\Gltf\`). Eighth format on the shared
pattern: fixtures `GltfImport`/`GltfExport` (+ `[Explicit]` `-future`/`-large`), folders
`models\GLTFfile*`, extensions `.gltf` and `.glb`, namespaces `MX_GLTF_*` / `MX_GLTFEXPORT_*`.

## ⚠ Headless import fails on glTF that carries materials (RH-81973)

This is the finding that shapes the suite. A glTF **with materials** cannot be imported back into
a headless document: `RhinoDoc.Import` returns false with zero objects. Isolated 2026-09-15 on
Rhino 9 WIP 9.0.26258:

- everything `FileGltf.Write` produces with its default `ExportMaterials = true` fails to
  re-import, in both `.gltf` and `.glb` form;
- the **same file imports fine** once its `materials` array and the primitives' `material`
  references are removed by hand;
- a hand-authored minimal glTF with no materials imports fine;
- it is **not** the KHR extensions the exporter declares - stripping `extensionsUsed` alone
  changes nothing.

That matches **RH-81973** (*GLB Import appears broken in SDK*, open): creating the PBR material
returns null unless the Commands plug-in is loaded, which in a headless host it is not. So this is
an existing known defect, not a new one, and not a property of the writer.

Consequences for the suite, all deliberate:

1. **`ExportMaterials` defaults to false here**, unlike RhinoCommon. With materials off the export
   round trips like every other format, so glTF is not a second-class suite.
2. **A materials-on export is still covered.** `models\GLTFfile-export\rhino-native-mix-gltf.3dm`
   pins `exportmaterials true`, and for that run the suite asserts the source measurement and the
   structural checks and stops - `GltfExportRunner.CanRoundTrip` makes that decision per model.
   When RH-81973 is fixed, flip that sidecar to `false`, regenerate, and it round trips too.
3. **The import corpus is hand-authored**, like OBJ's, and Rhino-written output lives in
   `models\GLTFfile-future\` together with the two probes that isolate the cause
   (`gltf-box-no-extensions.gltf` rules out the extensions; `probe-nomaterials.gltf` in the
   import folder is the same file with materials removed, which imports).

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
the `-future` folder - but the models the import suite actually runs are hand-authored JSON: a
minimal triangle, an indexed quad, two meshes in one file, a mesh carrying normals alongside
positions, and `probe-nomaterials.gltf`, which is Rhino's own output with its materials removed
and is therefore the direct demonstration of RH-81973.

One number worth not misreading: these models measure far larger than their stated coordinates.
glTF is metres by specification, so a 10 x 8 unit quad correctly arrives as 10 m x 8 m in the
suite's pinned millimetre document - an area of 8e7 mm2, not 80.

## Running

```bash
MSBuildEnableWorkloadResolver=false dotnet test FileIO/FileIO.csproj -f net48 --no-build --filter "FullyQualifiedName~FileIO.GltfImport"
MSBuildEnableWorkloadResolver=false dotnet test FileIO/FileIO.csproj -f net48 --no-build --filter "FullyQualifiedName~FileIO.GltfExport"
```

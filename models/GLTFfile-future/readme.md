# glTF models that do not import correctly yet #

Same semantics as the other `-future` folders, for the `GltfImportFuture` fixture (`[Explicit]`).
Baselines state the wanted result, not the current one; a model that comes up green has been
fixed - move it and its sidecar into the verified folder. Use `MX_GLTF_REGEN_DRYRUN=1` to look without writing.

## What is in here ##

**Everything Rhino's own glTF exporter writes.** Rhino cannot import the glTF it exports -
verified on 9.0.26258 for both .gltf and .glb, while hand-authored glTF imports fine - so these
files sit here as the standing record of that defect rather than as import test material.

`gltf-box-no-extensions.gltf` is Rhino's output with `extensionsUsed` stripped: it fails too,
which is what rules the KHR extensions out as the cause.

These have no baselines. Write one by hand stating what the import *should* produce if you want
the fixture to assert rather than merely fail.

See `GLTF-tests-guide.md` at the repository root.

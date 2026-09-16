# glTF models that do not import correctly yet #

Same semantics as the other `-future` folders, for the `GltfImportFuture` fixture (`[Explicit]`).
Baselines state the wanted result, not the current one; a model that comes up green has been
fixed - move it and its sidecar into the verified folder. Use `MX_GLTF_REGEN_DRYRUN=1` to look without writing.

## What is in here ##

**Rhino's own glTF output, which carries materials.** Headless import fails on glTF with
materials - **RH-81973** - because creating the PBR material returns null unless the Commands
plug-in is loaded. These files sit here as the standing record of that defect rather than as
import test material.

Two probes pin the cause down: `gltf-box-no-extensions.gltf` is Rhino's output with
`extensionsUsed` stripped and it still fails, ruling out the KHR extensions; while the same file
with its `materials` array and primitive material references removed **does** import, and lives in
`models\GLTFfile\probe-nomaterials.gltf` as a passing test.

These have no baselines. Write one by hand stating what the import *should* produce if you want
the fixture to assert rather than merely fail.

See `GLTF-tests-guide.md` at the repository root.

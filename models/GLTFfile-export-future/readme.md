# Models that do not survive a glTF export round trip yet #

Same semantics as the other `-future` folders, for the `GltfExportFuture` fixture (`[Explicit]`).
Baselines state the wanted result, not the current one; a model that comes up green has been
fixed - move it and its sidecar into the verified folder. Use `MX_GLTFEXPORT_REGEN_DRYRUN=1` to look without writing.

## What is in here ##

Nothing yet - and note that while `GltfExportRunner.RoundTripBroken` is true the export suite
does not round trip at all, so there is nothing for this folder to catch. It becomes useful the
day the reader is fixed and the flag flips.

See `GLTF-tests-guide.md` at the repository root.

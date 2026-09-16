# DWG import and export tests — how they work, and how to add one

**Scope:** the DWG tests in the `FileIO` project. Implementation lives in
`FileIO\DWG\DwgImportBase.cs` and `FileIO\DWG\DwgExportBase.cs`.

DWG is the fourth format on the shared pattern (see `STEP-tests-guide.md` for the machinery and
`DXF-tests-guide.md` for everything `FileDwg`-related): fixtures `DwgImport`/`DwgExport`
(+ `[Explicit]` `-future`/`-large`), folders `models\DWGfile*`, extension `.dwg`
(+ `.3dm` in export folders), env namespaces `MX_DWG_*` / `MX_DWGEXPORT_*`, tolerance knobs
`MX_DWG_RELTOL`/`MX_DWG_ABSTOL`.

**DWG and DXF share one plugin, one API and one options class — and are deliberately fully
separate suites** (own corpus, folders, fixtures, namespaces; no shared models). Everything the
DXF guide says about `FileDwg.Read`/`FileDwg.Write`, the near-empty-dictionary import quirk, the
bare-`doc.Export`-writes-2007 divergence, the headless unit-scale gap, and the three broken write
option keys (`simplifytolerance`, `curveusemaxangle`, `meshtype 3dface` — RH-98711) applies
verbatim here.
The export sidecar reuses the DXF option vocabulary unchanged. One extra caution from the shared
plumbing: the two dead keys fall back to the machine's *persisted export scheme*, not a compiled
default — the corpus keeps `Simplify=false` everywhere so `simplifytolerance` is inert.

**The structural checks** are byte-level on the binary header (R2000+ layout):

- bytes 0-5 are the ASCII version magic matching the pinned `acadversion` (AC1032 for 2018,
  AC1015 for 2000, …) — this doubles as the live probe that the Version option plumbs, and the
  corpus carries `dwg-r2000.dwg` as its witness;
- five zero pad bytes behind the magic; a sane preview pointer (zero, or an in-file offset
  carrying the 16-byte preview sentinel); codepage 30 (CP_ANSI_1252, which the writer sets
  explicitly); a 1 KB minimum size; and a cheap "not actually a DXF" negative.

Section-map decode and header CRCs are deliberately out of scope (compressed/obfuscated).

**The corpus** (`FileIO\DWG\AuthorDwgCorpus.cs`, re-author with `MX_DWG_AUTHOR_OUT_DIR=models`
then `MX_DWG_REGEN=*` / `MX_DWGEXPORT_REGEN=*`, and read every sidecar): a curve zoo, ACIS solids
(box/cylinder/sphere re-import **closed** — `solids 3`), the same box under the default
surfaces-as-curves (24 wireframe curves out — the headline default loss), polyface meshes
(survive, closed), nested + repeated blocks (assembly structure survives), annotations/nested
layers/colors, and the curve zoo re-written as ACAD 2000. The export source
`rhino-native-mix-dwg.3dm` pins the default loss profile: breps/extrusion collapse to wireframe
(curves 5 → 55), the mesh and blocks survive, and the only surviving area/volume is the mesh's.
It also carries one object whose print color differs from its display color — inert at the
default `UseColor=USEDISPLAY`, a loud diff if that default ever moves.

## Running

```bash
MSBuildEnableWorkloadResolver=false dotnet test FileIO/FileIO.csproj -f net48 --no-build --filter "FullyQualifiedName~FileIO.DwgImport"
MSBuildEnableWorkloadResolver=false dotnet test FileIO/FileIO.csproj -f net48 --no-build --filter "FullyQualifiedName~FileIO.DwgExport"
```

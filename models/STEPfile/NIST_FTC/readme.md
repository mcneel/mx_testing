# NIST MBE PMI FTC models #

Geometry-only STEP files from the [NIST MBE PMI Validation and Conformance Testing
Project](https://pages.nist.gov/CAD-PMI-Testing/models.html), Fixed Test Cases (FTC).

They are worth having in the corpus for two reasons:

- **They are AP203**, `CONFIG_CONTROL_DESIGN` and
  `AP203_CONFIGURATION_CONTROLLED_3D_DESIGN_OF_MECHANICAL_PARTS_AND_ASSEMBLIES_MIM_LF`. The rest of
  `models\STEPfile\` is AP214, so before these the reader's AP203 path was untested.
- **They are authored in inches**, so every one of them exercises the unit conversion into the
  suite's pinned-millimetre document. The AP214 corpus is millimetres throughout.

Each is a single `MANIFOLD_SOLID_BREP` over a `CLOSED_SHELL`. FTC-06 and FTC-09 also carry a
`GEOMETRIC_CURVE_SET` of supplemental geometry (datum targets and the like), which is why their
baselines show curves and points beside the brep.

| Model | What it is |
| --- | --- |
| `nist_ftc_07_asme1_rd.stp` | Solid, 1 brep. |
| `nist_ftc_08_asme1_rc.stp` | Solid, 1 brep. |
| `nist_ftc_09_asme1_rd.stp` | Solid plus 3 supplemental curves on 3 layers. The round trip drops one layer. |
| `nist_ftc_10_asme1_rb.stp` | Solid, 1 brep. |
| `nist_ftc_11_asme1_rb.stp` | Small plate, 7.6 KB - the cheapest STEP import test in the suite. |

`nist_ftc_06_asme1_rd.stp` is **not** here: it imports with a naked edge and so is not a solid. It
sits in `models\STEPfile-future\NIST_FTC\` with a baseline describing what it should produce.

The PDFs, the PMI spreadsheet and the FTC-06 file that ship alongside these in the NIST download are
not committed - only the STEP files the tests read.

## Provenance ##

Developed at NIST by federal employees in the course of their official duties, and therefore in the
public domain under 17 U.S.C. 105. NIST makes no guarantees about the models and takes no
responsibility for their use by other parties; see <https://www.nist.gov/disclaimer> and the
`README.txt` in the NIST download for the full statement.

NIST asks to be acknowledged when the models are used, and asks that its logo not be used.

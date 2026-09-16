using NUnit.Framework;
using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using System;
using System.Drawing;
using System.IO;

namespace FileIO
{
  /// <summary>
  /// Authors the committed SketchUp import corpus in <c>models\SKPfile\</c> and the export
  /// suite's <c>.3dm</c> source in <c>models\SKPfile-export\</c>.
  /// </summary>
  /// <remarks>
  /// SketchUp is a surface modeller: everything is faces and edges, so Rhino's breps arrive as
  /// meshes and its curves arrive as edges (or not at all, depending on the reader's ImportCurves
  /// option, which this suite pins). Unlike 3MF and glTF, SKP has a real read API with options,
  /// so the import side is fully pinned rather than inheriting machine state.
  ///
  /// The corpus deliberately includes an older-version write: the SketchUp version travels in the
  /// file's own header, so a file pinned to an older release is the live probe that the Version
  /// write option actually reaches the writer.
  ///
  /// [Explicit] twice over; refuses to run without MX_SKP_AUTHOR_OUT_DIR:
  /// <code>
  /// MX_SKP_AUTHOR_OUT_DIR=models dotnet test --filter "FullyQualifiedName~AuthorSkpCorpus"
  /// </code>
  /// </remarks>
  [TestFixture, Explicit]
  public class AuthorSkpCorpus
  {
    [Test, Explicit]
    public void Write()
    {
      string outDir = System.Environment.GetEnvironmentVariable("MX_SKP_AUTHOR_OUT_DIR");
      Assert.IsFalse(string.IsNullOrWhiteSpace(outDir),
        "Set MX_SKP_AUTHOR_OUT_DIR to the repository's models folder. This test overwrites the " +
        "corpus, so it will not guess a path - see the remarks on AuthorSkpCorpus.");

      string importDir = Path.Combine(outDir, "SKPfile");
      string exportDir = Path.Combine(outDir, "SKPfile-export");
      Directory.CreateDirectory(importDir);
      Directory.CreateDirectory(exportDir);

      // 1. A closed mesh box: the minimal file, and a closed body whose volume the baseline pins.
      Author(Path.Combine(importDir, "skp-box.skp"), null, doc =>
      {
        doc.Objects.AddMesh(Mesh.CreateFromBox(new BoundingBox(0, 0, 0, 10, 8, 5), 1, 1, 1));
      });

      // 2. NURBS in: SketchUp has no surface representation, so the writer tessellates. MaxAngle
      //    governs how finely, which is why the cylinder is here rather than another box.
      Author(Path.Combine(importDir, "skp-from-nurbs.skp"), null, doc =>
      {
        doc.Objects.AddBrep(Brep.CreateFromBox(new BoundingBox(0, 0, 0, 12, 6, 4)));
        doc.Objects.AddBrep(Brep.CreateFromCylinder(
          new Cylinder(new Circle(new Plane(new Point3d(30, 0, 0), Vector3d.ZAxis), 5), 12), true, true));
      });

      // 3. Two bodies on named layers: SketchUp carries layers, so the structure has somewhere
      //    to go - contrast with STL, where it does not.
      Author(Path.Combine(importDir, "skp-layers.skp"), null, doc =>
      {
        int a = doc.Layers.Add("SkpA", Color.Firebrick);
        int b = doc.Layers.Add("SkpB", Color.SteelBlue);

        doc.Objects.AddMesh(Mesh.CreateFromBox(new BoundingBox(0, 0, 0, 6, 6, 6), 1, 1, 1),
          new ObjectAttributes { LayerIndex = a });
        doc.Objects.AddMesh(Mesh.CreateFromBox(new BoundingBox(10, 0, 0, 16, 6, 6), 1, 1, 1),
          new ObjectAttributes { LayerIndex = b });
      });

      // 4. The same box written for an older SketchUp. The version lands in the file header, so
      //    this model is the corpus witness that the Version option is not being ignored.
      Author(Path.Combine(importDir, "skp-2014.skp"),
        o => o.Version = FileSkpWriteOptions.SketchUpVersion.SketchUp2014, doc =>
      {
        doc.Objects.AddMesh(Mesh.CreateFromBox(new BoundingBox(0, 0, 0, 10, 8, 5), 1, 1, 1));
      });

      AuthorExportSource3dm(Path.Combine(exportDir, "rhino-native-mix-skp.3dm"));
    }

    static void Author(string outPath, Action<FileSkpWriteOptions> configure, Action<RhinoDoc> build)
    {
      RhinoDoc doc = RhinoDoc.CreateHeadless(null);
      try
      {
        doc.ModelUnitSystem = UnitSystem.Millimeters;
        doc.ModelAbsoluteTolerance = 0.001;

        build(doc);
        Assert.Greater(doc.Objects.Count, 0, $"nothing was built for '{outPath}'.");

        var options = new FileSkpWriteOptions();
        configure?.Invoke(options);

        Assert.IsTrue(FileSkp.Write(outPath, doc, options), $"FileSkp.Write('{outPath}') returned false.");

        TestContext.Progress.WriteLine(
          $"[MXSKP] wrote '{outPath}': {doc.Objects.Count} objects, {new FileInfo(outPath).Length} bytes.");
      }
      finally
      {
        doc.Dispose();
      }
    }

    static void AuthorExportSource3dm(string outPath)
    {
      RhinoDoc doc = RhinoDoc.CreateHeadless(null);
      try
      {
        doc.ModelUnitSystem = UnitSystem.Millimeters;
        doc.ModelAbsoluteTolerance = 0.001;

        int solids = doc.Layers.Add("Solids", Color.SteelBlue);
        int facets = doc.Layers.Add("Facets", Color.SeaGreen);
        int wires = doc.Layers.Add("Wires", Color.Firebrick);

        doc.Objects.AddBrep(Brep.CreateFromBox(new BoundingBox(0, 0, 0, 12, 8, 5)),
          new ObjectAttributes { LayerIndex = solids });
        doc.Objects.AddExtrusion(
          Extrusion.Create(new Circle(new Plane(new Point3d(30, 0, 0), Vector3d.ZAxis), 4).ToNurbsCurve(), 10, true),
          new ObjectAttributes { LayerIndex = solids });
        doc.Objects.AddMesh(Mesh.CreateFromBox(new BoundingBox(50, 0, 0, 58, 8, 5), 1, 1, 1),
          new ObjectAttributes { LayerIndex = facets });

        // Curves feed the reader's ImportCurves option (pinned off by default), and the text has
        // no SketchUp representation at all.
        doc.Objects.AddCurve(new LineCurve(new Point3d(0, 20, 0), new Point3d(10, 20, 0)),
          new ObjectAttributes { LayerIndex = wires });
        doc.Objects.AddPoint(new Point3d(20, 20, 0), new ObjectAttributes { LayerIndex = wires });
        doc.Objects.AddText(
          new Rhino.Display.Text3d("mx skp src", new Plane(new Point3d(0, 30, 0), Vector3d.ZAxis), 3),
          new ObjectAttributes { LayerIndex = wires });

        // SuppressAllInput/SuppressDialogBoxes are not optional here - see AuthorStlCorpus and
        // STL-tests-guide.md: without them a re-author blocks forever in an unkillable host.
        Assert.IsTrue(doc.WriteFile(outPath, new FileWriteOptions { FileVersion = 7, SuppressAllInput = true, SuppressDialogBoxes = true }),
          $"RhinoDoc.WriteFile('{outPath}') returned false.");

        TestContext.Progress.WriteLine(
          $"[MXSKPEX] wrote '{outPath}': {doc.Objects.Count} objects. " +
          "Regenerate its baseline next: MX_SKPEXPORT_REGEN=rhino-native-mix-skp.");
      }
      finally
      {
        doc.Dispose();
      }
    }
  }
}

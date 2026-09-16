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
  /// Authors the committed 3MF import corpus in <c>models\3MFfile\</c> and the export suite's
  /// <c>.3dm</c> source in <c>models\3MFfile-export\</c>.
  /// </summary>
  /// <remarks>
  /// 3MF is a manufacturing format: an OPC package (a ZIP) carrying triangle meshes with units,
  /// colours and a build plate. What it deliberately cannot carry is as interesting as what it
  /// can - NURBS surfaces are meshed on the way out, and curves, points and annotations have no
  /// representation at all.
  ///
  /// [Explicit] twice over; refuses to run without MX_TMF_AUTHOR_OUT_DIR:
  /// <code>
  /// MX_TMF_AUTHOR_OUT_DIR=models dotnet test --filter "FullyQualifiedName~AuthorTmfCorpus"
  /// </code>
  /// </remarks>
  [TestFixture, Explicit]
  public class AuthorTmfCorpus
  {
    [Test, Explicit]
    public void Write()
    {
      string outDir = System.Environment.GetEnvironmentVariable("MX_TMF_AUTHOR_OUT_DIR");
      Assert.IsFalse(string.IsNullOrWhiteSpace(outDir),
        "Set MX_TMF_AUTHOR_OUT_DIR to the repository's models folder. This test overwrites the " +
        "corpus, so it will not guess a path - see the remarks on AuthorTmfCorpus.");

      string importDir = Path.Combine(outDir, "3MFfile");
      string exportDir = Path.Combine(outDir, "3MFfile-export");
      Directory.CreateDirectory(importDir);
      Directory.CreateDirectory(exportDir);

      // 1. A single closed mesh box: the minimal package, and a closed solid whose volume the
      //    baseline can pin exactly.
      Author(Path.Combine(importDir, "tmf-box.3mf"), doc =>
      {
        doc.Objects.AddMesh(Mesh.CreateFromBox(new BoundingBox(0, 0, 0, 10, 8, 5), 1, 1, 1));
      });

      // 2. Two disjoint bodies: 3MF keeps them as separate objects in the package, unlike STL
      //    where they collapse into one triangle soup and have to be split on the way back.
      Author(Path.Combine(importDir, "tmf-two-bodies.3mf"), doc =>
      {
        doc.Objects.AddMesh(Mesh.CreateFromBox(new BoundingBox(0, 0, 0, 8, 8, 8), 1, 1, 1));
        doc.Objects.AddMesh(Mesh.CreateFromBox(new BoundingBox(20, 0, 0, 28, 8, 8), 1, 1, 1));
      });

      // 3. A brep and an extrusion: NURBS has no 3MF representation, so the writer meshes them.
      //    The baseline records the brep-to-mesh conversion.
      Author(Path.Combine(importDir, "tmf-from-nurbs.3mf"), doc =>
      {
        doc.Objects.AddBrep(Brep.CreateFromBox(new BoundingBox(0, 0, 0, 12, 6, 4)));
        doc.Objects.AddExtrusion(
          Extrusion.Create(new Circle(new Plane(new Point3d(30, 0, 0), Vector3d.ZAxis), 4).ToNurbsCurve(), 10, true));
      });

      // 4. Colours on layers: 3MF carries colour, so this pins whether it survives the trip.
      Author(Path.Combine(importDir, "tmf-colors.3mf"), doc =>
      {
        int red = doc.Layers.Add("TmfRed", Color.Firebrick);
        int blue = doc.Layers.Add("TmfBlue", Color.SteelBlue);

        doc.Objects.AddMesh(Mesh.CreateFromBox(new BoundingBox(0, 0, 0, 6, 6, 6), 1, 1, 1),
          new ObjectAttributes { LayerIndex = red });
        doc.Objects.AddMesh(Mesh.CreateFromBox(new BoundingBox(10, 0, 0, 16, 6, 6), 1, 1, 1),
          new ObjectAttributes { LayerIndex = blue });
      });

      // 5. A sphere at negative coordinates. 3MF requires non-negative coordinates, so this is
      //    the model that exercises MoveOutputToPositiveXYZOctant - the one write option that
      //    changes geometry rather than metadata. The baseline's bbox records where it landed.
      Author(Path.Combine(importDir, "tmf-negative-octant.3mf"), doc =>
      {
        doc.Objects.AddMesh(Mesh.CreateFromSphere(new Sphere(new Point3d(-20, -15, -10), 5), 12, 8));
      });

      AuthorExportSource3dm(Path.Combine(exportDir, "rhino-native-mix-3mf.3dm"));
    }

    static void Author(string outPath, Action<RhinoDoc> build)
    {
      RhinoDoc doc = RhinoDoc.CreateHeadless(null);
      try
      {
        doc.ModelUnitSystem = UnitSystem.Millimeters;
        doc.ModelAbsoluteTolerance = 0.001;

        build(doc);
        Assert.Greater(doc.Objects.Count, 0, $"nothing was built for '{outPath}'.");

        Assert.IsTrue(File3mf.Write(outPath, doc, new File3mfWriteOptions()),
          $"File3mf.Write('{outPath}') returned false.");

        TestContext.Progress.WriteLine(
          $"[MXTMF] wrote '{outPath}': {doc.Objects.Count} objects, {new FileInfo(outPath).Length} bytes.");
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

        // Meshed on the way out.
        doc.Objects.AddBrep(Brep.CreateFromBox(new BoundingBox(0, 0, 0, 12, 8, 5)),
          new ObjectAttributes { LayerIndex = solids });
        doc.Objects.AddExtrusion(
          Extrusion.Create(new Circle(new Plane(new Point3d(30, 0, 0), Vector3d.ZAxis), 4).ToNurbsCurve(), 10, true),
          new ObjectAttributes { LayerIndex = solids });

        // Passes through as itself.
        doc.Objects.AddMesh(Mesh.CreateFromBox(new BoundingBox(50, 0, 0, 58, 8, 5), 1, 1, 1),
          new ObjectAttributes { LayerIndex = facets });

        // No 3MF representation: the baseline records these as src counts against nothing.
        doc.Objects.AddCurve(new LineCurve(new Point3d(0, 20, 0), new Point3d(10, 20, 0)),
          new ObjectAttributes { LayerIndex = wires });
        doc.Objects.AddPoint(new Point3d(20, 20, 0), new ObjectAttributes { LayerIndex = wires });
        doc.Objects.AddText(
          new Rhino.Display.Text3d("mx 3mf src", new Plane(new Point3d(0, 30, 0), Vector3d.ZAxis), 3),
          new ObjectAttributes { LayerIndex = wires });

        // SuppressAllInput/SuppressDialogBoxes are not optional here. RhinoDoc.WriteFile over an
        // EXISTING .3dm asks the user something; without them this [Explicit] author does not
        // fail, it blocks forever, and the blocked test host cannot be killed with Stop-Process
        // -Force or taskkill /F /T - the machine needs a reboot to clear it.
        Assert.IsTrue(doc.WriteFile(outPath, new FileWriteOptions { FileVersion = 7, SuppressAllInput = true, SuppressDialogBoxes = true }),
          $"RhinoDoc.WriteFile('{outPath}') returned false.");

        TestContext.Progress.WriteLine(
          $"[MXTMFEX] wrote '{outPath}': {doc.Objects.Count} objects. " +
          "Regenerate its baseline next: MX_TMFEXPORT_REGEN=rhino-native-mix-3mf.");
      }
      finally
      {
        doc.Dispose();
      }
    }
  }
}

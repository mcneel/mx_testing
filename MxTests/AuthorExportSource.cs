using NUnit.Framework;
using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using System.Drawing;
using System.IO;

namespace MxTests
{
  /// <summary>
  /// Authors <c>models\STEPfile-export\rhino-native-mix.3dm</c>, the export suite's purpose-made
  /// source model.
  /// </summary>
  /// <remarks>
  /// The model is committed, so nothing needs this to run the tests. It is here because the model
  /// is a *generated* artifact and a `.3dm` in Git LFS is opaque: this file is the readable
  /// statement of what is in it, and the way to extend it. Add geometry here, re-run, regenerate
  /// the baseline, review the diff, commit all three.
  ///
  /// Everything in the model is a case a STEP source file cannot produce. A STEP reader hands the
  /// writer trimmed breps and blocks, and nothing else; the writer has to cope with the whole of
  /// what a Rhino document can hold. What the round trip currently does with each is recorded in
  /// <c>rhino-native-mix.3dm.exported.txt</c> - most notably that the mesh is dropped, which shows
  /// there as <c>srcmeshes 1</c> against <c>meshes 0</c>, and as exactly 600 mm² of area and
  /// 900 mm³ of volume going missing.
  ///
  /// [Explicit] twice over, and it refuses to run without MX_STEPEXPORT_AUTHOR_OUT naming the
  /// output, so that it can never overwrite the committed model by accident:
  ///
  /// <code>
  /// MX_STEPEXPORT_AUTHOR_OUT=models/STEPfile-export/rhino-native-mix.3dm \
  ///   dotnet test --filter "FullyQualifiedName~AuthorExportSource"
  /// </code>
  /// </remarks>
  [TestFixture, Explicit]
  public class AuthorExportSource
  {
    [Test, Explicit]
    public void Write()
    {
      string outPath = System.Environment.GetEnvironmentVariable("MX_STEPEXPORT_AUTHOR_OUT");
      Assert.IsFalse(string.IsNullOrWhiteSpace(outPath),
        "Set MX_STEPEXPORT_AUTHOR_OUT to the .3dm to write. This test overwrites it, so it will not " +
        "guess a path - see the remarks on AuthorExportSource.");

      // Millimetres, and Rhino's default tolerance for them. The exporter writes in document units,
      // so the units the source is authored in are part of what the round trip is measuring.
      RhinoDoc doc = RhinoDoc.CreateHeadless(null);
      try
      {
        doc.ModelUnitSystem = UnitSystem.Millimeters;
        doc.ModelAbsoluteTolerance = 0.001;

        int solids = doc.Layers.Add("Solids", Color.SteelBlue);
        int shells = doc.Layers.Add("Shells", Color.Goldenrod);
        int wires = doc.Layers.Add("Wires", Color.Firebrick);
        int facets = doc.Layers.Add("Facets", Color.SeaGreen);

        // A plain solid brep: the baseline case the writer has to get right.
        doc.Objects.AddBrep(
          Brep.CreateFromBox(new BoundingBox(0, 0, 0, 20, 10, 5)),
          new ObjectAttributes { LayerIndex = solids });

        // A capped cylinder. Its lateral face is closed, which is what 'splitclosedsurfaces' acts
        // on - some receiving systems cannot read a seamed surface.
        doc.Objects.AddBrep(
          Brep.CreateFromCylinder(
            new Cylinder(new Circle(new Plane(new Point3d(40, 0, 0), Vector3d.ZAxis), 6), 15), true, true),
          new ObjectAttributes { LayerIndex = solids });

        // Rhino's own lightweight solid. STEP has no such thing, so the writer has to expand it,
        // and the round trip shows it coming back as a brep.
        doc.Objects.AddExtrusion(
          Extrusion.Create(
            new Circle(new Plane(new Point3d(70, 0, 0), Vector3d.ZAxis), 4).ToNurbsCurve(), 12, true),
          new ObjectAttributes { LayerIndex = solids });

        // An open, untrimmed surface: not every export is a closed solid.
        doc.Objects.AddSurface(
          NurbsSurface.CreateFromCorners(
            new Point3d(0, 30, 0), new Point3d(20, 30, 0), new Point3d(20, 45, 8), new Point3d(0, 45, 8)),
          new ObjectAttributes { LayerIndex = shells });

        // Wires and a point, written as STEP geometric curve sets.
        doc.Objects.AddCurve(
          new ArcCurve(new Arc(new Point3d(40, 30, 0), new Point3d(50, 38, 0), new Point3d(60, 30, 0))),
          new ObjectAttributes { LayerIndex = wires });

        // Black, to exercise the 'exportblack' option: with it off, this one should not appear.
        doc.Objects.AddCurve(
          new LineCurve(new Point3d(40, 45, 0), new Point3d(60, 45, 0)),
          new ObjectAttributes
          {
            LayerIndex = wires,
            ColorSource = ObjectColorSource.ColorFromObject,
            ObjectColor = Color.Black,
          });

        doc.Objects.AddPoint(new Point3d(70, 45, 0), new ObjectAttributes { LayerIndex = wires });

        // The exporter skips meshes on purpose - WriteSTEPfile.cpp counts them into
        // SkippedMeshCount and moves on. This box is 15 x 10 x 6, so the baseline records the drop
        // twice over: as meshes 1 -> 0, and as 600 mm2 of area and 900 mm3 of volume disappearing.
        doc.Objects.AddMesh(
          Mesh.CreateFromBox(new BoundingBox(0, 60, 0, 15, 70, 6), 1, 1, 1),
          new ObjectAttributes { LayerIndex = facets });

        // A block, inserted twice: does the assembly structure survive the trip?
        int idef = doc.InstanceDefinitions.Add(
          "Peg", "A pin, instanced twice", Point3d.Origin,
          new GeometryBase[]
          {
            Brep.CreateFromCylinder(
              new Cylinder(new Circle(new Plane(Point3d.Origin, Vector3d.ZAxis), 2), 6), true, true),
          },
          new ObjectAttributes[] { new ObjectAttributes { LayerIndex = solids } });

        doc.Objects.AddInstanceObject(idef, Transform.Translation(0, 80, 0));
        doc.Objects.AddInstanceObject(idef, Transform.Translation(12, 80, 0));

        Assert.IsTrue(doc.WriteFile(outPath, new FileWriteOptions { FileVersion = 7 }),
          $"RhinoDoc.WriteFile('{outPath}') returned false.");

        TestContext.Progress.WriteLine(
          $"[MXSTEPEX] wrote '{outPath}': {doc.Objects.Count} objects, {new FileInfo(outPath).Length} bytes. " +
          "Regenerate its baseline next: MX_STEPEXPORT_REGEN=rhino-native-mix.");
      }
      finally
      {
        doc.Dispose();
      }
    }
  }
}

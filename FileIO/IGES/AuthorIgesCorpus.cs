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
  /// Authors the committed IGES import corpus in <c>models\IGESfile\</c> and the export suite's
  /// purpose-made <c>.3dm</c> source in <c>models\IGESfile-export\</c>.
  /// </summary>
  /// <remarks>
  /// The corpus is committed, so nothing needs this to run the tests. It is here because the
  /// models are *generated* artifacts - written by Rhino's own IGES writer from geometry stated in
  /// this file - and that is a deliberate corpus choice: every file is unencumbered, reviewable as
  /// code, and reproducible. The trade-off is equally deliberate and worth knowing: a corpus
  /// written by the same product's writer only exercises the reader on Rhino's own IGES dialect.
  /// Files from other originators (CATIA, NX, SolidWorks) belong in the corpus too as they become
  /// available with clean redistribution rights - or in <c>private_models\models\IGESfile\</c>
  /// when they cannot be published.
  ///
  /// [Explicit] twice over, and it refuses to run without MX_IGES_AUTHOR_OUT_DIR naming the
  /// repository's <c>models\</c> folder, so that it can never overwrite the committed corpus by
  /// accident:
  ///
  /// <code>
  /// MX_IGES_AUTHOR_OUT_DIR=models dotnet test --filter "FullyQualifiedName~AuthorIgesCorpus"
  /// </code>
  ///
  /// After (re)authoring, regenerate the baselines (MX_IGES_REGEN=*, MX_IGESEXPORT_REGEN=*),
  /// review every sidecar, and commit models and sidecars together.
  /// </remarks>
  [TestFixture, Explicit]
  public class AuthorIgesCorpus
  {
    [Test, Explicit]
    public void Write()
    {
      string outDir = System.Environment.GetEnvironmentVariable("MX_IGES_AUTHOR_OUT_DIR");
      Assert.IsFalse(string.IsNullOrWhiteSpace(outDir),
        "Set MX_IGES_AUTHOR_OUT_DIR to the repository's models folder. This test overwrites the " +
        "corpus, so it will not guess a path - see the remarks on AuthorIgesCorpus.");

      string importDir = Path.Combine(outDir, "IGESfile");
      string exportDir = Path.Combine(outDir, "IGESfile-export");
      Directory.CreateDirectory(importDir);
      Directory.CreateDirectory(exportDir);

      // ----- Import corpus: each model is one writer configuration over stated geometry. -----

      // A solid box under the default SolidType=separate: the writer splits it into independent
      // trimmed faces and the reader's default join is what puts it back together.
      Author(Path.Combine(importDir, "solid-box.igs"), null, doc =>
      {
        doc.Objects.AddBrep(Brep.CreateFromBox(new BoundingBox(0, 0, 0, 20, 10, 5)));
      });

      // NOTE deliberately absent: a 186 Manifold B-Rep variant of the box. FileIgs.Write ignores
      // FileIgsWriteOptions today - the native writer builds its options from the plugin's current
      // options and never reads the Export dictionary (WriteIGESfile.cpp consumes
      // theLib.m_current_options; only units and tolerance follow the headless doc) - so a
      // "solid-box-186.igs" comes out byte-identical to solid-box.igs and its name would lie.
      // Add per-entity write-mode models here once the options plumbing works; the sidecar option
      // keys are already in place for that day.

      // A capped cylinder: a closed, seamed lateral face, the case 'splitclosedsurfaces' exists for.
      Author(Path.Combine(importDir, "cylinder-capped.igs"), null, doc =>
      {
        doc.Objects.AddBrep(Brep.CreateFromCylinder(
          new Cylinder(new Circle(new Plane(Point3d.Origin, Vector3d.ZAxis), 6), 15), true, true));
      });

      // Wires and points: lines, an arc, a freeform curve and both point spellings (116 vs 106).
      Author(Path.Combine(importDir, "curves-points.igs"), null, doc =>
      {
        doc.Objects.AddCurve(new LineCurve(new Point3d(0, 0, 0), new Point3d(20, 0, 0)));
        doc.Objects.AddCurve(new ArcCurve(new Arc(new Point3d(0, 10, 0), new Point3d(10, 18, 0), new Point3d(20, 10, 0))));
        doc.Objects.AddCurve(NurbsCurve.Create(false, 3, new Point3d[]
        {
          new Point3d(0, 25, 0), new Point3d(6, 30, 4), new Point3d(14, 22, -3), new Point3d(20, 28, 0)
        }));
        doc.Objects.AddPoint(new Point3d(25, 0, 0));
        doc.Objects.AddPoint(new Point3d(25, 5, 0));
      });

      // Surfaces open and trimmed: an untrimmed bilinear patch and a trimmed planar face.
      Author(Path.Combine(importDir, "surfaces-open-trimmed.igs"), null, doc =>
      {
        doc.Objects.AddSurface(NurbsSurface.CreateFromCorners(
          new Point3d(0, 0, 0), new Point3d(20, 0, 0), new Point3d(20, 15, 8), new Point3d(0, 15, 8)));

        var circle = new Circle(new Plane(new Point3d(40, 8, 0), Vector3d.ZAxis), 6);
        Brep[] trimmed = Brep.CreatePlanarBreps(new Curve[] { circle.ToNurbsCurve() }, 0.001);
        Assert.IsNotNull(trimmed, "CreatePlanarBreps failed while authoring surfaces-open-trimmed.igs");
        foreach (Brep b in trimmed) doc.Objects.AddBrep(b);
      });

      // Blocks inserted twice: IGES has no assembly structure under these settings, so this pins
      // the flattening - the import should measure the same leaves as a flat file.
      Author(Path.Combine(importDir, "blocks-flattened.igs"), null, doc =>
      {
        int idef = doc.InstanceDefinitions.Add(
          "Peg", "A pin, instanced twice", Point3d.Origin,
          new GeometryBase[]
          {
            Brep.CreateFromCylinder(
              new Cylinder(new Circle(new Plane(Point3d.Origin, Vector3d.ZAxis), 2), 6), true, true),
          },
          new ObjectAttributes[] { new ObjectAttributes() });

        doc.Objects.AddInstanceObject(idef, Transform.Translation(0, 0, 0));
        doc.Objects.AddInstanceObject(idef, Transform.Translation(12, 0, 0));
      });

      // ----- Export source: the Rhino-native cases the writer has to cope with. -----
      // The same reasoning as STEP's rhino-native-mix.3dm: an IGES source can only hand the writer
      // what an IGES reader produced, so the extrusion, the mesh, the point and the block can only
      // arrive from a .3dm. The mesh is the interesting one - the writer skips meshes by default
      // (MeshType=none), and the baseline pins that drop the same way STEP's pins its mesh skip.
      AuthorExportSource3dm(Path.Combine(exportDir, "rhino-native-mix-iges.3dm"));
    }

    static void Author(string outPath, Action<FileIgsWriteOptions> configure, Action<RhinoDoc> build)
    {
      RhinoDoc doc = RhinoDoc.CreateHeadless(null);
      try
      {
        doc.ModelUnitSystem = UnitSystem.Millimeters;
        doc.ModelAbsoluteTolerance = 0.001;

        build(doc);
        Assert.Greater(doc.Objects.Count, 0, $"nothing was built for '{outPath}'.");

        var options = new FileIgsWriteOptions
        {
          Units = UnitSystem.Millimeters,
          Tolerance = 0.001,
        };
        configure?.Invoke(options);

        Assert.IsTrue(FileIgs.Write(outPath, doc, options), $"FileIgs.Write('{outPath}') returned false.");

        TestContext.Progress.WriteLine(
          $"[MXIGES] wrote '{outPath}': {doc.Objects.Count} objects, {new FileInfo(outPath).Length} bytes.");
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
        int shells = doc.Layers.Add("Shells", Color.Goldenrod);
        int wires = doc.Layers.Add("Wires", Color.Firebrick);
        int facets = doc.Layers.Add("Facets", Color.SeaGreen);

        // A plain solid brep: the baseline case.
        doc.Objects.AddBrep(
          Brep.CreateFromBox(new BoundingBox(0, 0, 0, 20, 10, 5)),
          new ObjectAttributes { LayerIndex = solids });

        // A capped cylinder, whose closed lateral face is what 'splitclosedsurfaces' acts on.
        doc.Objects.AddBrep(
          Brep.CreateFromCylinder(
            new Cylinder(new Circle(new Plane(new Point3d(40, 0, 0), Vector3d.ZAxis), 6), 15), true, true),
          new ObjectAttributes { LayerIndex = solids });

        // Rhino's own lightweight solid: IGES has no such thing, the writer has to expand it.
        doc.Objects.AddExtrusion(
          Extrusion.Create(
            new Circle(new Plane(new Point3d(70, 0, 0), Vector3d.ZAxis), 4).ToNurbsCurve(), 12, true),
          new ObjectAttributes { LayerIndex = solids });

        // An open, untrimmed surface: not every export is a closed solid.
        doc.Objects.AddSurface(
          NurbsSurface.CreateFromCorners(
            new Point3d(0, 30, 0), new Point3d(20, 30, 0), new Point3d(20, 45, 8), new Point3d(0, 45, 8)),
          new ObjectAttributes { LayerIndex = shells });

        // Wires and a point.
        doc.Objects.AddCurve(
          new ArcCurve(new Arc(new Point3d(40, 30, 0), new Point3d(50, 38, 0), new Point3d(60, 30, 0))),
          new ObjectAttributes { LayerIndex = wires });
        doc.Objects.AddPoint(new Point3d(70, 45, 0), new ObjectAttributes { LayerIndex = wires });

        // The writer skips meshes by default (MeshType=none). This box is 15 x 10 x 6, so the
        // baseline records the drop twice over: as meshes 1 -> 0, and as 600 mm2 of area and
        // 900 mm3 of volume disappearing. Pinning 'meshtype 10613' in the sidecar instead turns
        // the same model into the mesh-export case.
        doc.Objects.AddMesh(
          Mesh.CreateFromBox(new BoundingBox(0, 60, 0, 15, 70, 6), 1, 1, 1),
          new ObjectAttributes { LayerIndex = facets });

        // A block, inserted twice: IGES flattens it, and the baseline pins that.
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

        // SuppressAllInput/SuppressDialogBoxes are not optional here. RhinoDoc.WriteFile over an
        // EXISTING .3dm asks the user something; without them this [Explicit] author does not
        // fail, it blocks forever, and the blocked test host cannot be killed with Stop-Process
        // -Force or taskkill /F /T - the machine needs a reboot to clear it. Writing to a fresh
        // folder hides this, which is exactly how it stayed hidden until a re-author.
        Assert.IsTrue(doc.WriteFile(outPath, new FileWriteOptions { FileVersion = 7, SuppressAllInput = true, SuppressDialogBoxes = true }),
          $"RhinoDoc.WriteFile('{outPath}') returned false.");

        TestContext.Progress.WriteLine(
          $"[MXIGESEX] wrote '{outPath}': {doc.Objects.Count} objects, {new FileInfo(outPath).Length} bytes. " +
          "Regenerate its baseline next: MX_IGESEXPORT_REGEN=rhino-native-mix-iges.");
      }
      finally
      {
        doc.Dispose();
      }
    }
  }
}

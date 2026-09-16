using NUnit.Framework;
using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using System.Drawing;
using System.IO;

namespace FileIO
{
  /// <summary>
  /// Authors the OBJ export suite's <c>.3dm</c> source in <c>models\OBJfile-export\</c>.
  /// </summary>
  /// <remarks>
  /// The OBJ *import* corpus is different from every other format's: OBJ is line-oriented text, so
  /// the committed models in <c>models\OBJfile\</c> are hand-authored files - the text itself is
  /// the reviewable statement of what is in them, and hand-authoring lets the corpus exercise
  /// dialect features Rhino's own writer never emits (negative indices, comma decimals, line
  /// continuations, byte-range vertex colors). Only the export source is generated here.
  ///
  /// [Explicit] twice over; refuses to run without MX_OBJ_AUTHOR_OUT_DIR:
  /// <code>
  /// MX_OBJ_AUTHOR_OUT_DIR=models dotnet test --filter "FullyQualifiedName~AuthorObjCorpus"
  /// </code>
  /// </remarks>
  [TestFixture, Explicit]
  public class AuthorObjCorpus
  {
    [Test, Explicit]
    public void Write()
    {
      string outDir = System.Environment.GetEnvironmentVariable("MX_OBJ_AUTHOR_OUT_DIR");
      Assert.IsFalse(string.IsNullOrWhiteSpace(outDir),
        "Set MX_OBJ_AUTHOR_OUT_DIR to the repository's models folder. This test overwrites the " +
        "export source, so it will not guess a path - see the remarks on AuthorObjCorpus.");

      string exportDir = Path.Combine(outDir, "OBJfile-export");
      Directory.CreateDirectory(exportDir);
      string outPath = Path.Combine(exportDir, "rhino-native-mix-obj.3dm");

      RhinoDoc doc = RhinoDoc.CreateHeadless(null);
      try
      {
        doc.ModelUnitSystem = UnitSystem.Millimeters;
        doc.ModelAbsoluteTolerance = 0.001;

        int solids = doc.Layers.Add("Solids", Color.SteelBlue);
        int facets = doc.Layers.Add("Facets", Color.SeaGreen);
        int wires = doc.Layers.Add("Wires", Color.Firebrick);
        int notes = doc.Layers.Add("Notes", Color.DarkSlateGray);

        // A closed brep box and an extrusion: meshed via MeshingParameters in the default Mesh
        // mode; in a Nurbs-mode baseline they become cstype bspline surf blocks instead.
        doc.Objects.AddBrep(Brep.CreateFromBox(new BoundingBox(0, 0, 0, 20, 10, 5)),
          new ObjectAttributes { LayerIndex = solids });
        doc.Objects.AddExtrusion(
          Extrusion.Create(new Circle(new Plane(new Point3d(40, 0, 0), Vector3d.ZAxis), 4).ToNurbsCurve(), 12, true),
          new ObjectAttributes { LayerIndex = solids });

        // An open trimmed surface: trim-loop writing in Nurbs mode, open-mesh path in Mesh mode.
        var circle = new Circle(new Plane(new Point3d(65, 5, 0), Vector3d.ZAxis), 5);
        Brep[] trimmed = Brep.CreatePlanarBreps(new Curve[] { circle.ToNurbsCurve() }, 0.001);
        Assert.IsNotNull(trimmed, "CreatePlanarBreps failed while authoring rhino-native-mix-obj.3dm");
        foreach (Brep b in trimmed) doc.Objects.AddBrep(b, new ObjectAttributes { LayerIndex = solids });

        // A mesh with vertex colors and texture coordinates: the ExportVcs / ExportTcs subject.
        Mesh colored = Mesh.CreateFromBox(new BoundingBox(0, 25, 0, 12, 33, 5), 1, 1, 1);
        for (int i = 0; i < colored.Vertices.Count; i++)
          colored.VertexColors.Add(Color.FromArgb((i * 23) % 256, (i * 57) % 256, (i * 91) % 256));
        doc.Objects.AddMesh(colored, new ObjectAttributes { LayerIndex = facets });

        // A NURBS curve and a polyline: curve goes to cstype bspline, polyline feeds PolylineType.
        doc.Objects.AddCurve(NurbsCurve.Create(false, 3, new Point3d[]
        {
          new Point3d(0, 45, 0), new Point3d(5, 50, 2), new Point3d(11, 42, -2), new Point3d(16, 48, 0)
        }), new ObjectAttributes { LayerIndex = wires });
        doc.Objects.AddCurve(new Polyline(new Point3d[]
        {
          new Point3d(25, 45, 0), new Point3d(32, 45, 0), new Point3d(32, 52, 0)
        }).ToPolylineCurve(), new ObjectAttributes { LayerIndex = wires });

        // A point: written as a 'p' record.
        doc.Objects.AddPoint(new Point3d(45, 45, 0), new ObjectAttributes { LayerIndex = wires });

        // A block inserted twice: OBJ has no assembly structure - instances flatten
        // (srcinstances 2 -> instances 0, the IGES story again).
        int idef = doc.InstanceDefinitions.Add(
          "ObjPeg", "instanced twice", Point3d.Origin,
          new GeometryBase[] { Mesh.CreateFromBox(new BoundingBox(-1, -1, 0, 1, 1, 2), 1, 1, 1) },
          new ObjectAttributes[] { new ObjectAttributes { LayerIndex = facets } });
        doc.Objects.AddInstanceObject(idef, Transform.Translation(0, 60, 0));
        doc.Objects.AddInstanceObject(idef, Transform.Translation(8, 60, 0));

        // Text and a dimension: silently dropped by the OBJ writer - the cleanest loss case
        // (srcother 2 against nothing in the file).
        doc.Objects.AddText(new Rhino.Display.Text3d("mx obj src", new Plane(new Point3d(0, 75, 0), Vector3d.ZAxis), 3),
          new ObjectAttributes { LayerIndex = notes });
        var dim = LinearDimension.Create(AnnotationType.Aligned, doc.DimStyles.Current,
          new Plane(new Point3d(0, 85, 0), Vector3d.ZAxis),
          Vector3d.XAxis, new Point3d(0, 85, 0), new Point3d(15, 85, 0), new Point3d(7.5, 89, 0), 0.0);
        doc.Objects.AddLinearDimension(dim, new ObjectAttributes { LayerIndex = notes });

        // SuppressAllInput/SuppressDialogBoxes are not optional here. RhinoDoc.WriteFile over an
        // EXISTING .3dm asks the user something; without them this [Explicit] author does not
        // fail, it blocks forever, and the blocked test host cannot be killed with Stop-Process
        // -Force or taskkill /F /T - the machine needs a reboot to clear it. Writing to a fresh
        // folder hides this, which is exactly how it stayed hidden until a re-author.
        Assert.IsTrue(doc.WriteFile(outPath, new FileWriteOptions { FileVersion = 7, SuppressAllInput = true, SuppressDialogBoxes = true }),
          $"RhinoDoc.WriteFile('{outPath}') returned false.");

        TestContext.Progress.WriteLine(
          $"[MXOBJEX] wrote '{outPath}': {doc.Objects.Count} objects, {new FileInfo(outPath).Length} bytes. " +
          "Regenerate its baseline next: MX_OBJEXPORT_REGEN=rhino-native-mix-obj.");
      }
      finally
      {
        doc.Dispose();
      }
    }
  }
}

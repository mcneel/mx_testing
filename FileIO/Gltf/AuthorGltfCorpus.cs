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
  /// Authors the committed glTF import corpus in <c>models\GLTFfile\</c> and the export suite's
  /// <c>.3dm</c> source in <c>models\GLTFfile-export\</c>.
  /// </summary>
  /// <remarks>
  /// glTF has two on-disk forms and the corpus carries both: <c>.gltf</c> is JSON with its
  /// buffers inlined as data: URIs, <c>.glb</c> packs the same content into a binary container.
  /// The same geometry is written both ways deliberately - the two baselines should agree, which
  /// is a cross-check no single file can give.
  ///
  /// Draco compression is left off throughout: with it on the mesh payload is an opaque
  /// compressed blob, which is worth testing one day but is a poor thing to pin counts against.
  ///
  /// [Explicit] twice over; refuses to run without MX_GLTF_AUTHOR_OUT_DIR:
  /// <code>
  /// MX_GLTF_AUTHOR_OUT_DIR=models dotnet test --filter "FullyQualifiedName~AuthorGltfCorpus"
  /// </code>
  /// </remarks>
  [TestFixture, Explicit]
  public class AuthorGltfCorpus
  {
    [Test, Explicit]
    public void Write()
    {
      string outDir = System.Environment.GetEnvironmentVariable("MX_GLTF_AUTHOR_OUT_DIR");
      Assert.IsFalse(string.IsNullOrWhiteSpace(outDir),
        "Set MX_GLTF_AUTHOR_OUT_DIR to the repository's models folder. This test overwrites the " +
        "corpus, so it will not guess a path - see the remarks on AuthorGltfCorpus.");

      string importDir = Path.Combine(outDir, "GLTFfile");
      string exportDir = Path.Combine(outDir, "GLTFfile-export");
      Directory.CreateDirectory(importDir);
      Directory.CreateDirectory(exportDir);

      // 1 & 2. The same box in both on-disk forms. Their baselines should match each other.
      Author(Path.Combine(importDir, "gltf-box.gltf"), null, BuildBox);
      Author(Path.Combine(importDir, "gltf-box.glb"), null, BuildBox);

      // 3. Vertex colours: glTF carries them as a mesh attribute, so this pins the round trip
      //    of something STL and IGES cannot represent at all.
      Author(Path.Combine(importDir, "gltf-vertex-colors.gltf"),
        o => o.ExportVertexColors = true, doc =>
      {
        Mesh m = Mesh.CreateFromBox(new BoundingBox(0, 0, 0, 10, 8, 5), 1, 1, 1);
        for (int i = 0; i < m.Vertices.Count; i++)
          m.VertexColors.Add(Color.FromArgb((i * 37) % 256, (i * 71) % 256, (i * 113) % 256));
        doc.Objects.AddMesh(m);
      });

      // 4. NURBS in, triangles out: glTF is a delivery format with no surface representation.
      Author(Path.Combine(importDir, "gltf-from-nurbs.gltf"), null, doc =>
      {
        doc.Objects.AddBrep(Brep.CreateFromBox(new BoundingBox(0, 0, 0, 12, 6, 4)));
        doc.Objects.AddBrep(Brep.CreateFromCylinder(
          new Cylinder(new Circle(new Plane(new Point3d(30, 0, 0), Vector3d.ZAxis), 5), 12), true, true));
      });

      // 5. Two bodies on named layers, written with ExportLayers on: glTF has a node hierarchy,
      //    so unlike STL the structure has somewhere to live.
      Author(Path.Combine(importDir, "gltf-layers.gltf"),
        o => o.ExportLayers = true, doc =>
      {
        int a = doc.Layers.Add("GltfA", Color.Firebrick);
        int b = doc.Layers.Add("GltfB", Color.SteelBlue);

        doc.Objects.AddMesh(Mesh.CreateFromBox(new BoundingBox(0, 0, 0, 6, 6, 6), 1, 1, 1),
          new ObjectAttributes { LayerIndex = a });
        doc.Objects.AddMesh(Mesh.CreateFromBox(new BoundingBox(10, 0, 0, 16, 6, 6), 1, 1, 1),
          new ObjectAttributes { LayerIndex = b });
      });

      AuthorExportSource3dm(Path.Combine(exportDir, "rhino-native-mix-gltf.3dm"));
    }

    static void BuildBox(RhinoDoc doc)
    {
      doc.Objects.AddMesh(Mesh.CreateFromBox(new BoundingBox(0, 0, 0, 10, 8, 5), 1, 1, 1));
    }

    static void Author(string outPath, Action<FileGltfWriteOptions> configure, Action<RhinoDoc> build)
    {
      RhinoDoc doc = RhinoDoc.CreateHeadless(null);
      try
      {
        doc.ModelUnitSystem = UnitSystem.Millimeters;
        doc.ModelAbsoluteTolerance = 0.001;

        build(doc);
        Assert.Greater(doc.Objects.Count, 0, $"nothing was built for '{outPath}'.");

        var options = new FileGltfWriteOptions();
        configure?.Invoke(options);

        Assert.IsTrue(FileGltf.Write(outPath, doc, options), $"FileGltf.Write('{outPath}') returned false.");

        TestContext.Progress.WriteLine(
          $"[MXGLTF] wrote '{outPath}': {doc.Objects.Count} objects, {new FileInfo(outPath).Length} bytes.");
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

        Mesh coloured = Mesh.CreateFromBox(new BoundingBox(50, 0, 0, 58, 8, 5), 1, 1, 1);
        for (int i = 0; i < coloured.Vertices.Count; i++)
          coloured.VertexColors.Add(Color.FromArgb((i * 23) % 256, (i * 57) % 256, (i * 91) % 256));
        doc.Objects.AddMesh(coloured, new ObjectAttributes { LayerIndex = facets });

        // Curves, points and annotations have no glTF representation.
        doc.Objects.AddCurve(new LineCurve(new Point3d(0, 20, 0), new Point3d(10, 20, 0)),
          new ObjectAttributes { LayerIndex = wires });
        doc.Objects.AddPoint(new Point3d(20, 20, 0), new ObjectAttributes { LayerIndex = wires });
        doc.Objects.AddText(
          new Rhino.Display.Text3d("mx gltf src", new Plane(new Point3d(0, 30, 0), Vector3d.ZAxis), 3),
          new ObjectAttributes { LayerIndex = wires });

        // SuppressAllInput/SuppressDialogBoxes are not optional here - see AuthorStlCorpus and
        // STL-tests-guide.md: without them a re-author blocks forever in an unkillable host.
        Assert.IsTrue(doc.WriteFile(outPath, new FileWriteOptions { FileVersion = 7, SuppressAllInput = true, SuppressDialogBoxes = true }),
          $"RhinoDoc.WriteFile('{outPath}') returned false.");

        TestContext.Progress.WriteLine(
          $"[MXGLTFEX] wrote '{outPath}': {doc.Objects.Count} objects. " +
          "Regenerate its baseline next: MX_GLTFEXPORT_REGEN=rhino-native-mix-gltf.");
      }
      finally
      {
        doc.Dispose();
      }
    }
  }
}

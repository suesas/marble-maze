using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Editor-side utilities to extrude polygonal contours (outer + holes) into a closed mesh with
/// top/bottom surfaces and side walls. Uses <see cref="PolygonCollider2D.CreateMesh"/> for robust
/// triangulation and combines submeshes. Also supports in-place mesh rotation.
/// </summary>
public static class MeshExtruder
{
    public struct Triangle { public Vector2 a, b, c; }

    /// <summary>
    /// Builds an extruded mesh from contours. Floor holes get inward-facing side walls.
    /// </summary>
    public static Mesh Extrude(float height, float baseY, List<List<Vector2>> contours, bool isFloor)
    {
        var surfaceMesh = new Mesh();
        if (contours != null && contours.Count > 0)
        {
            var surfaceVertices = new List<Vector3>();
            var surfaceTriangles = new List<int>();
            var surfaceNormals = new List<Vector3>();
            var triangulatedSurface = Triangulate(contours);
            foreach (var tri in triangulatedSurface)
            {
                int baseIdx = surfaceVertices.Count;
                surfaceVertices.Add(new Vector3(tri.a.x, baseY + height, tri.a.y));
                surfaceVertices.Add(new Vector3(tri.b.x, baseY + height, tri.b.y));
                surfaceVertices.Add(new Vector3(tri.c.x, baseY + height, tri.c.y));
                surfaceTriangles.Add(baseIdx); surfaceTriangles.Add(baseIdx + 1); surfaceTriangles.Add(baseIdx + 2);
                surfaceNormals.Add(Vector3.up); surfaceNormals.Add(Vector3.up); surfaceNormals.Add(Vector3.up);

                baseIdx = surfaceVertices.Count;
                surfaceVertices.Add(new Vector3(tri.a.x, baseY, tri.a.y));
                surfaceVertices.Add(new Vector3(tri.b.x, baseY, tri.b.y));
                surfaceVertices.Add(new Vector3(tri.c.x, baseY, tri.c.y));
                surfaceTriangles.Add(baseIdx + 2); surfaceTriangles.Add(baseIdx + 1); surfaceTriangles.Add(baseIdx);
                surfaceNormals.Add(Vector3.down); surfaceNormals.Add(Vector3.down); surfaceNormals.Add(Vector3.down);
            }
            if (surfaceVertices.Count > 65535) surfaceMesh.indexFormat = IndexFormat.UInt32;
            surfaceMesh.SetVertices(surfaceVertices);
            surfaceMesh.SetTriangles(surfaceTriangles, 0);
            surfaceMesh.SetNormals(surfaceNormals);
            surfaceMesh.RecalculateBounds();
        }

        var sideWallsMesh = new Mesh();
        if (contours != null && contours.Count > 0)
        {
            var sideVertices = new List<Vector3>();
            var sideTriangles = new List<int>();
            for (int c = 0; c < contours.Count; c++)
            {
                var contour = contours[c];
                bool isHole = isFloor && c > 0;
                for (int i = 0; i < contour.Count; i++)
                {
                    Vector2 p0 = contour[i];
                    Vector2 p1 = contour[(i + 1) % contour.Count];
                    int baseIndex = sideVertices.Count;
                    sideVertices.Add(new Vector3(p0.x, baseY, p0.y));
                    sideVertices.Add(new Vector3(p0.x, baseY + height, p0.y));
                    sideVertices.Add(new Vector3(p1.x, baseY, p1.y));
                    sideVertices.Add(new Vector3(p1.x, baseY + height, p1.y));
                    if (!isHole)
                    {
                        if (isFloor)
                        {
                            sideTriangles.Add(baseIndex); sideTriangles.Add(baseIndex + 2); sideTriangles.Add(baseIndex + 3);
                            sideTriangles.Add(baseIndex); sideTriangles.Add(baseIndex + 3); sideTriangles.Add(baseIndex + 1);
                        }
                        else
                        {
                            sideTriangles.Add(baseIndex); sideTriangles.Add(baseIndex + 1); sideTriangles.Add(baseIndex + 3);
                            sideTriangles.Add(baseIndex); sideTriangles.Add(baseIndex + 3); sideTriangles.Add(baseIndex + 2);
                        }
                    }
                    else
                    {
                        sideTriangles.Add(baseIndex); sideTriangles.Add(baseIndex + 2); sideTriangles.Add(baseIndex + 3);
                        sideTriangles.Add(baseIndex); sideTriangles.Add(baseIndex + 3); sideTriangles.Add(baseIndex + 1);
                    }
                }
            }
            if (sideVertices.Count > 65535) sideWallsMesh.indexFormat = IndexFormat.UInt32;
            sideWallsMesh.SetVertices(sideVertices);
            sideWallsMesh.SetTriangles(sideTriangles, 0);
            sideWallsMesh.RecalculateNormals();
            sideWallsMesh.RecalculateBounds();
        }

        var combines = new List<CombineInstance>(2);
        if (surfaceMesh != null && surfaceMesh.vertexCount > 0)
        {
            combines.Add(new CombineInstance { mesh = surfaceMesh, transform = Matrix4x4.identity });
        }
        if (sideWallsMesh != null && sideWallsMesh.vertexCount > 0)
        {
            combines.Add(new CombineInstance { mesh = sideWallsMesh, transform = Matrix4x4.identity });
        }
        var finalMesh = new Mesh();
        int totalVerts = 0;
        if (surfaceMesh != null) totalVerts += surfaceMesh.vertexCount;
        if (sideWallsMesh != null) totalVerts += sideWallsMesh.vertexCount;
        if (totalVerts > 65535) finalMesh.indexFormat = IndexFormat.UInt32;
        if (combines.Count > 0)
        {
            finalMesh.CombineMeshes(combines.ToArray(), true, false);
            // Cleanup intermediate meshes to avoid editor-side native memory leaks
            if (surfaceMesh != null) Object.DestroyImmediate(surfaceMesh);
            if (sideWallsMesh != null) Object.DestroyImmediate(sideWallsMesh);
        }
        return finalMesh;
    }

    /// <summary>
    /// Rotates vertex and normal data of a mesh in place and updates bounds.
    /// </summary>
    public static void RotateMeshInPlace(Mesh mesh, Quaternion rotation)
    {
        if (mesh == null) return;
        var verts = mesh.vertices;
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] = rotation * verts[i];
        }
        mesh.vertices = verts;
        if (mesh.normals != null && mesh.normals.Length == verts.Length)
        {
            var norms = mesh.normals;
            for (int i = 0; i < norms.Length; i++)
            {
                norms[i] = rotation * norms[i];
            }
            mesh.normals = norms;
        }
        mesh.RecalculateBounds();
    }

    /// <summary>
    /// Triangulates a set of contours (first is outer, others are holes) using PolygonCollider2D.
    /// </summary>
    public static List<Triangle> Triangulate(List<List<Vector2>> allContours)
    {
        var triangles = new List<Triangle>();
        var tempGO = new GameObject("TempTriangulator") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var collider = tempGO.AddComponent<PolygonCollider2D>();
            collider.pathCount = allContours.Count;
            for (int j = 0; j < allContours.Count; j++)
            {
                var contour = new List<Vector2>(allContours[j]);
                float winding = SVGShapeSampler.GetContourWinding(contour);
                if (j == 0) { if (winding < 0) contour.Reverse(); }
                else { if (winding > 0) contour.Reverse(); }
                collider.SetPath(j, contour.ToArray());
            }
            var tempMesh = collider.CreateMesh(false, false);
            if (tempMesh != null)
            {
                for (int i = 0; i < tempMesh.triangles.Length; i += 3)
                {
                    triangles.Add(new Triangle
                    {
                        a = tempMesh.vertices[tempMesh.triangles[i]],
                        b = tempMesh.vertices[tempMesh.triangles[i + 1]],
                        c = tempMesh.vertices[tempMesh.triangles[i + 2]],
                    });
                }
                Object.DestroyImmediate(tempMesh);
            }
        }
        finally { Object.DestroyImmediate(tempGO); }
        return triangles;
    }
}


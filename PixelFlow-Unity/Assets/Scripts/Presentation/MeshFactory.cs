using System.Collections.Generic;
using UnityEngine;

namespace PixelFlow.Presentation
{
    /// <summary>Closed meshes, shared by the play field and the waiting UFOs.</summary>
    public static class MeshFactory
    {
        private static readonly Dictionary<string, Mesh> Boxes = new Dictionary<string, Mesh>();
        private static readonly Dictionary<Mesh, Mesh> Outlines = new Dictionary<Mesh, Mesh>();

        public static Mesh BeveledBox(Vector3 size, float radius = .06f)
        {
            string key = size.ToString("F4") + "/" + radius.ToString("F4");
            Mesh cached;
            if (Boxes.TryGetValue(key, out cached)) return cached;
            const int divisions = 6;
            Vector3 half = size * .5f;
            radius = Mathf.Clamp(radius, .001f, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * .8f);
            Vector3 inner = half - Vector3.one * radius;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            Vector3[] directions = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 normal in directions)
            {
                Vector3 axisU = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up;
                Vector3 axisV = Vector3.Cross(normal, axisU);
                int offset = vertices.Count;
                for (int y = 0; y <= divisions; y++)
                for (int x = 0; x <= divisions; x++)
                {
                    float u = x / (float)divisions;
                    float v = y / (float)divisions;
                    Vector3 cube = normal + axisU * (u * 2 - 1) + axisV * (v * 2 - 1);
                    Vector3 p = Vector3.Scale(cube, half);
                    Vector3 closest = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 n = (p - closest).normalized;
                    vertices.Add(closest + n * radius);
                    normals.Add(n);
                    uv.Add(new Vector2(u, v));
                    if (x == divisions || y == divisions) continue;
                    int a = offset + y * (divisions + 1) + x;
                    int b = a + divisions + 1;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                }
            }
            Mesh result = new Mesh { name = "Beveled solid box " + key };
            result.SetVertices(vertices); result.SetNormals(normals); result.SetUVs(0, uv); result.SetTriangles(triangles, 0);
            result.RecalculateBounds();
            Boxes.Add(key, result);
            return result;
        }

        public static Mesh Lathe(Vector2[] profile, int sides = 40)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int row = 0; row < profile.Length; row++)
            {
                Vector2 before = profile[Mathf.Max(0, row - 1)];
                Vector2 after = profile[Mathf.Min(profile.Length - 1, row + 1)];
                Vector2 tangent = (after - before).normalized;
                for (int segment = 0; segment <= sides; segment++)
                {
                    float angle = segment * Mathf.PI * 2 / sides;
                    float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
                    vertices.Add(new Vector3(profile[row].x * cos, profile[row].y, profile[row].x * sin));
                    normals.Add(new Vector3(tangent.y * cos, -tangent.x, tangent.y * sin));
                    uv.Add(new Vector2(segment / (float)sides, row / (float)(profile.Length - 1)));
                    if (row == profile.Length - 1 || segment == sides) continue;
                    int a = row * (sides + 1) + segment;
                    int b = a + sides + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            }
            Mesh result = new Mesh { name = "Solid turned model" };
            result.SetVertices(vertices); result.SetNormals(normals); result.SetUVs(0, uv); result.SetTriangles(triangles, 0); result.RecalculateBounds();
            return result;
        }

        public static Mesh Torus(float majorRadius, float tubeRadius, int segments = 36, int rings = 10)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int segment = 0; segment <= segments; segment++)
            for (int ring = 0; ring <= rings; ring++)
            {
                float a = segment * Mathf.PI * 2 / segments;
                float b = ring * Mathf.PI * 2 / rings;
                Vector3 normal = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b));
                vertices.Add(new Vector3(Mathf.Cos(a) * majorRadius, 0, Mathf.Sin(a) * majorRadius) + normal * tubeRadius);
                normals.Add(normal);
                if (segment == segments || ring == rings) continue;
                int p = segment * (rings + 1) + ring;
                int q = p + rings + 1;
                triangles.Add(p); triangles.Add(p + 1); triangles.Add(q);
                triangles.Add(p + 1); triangles.Add(q + 1); triangles.Add(q);
            }
            Mesh mesh = new Mesh { name = "Solid rubber shoulder" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Inverted(Mesh source)
        {
            Mesh result;
            if (Outlines.TryGetValue(source, out result)) return result;
            result = Object.Instantiate(source); result.name = source.name + " silhouette";
            int[] triangles = result.triangles;
            for (int i = 0; i < triangles.Length; i += 3) { int swap = triangles[i]; triangles[i] = triangles[i + 1]; triangles[i + 1] = swap; }
            Vector3[] normals = result.normals;
            for (int i = 0; i < normals.Length; i++) normals[i] = -normals[i];
            result.triangles = triangles; result.normals = normals;
            Outlines.Add(source, result);
            return result;
        }
    }
}

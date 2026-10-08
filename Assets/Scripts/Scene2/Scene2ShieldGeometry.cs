using System.Collections.Generic;
using UnityEngine;

// Dual geodesic sphere: mostly hexagonal cells, with twelve pentagons and no UV poles.
// Visual geometry only; the gameplay collider remains unchanged.
internal static class Scene2ShieldGeometry
{
    public static Mesh Create()
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        var points = new List<Vector3> {
            new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),
            new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),
            new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1) };
        for (int i = 0; i < points.Count; i++) points[i] = points[i].normalized;
        var faces = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        var midpointCache = new Dictionary<long,int>();
        for (int level = 0; level < 2; level++)
        {
            var next = new List<int>(faces.Count * 4);
            for (int i = 0; i < faces.Count; i += 3)
            {
                int a = faces[i], b = faces[i+1], c = faces[i+2];
                int ab = Midpoint(a,b,points,midpointCache), bc = Midpoint(b,c,points,midpointCache), ca = Midpoint(c,a,points,midpointCache);
                next.AddRange(new[] { a,ab,ca, b,bc,ab, c,ca,bc, ab,bc,ca });
            }
            faces = next;
        }
        var neighbours = new List<Vector3>[points.Count];
        for (int i = 0; i < points.Count; i++) neighbours[i] = new List<Vector3>(6);
        for (int i = 0; i < faces.Count; i += 3)
        {
            Vector3 corner = (points[faces[i]] + points[faces[i+1]] + points[faces[i+2]]).normalized;
            neighbours[faces[i]].Add(corner); neighbours[faces[i+1]].Add(corner); neighbours[faces[i+2]].Add(corner);
        }
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var centres = new List<Vector3>();
        var indices = new List<int>();
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 centre = points[i];
            Vector3 axis = Vector3.Cross(centre, Mathf.Abs(centre.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 other = Vector3.Cross(centre, axis);
            var ring = neighbours[i];
            ring.Sort((a,b) => Mathf.Atan2(Vector3.Dot(a,other),Vector3.Dot(a,axis)).CompareTo(
                Mathf.Atan2(Vector3.Dot(b,other),Vector3.Dot(b,axis))));
            for (int j = 0; j < ring.Count; j++)
            {
                int start = vertices.Count;
                vertices.Add(centre); vertices.Add(ring[j]); vertices.Add(ring[(j+1)%ring.Count]);
                // Distance from cell centre to its perimeter, without triangle spokes.
                uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.right);
                centres.Add(centre); centres.Add(centre); centres.Add(centre);
                indices.Add(start); indices.Add(start+1); indices.Add(start+2);
            }
        }
        var mesh = new Mesh { name = "Scene2 Geodesic Energy Sphere" };
        mesh.SetVertices(vertices); mesh.SetNormals(vertices); mesh.SetUVs(0,uv); mesh.SetUVs(1,centres); mesh.SetTriangles(indices,0);
        mesh.bounds = new Bounds(Vector3.zero,Vector3.one*2.2f);
        return mesh;
    }

    private static int Midpoint(int a, int b, List<Vector3> points, Dictionary<long,int> cache)
    {
        long key = ((long)Mathf.Min(a,b) << 32) | (uint)Mathf.Max(a,b);
        if (cache.TryGetValue(key,out int index)) return index;
        index = points.Count; points.Add((points[a]+points[b]).normalized); cache.Add(key,index); return index;
    }
}

using System.Collections.Generic;
using Unity.VectorGraphics;
using UnityEngine;

/// <summary>
/// Samples SVG vector shapes into polygonal contours and prepares them for robust mesh extrusion
/// (outer + inner hole handling, vertex simplification).
///
/// UNIT CONVENTION
/// - All positions and distances processed here are in SVG shape space ("board units").
/// - "Board units" are the raw SVG coordinate units and will uniformly scale when the
///   importer fits the generated level to a target world width.
/// - Any world-space sizing should be applied by the caller after import scaling is known.
///
/// Editor-only utilities to keep runtime clean.
/// </summary>
public static class SVGShapeSampler
{
    /// <summary>
    /// Samples all contours of the given SVG shape into polyline vertices at approximately the target distance
    /// (distance measured in SVG/board units). Returns list with first element as the outer contour,
    /// followed by holes (if present).
    /// </summary>
    public static List<List<Vector2>> SampleAllContours(Shape shape, float targetDistance)
    {
        var allContours = new List<List<Vector2>>();
        if (shape == null || shape.Contours == null) return allContours;
        targetDistance = Mathf.Max(0.0001f, targetDistance);
        foreach (var contour in shape.Contours)
        {
            var sampled = SampleBezierContour(contour, targetDistance);
            if (sampled.Count >= 3) allContours.Add(sampled);
        }
        return allContours;
    }

    /// <summary>
        /// Prepares sampled contours for floor extrusion (all inputs in SVG/board units): simplifies the outer boundary
        /// and converts inner holes to regular polygons to ensure stable colliders/meshes.
    /// </summary>
    public static List<List<Vector2>> PrepareContoursForFloorExtrusion(List<List<Vector2>> allContours, bool simplify, float tolerance)
    {
        if (allContours == null || allContours.Count <= 1) return allContours ?? new List<List<Vector2>>();

        var outer = allContours[0];
        if (simplify)
        {
            outer = SimplifyClosedPolygon(outer, Mathf.Max(1e-5f, tolerance));
        }
        var processedContours = new List<List<Vector2>> { outer };
        for (int j = 1; j < allContours.Count; j++)
        {
            processedContours.Add(CreateRegularPolygonContour(allContours[j], 24));
        }

        var tempGO = new GameObject("TempCollider") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var collider = tempGO.AddComponent<PolygonCollider2D>();
            collider.pathCount = processedContours.Count;
            for (int j = 0; j < processedContours.Count; j++)
            {
                var contour = processedContours[j];
                float winding = GetContourWinding(contour);
                if (j == 0) { if (winding < 0) contour.Reverse(); }
                else { if (winding > 0) contour.Reverse(); }
                collider.SetPath(j, contour.ToArray());
            }

            var tempMesh = collider.CreateMesh(true, true);
            if (tempMesh != null)
            {
                var _ = tempMesh.vertices;
                Object.DestroyImmediate(tempMesh, true);
            }
            else
            {
                Debug.LogError("PolygonCollider2D failed to create a mesh for the floor.");
            }
        }
        finally
        {
            Object.DestroyImmediate(tempGO);
        }

        return processedContours;
    }

        /// <summary>
        /// Simplifies a closed polygon with RDP and collinearity pruning. Keeps shape stability.
        /// </summary>
        public static List<Vector2> SimplifyClosedPolygon(List<Vector2> input, float tolerance)
    {
        if (input == null || input.Count < 4) return new List<Vector2>(input ?? new List<Vector2>());
        var points = new List<Vector2>(input);
        if (points.Count > 1 && (points[0] - points[points.Count - 1]).sqrMagnitude < 1e-12f)
        {
            points.RemoveAt(points.Count - 1);
        }
        points = RemoveNearDuplicateVertices(points, tolerance * 0.2f);

        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < points.Count; i++) centroid += points[i];
        centroid /= points.Count;
        int startIndex = 0;
        float maxDist = -1f;
        for (int i = 0; i < points.Count; i++)
        {
            float d = (points[i] - centroid).sqrMagnitude;
            if (d > maxDist) { maxDist = d; startIndex = i; }
        }
        var rotated = new List<Vector2>();
        for (int i = 0; i < points.Count; i++) rotated.Add(points[(startIndex + i) % points.Count]);

        var simplified = RdpSimplify(rotated, tolerance);
        simplified = RemoveCollinearVertices(simplified, tolerance * 0.5f);
        return simplified;
    }

        /// <summary>
        /// Recursive Ramer–Douglas–Peucker simplification for 2D polylines.
        /// </summary>
        public static List<Vector2> RdpSimplify(List<Vector2> pts, float epsilon)
    {
        if (pts == null || pts.Count <= 2) return new List<Vector2>(pts ?? new List<Vector2>());

        int index = -1;
        float dmax = 0f;
        Vector2 start = pts[0];
        Vector2 end = pts[pts.Count - 1];
        for (int i = 1; i < pts.Count - 1; i++)
        {
            float d = PerpendicularDistance(pts[i], start, end);
            if (d > dmax)
            {
                index = i; dmax = d;
            }
        }
        if (dmax > epsilon)
        {
            var rec1 = RdpSimplify(pts.GetRange(0, index + 1), epsilon);
            var rec2 = RdpSimplify(pts.GetRange(index, pts.Count - index), epsilon);
            if (rec1.Count > 0) rec1.RemoveAt(rec1.Count - 1);
            rec1.AddRange(rec2);
            return rec1;
        }
        else
        {
            return new List<Vector2> { start, end };
        }
    }

        /// <summary>
        /// Perpendicular distance from point to line segment (infinite line if degenerate).
        /// </summary>
        public static float PerpendicularDistance(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
    {
        if ((lineEnd - lineStart).sqrMagnitude < 1e-12f) return Vector2.Distance(point, lineStart);
        return Mathf.Abs((lineEnd.x - lineStart.x) * (lineStart.y - point.y) - (lineStart.x - point.x) * (lineEnd.y - lineStart.y)) /
               Vector2.Distance(lineStart, lineEnd);
    }

        /// <summary>
        /// Removes vertices closer than <paramref name="minDist"/> to reduce noise.
        /// </summary>
        public static List<Vector2> RemoveNearDuplicateVertices(List<Vector2> pts, float minDist)
    {
        if (pts == null || pts.Count == 0) return new List<Vector2>();
        var result = new List<Vector2> { pts[0] };
        for (int i = 1; i < pts.Count; i++)
        {
            if ((pts[i] - result[result.Count - 1]).magnitude >= minDist)
                result.Add(pts[i]);
        }
        return result;
    }

        /// <summary>
        /// Removes vertices whose neighborhood triangle area is under <paramref name="areaTolerance"/>.
        /// </summary>
        public static List<Vector2> RemoveCollinearVertices(List<Vector2> pts, float areaTolerance)
    {
        if (pts == null || pts.Count < 3) return new List<Vector2>(pts ?? new List<Vector2>());
        var output = new List<Vector2>();
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 prev = pts[(i - 1 + pts.Count) % pts.Count];
            Vector2 curr = pts[i];
            Vector2 next = pts[(i + 1) % pts.Count];
            Vector2 a = curr - prev;
            Vector2 b = next - curr;
            float cross = Mathf.Abs(a.x * b.y - a.y * b.x);
            if (cross > areaTolerance)
            {
                output.Add(curr);
            }
        }
        return output.Count >= 3 ? output : new List<Vector2>(pts);
    }

        /// <summary>
        /// Approximates a closed/open cubic Bezier contour with points spaced ~targetDistance apart.
        /// </summary>
        public static List<Vector2> SampleBezierContour(BezierContour contour, float targetDistance)
    {
        var output = new List<Vector2>();
        if (contour.Segments == null || contour.Segments.Length == 0)
            return output;

        Vector2 previous = contour.Segments[0].P0;
        output.Add(previous);
        for (int i = 0; i < contour.Segments.Length; i++)
        {
            var seg = contour.Segments[i];
            var nextSeg = contour.Segments[(i + 1) % contour.Segments.Length];
            Vector2 p0 = seg.P0;
            Vector2 p1 = seg.P1;
            Vector2 p2 = seg.P2;
            Vector2 p3 = (i < contour.Segments.Length - 1 || contour.Closed) ? nextSeg.P0 : seg.P2;

            if ((p0 - previous).sqrMagnitude > 1e-6f)
            {
                previous = p0;
                if (output.Count == 0 || (output[output.Count - 1] - p0).sqrMagnitude > 1e-6f)
                    output.Add(p0);
            }

            float estimatedLen = EstimateCubicLength(p0, p1, p2, p3);
            int steps = Mathf.Max(1, Mathf.RoundToInt(estimatedLen / Mathf.Max(0.0001f, targetDistance)));
            for (int s = 1; s <= steps; s++)
            {
                float t = (float)s / steps;
                Vector2 pt = EvaluateCubic(p0, p1, p2, p3, t);
                if ((pt - output[output.Count - 1]).sqrMagnitude > 1e-10f)
                    output.Add(pt);
            }
            previous = p3;
        }
        if (contour.Closed && output.Count > 2)
        {
            if ((output[0] - output[output.Count - 1]).sqrMagnitude > 1e-6f)
                output.Add(output[0]);
        }
        return output;
    }

        /// <summary>
        /// Evaluates a cubic Bezier at parameter <paramref name="t"/> ∈ [0,1].
        /// </summary>
        public static Vector2 EvaluateCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t;
        float uu = u * u;
        float uuu = uu * u;
        float tt = t * t;
        float ttt = tt * t;
        return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
    }

        /// <summary>
        /// Fast length estimate for a cubic using control polygon length and chord.
        /// </summary>
        public static float EstimateCubicLength(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        float len = 0f;
        len += Vector2.Distance(p0, p1);
        len += Vector2.Distance(p1, p2);
        len += Vector2.Distance(p2, p3);
        float chord = Vector2.Distance(p0, p3);
        return Mathf.Max(len * 0.5f, chord);
    }

        /// <summary>
        /// Converts an arbitrary roughly circular contour into a regular polygon with <paramref name="sides"/>.
        /// </summary>
        public static List<Vector2> CreateRegularPolygonContour(List<Vector2> circleContour, int sides)
    {
        if (circleContour == null || circleContour.Count == 0) return new List<Vector2>();
        float minX = circleContour[0].x, maxX = circleContour[0].x;
        float minY = circleContour[0].y, maxY = circleContour[0].y;
        foreach (var p in circleContour)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }
        Vector2 center = new Vector2((minX + maxX) / 2, (minY + maxY) / 2);
        float radius = ((maxX - minX) + (maxY - minY)) / 4;
        var polygonPoints = new List<Vector2>();
        if (sides < 3) sides = 3;
        for (int i = 0; i < sides; i++)
        {
            float angle = (360f / sides * i) * Mathf.Deg2Rad;
            float x = center.x + radius * Mathf.Cos(angle);
            float y = center.y + radius * Mathf.Sin(angle);
            polygonPoints.Add(new Vector2(x, y));
        }
        return polygonPoints;
    }

        /// <summary>
        /// Computes a signed winding value; sign indicates orientation (CW/CCW).
        /// </summary>
        public static float GetContourWinding(List<Vector2> contour)
    {
        float signedArea = 0;
        for (int i = 0; i < contour.Count; i++)
        {
            Vector2 p0 = contour[i];
            Vector2 p1 = contour[(i + 1) % contour.Count];
            signedArea += (p1.x - p0.x) * (p1.y + p0.y);
        }
        return signedArea;
    }
}


using System;
using Unity.Mathematics;
using UnityEngine;
namespace Generation
{


static class Geometry
{
    // public static void CircumCircle(ref Triangle t, out float3 circleCenter, out float circleRadius) =>
    //     CircumCircle(Voronoi.V3(t.A), Voronoi.V3(t.B), Voronoi.V3(t.C), out circleCenter, out circleRadius);
    public static float3 V3(float2 v, float y = 0) => new float3(v.x, y, v.y);

    public static void InCenter(float2 p1, float2 p2, float2 p3, out float2 inCenter, out float inRadius)
    {
        var a = math.distance(p1, p2);
        var b = math.distance(p2, p3);
        var c = math.distance(p3, p1);

        var perimeter = (a + b + c);
        var x = (a * p1.x + b * p2.x + c * p3.x) / perimeter; 
        var y = (a * p1.y + b * p2.y + c * p3.y) / perimeter; 
        inCenter = new float2(x,y);

        var s = perimeter / 2;
        var triangleArea = math.sqrt(s * (s - a) * (s - b) * (s - c));
        inRadius = triangleArea / s;
    }
    public static void CircumCircle(float3 a, float3 b, float3 c, out float3 circleCenter, out float circleRadius)
    {
        PerpendicularBisector(a, b - a, out var perpAbPos, out var perpAbDir);
        PerpendicularBisector(a, c - a, out var perpAcPos, out var perpAcdir);
        var tAb = Intersection(perpAbPos, perpAbPos + perpAbDir, perpAcPos, perpAcPos + perpAcdir);
        circleCenter = perpAbPos + perpAbDir * tAb;

        circleRadius = math.length(circleCenter - a);
    }

    private static void PerpendicularBisector(float3 pos, float3 dir, out float3 bisectorPos,
        out float3 bisectorDir)
    {
        var m = dir * .5f;
        var cross = math.normalize(math.cross(math.normalize(dir), new float3(0, 1, 0)));
        bisectorPos = pos + m;
        bisectorDir = cross;
    }

    // http://paulbourke.net/geometry/pointlineplane/
    private static float Intersection(float3 p1, float3 p2, float3 p3, float3 p4)
    {
        var tAb = ((p4.x - p3.x) * (p1.z - p3.z) - (p4.z - p3.z) * (p1.x - p3.x)) /
                  ((p4.z - p3.z) * (p2.x - p1.x) - (p4.x - p3.x) * (p2.z - p1.z));
        return tAb;
    }

    public static bool PointTriangleIntersection(in TriangleStorage storage, float2 p, TriangleStorage.Triangle t)
    {
        var a = storage.F2(t.V1);
        var b = storage.F2(t.V2);
        var c = storage.F2(t.V3);
        
        var s = a.y * c.x - a.x * c.y + (c.y - a.y) * p.x + (a.x - c.x) * p.y;
        var tr = a.x * b.y - a.y * b.x + (a.y - b.y) * p.x + (b.x - a.x) * p.y;

        if ((s < 0) != (tr < 0))
            return false;

        var A = -b.y * c.x + a.y * (c.x - b.x) + a.x * (b.y - c.y) + b.x * c.y;

        return A < 0 ?
            (s <= 0 && s + tr >= A) :
            (s >= 0 && s + tr <= A);
    }
    
    /// <summary>
	/// Get open-ended Bezier Spline Control Points.
	/// </summary>
	/// <param name="knots">Input Knot Bezier spline points.</param>
	/// <param name="firstControlPoints">Output First Control points
	/// array of knots.Length - 1 length.</param>
	/// <param name="secondControlPoints">Output Second Control points
	/// array of knots.Length - 1 length.</param>
	/// <exception cref="ArgumentNullException"><paramref name="knots"/>
	/// parameter must be not null.</exception>
	/// <exception cref="ArgumentException"><paramref name="knots"/>
	/// array must contain at least two points.</exception>
	public static void GetCurveControlPoints(float2[] knots,
		out float2[] firstControlPoints, out float2[] secondControlPoints)
	{
		if (knots == null)
			throw new ArgumentNullException("knots");
		int n = knots.Length - 1;
		if (n < 1)
			throw new ArgumentException
			("At least two knot points required", "knots");
		if (n == 1)
		{ // Special case: Bezier curve should be a straight line.
			firstControlPoints = new float2[1];
			// 3P1 = 2P0 + P3
			firstControlPoints[0].x = (2 * knots[0].x + knots[1].x) / 3;
			firstControlPoints[0].y = (2 * knots[0].y + knots[1].y) / 3;

			secondControlPoints = new float2[1];
			// P2 = 2P1 – P0
			secondControlPoints[0].x = 2 *
				firstControlPoints[0].x - knots[0].x;
			secondControlPoints[0].y = 2 *
				firstControlPoints[0].y - knots[0].y;
			return;
		}

		// Calculate first Bezier control points
		// Right hand side vector
		double[] rhs = new double[n];

		// Set right hand side X values
		for (int i = 1; i < n - 1; ++i)
			rhs[i] = 4 * knots[i].x + 2 * knots[i + 1].x;
		rhs[0] = knots[0].x + 2 * knots[1].x;
		rhs[n - 1] = (8 * knots[n - 1].x + knots[n].x) / 2.0;
		// Get first control points X-values
		double[] x = GetFirstControlPoints(rhs);

		// Set right hand side Y values
		for (int i = 1; i < n - 1; ++i)
			rhs[i] = 4 * knots[i].y + 2 * knots[i + 1].y;
		rhs[0] = knots[0].y + 2 * knots[1].y;
		rhs[n - 1] = (8 * knots[n - 1].y + knots[n].y) / 2.0;
		// Get first control points Y-values
		double[] y = GetFirstControlPoints(rhs);

		// Fill output arrays.
		firstControlPoints = new float2[n];
		secondControlPoints = new float2[n];
		for (int i = 0; i < n; ++i)
		{
			// First control point
			firstControlPoints[i] = new float2((float) x[i], (float)y[i]);
			// Second control point
			if (i < n - 1)
				secondControlPoints[i] = new float2((float)(2 * knots
					[i + 1].x - x[i + 1]),(float)(2 *
					knots[i + 1].y - y[i + 1]));
			else
				secondControlPoints[i] = new float2((float)(knots
					[n].x + x[n - 1]) / 2,
					(float)(knots[n].y + y[n - 1]) / 2);
		}
	}

	/// <summary>
	/// Solves a tridiagonal system for one of coordinates (x or y)
	/// of first Bezier control points.
	/// </summary>
	/// <param name="rhs">Right hand side vector.</param>
	/// <returns>Solution vector.</returns>
	private static double[] GetFirstControlPoints(double[] rhs)
	{
		int n = rhs.Length;
		double[] x = new double[n]; // Solution vector.
		double[] tmp = new double[n]; // Temp workspace.

		double b = 2.0;
		x[0] = rhs[0] / b;
		for (int i = 1; i < n; i++) // Decomposition and forward substitution.
		{
			tmp[i] = 1 / b;
			b = (i < n - 1 ? 4.0 : 3.5) - tmp[i];
			x[i] = (rhs[i] - x[i - 1]) / b;
		}
		for (int i = 1; i < n; i++)
			x[n - i - 1] -= tmp[n - i] * x[n - i]; // Backsubstitution.

		return x;
	}

	public static bool SegmentSegmentIntersection(float2 a, float2 b, float2 c, float2 d, out float t, out float u)
	{
		float2 cmP = new float2(c.x - a.x, c.y - a.y);
		float2 r = new float2(b.x - a.x, b.y - a.y);
		float2 s = new float2(d.x - c.x, d.y - c.y);
 
		float cmPxr = cmP.x * r.y - cmP.y * r.x;
		float cmPxs = cmP.x * s.y - cmP.y * s.x;
		float rxs = r.x * s.y - r.y * s.x;
 
		if (math.abs(cmPxr) < float.Epsilon)
		{
			t = u = -1;
			// Lines are collinear, and so intersect if they have any overlap
			return ((c.x - a.x < 0f) != (c.x - b.x < 0f))
			       || ((c.y - a.y < 0f) != (c.y - b.y < 0f));
		}
 
		if (math.abs(rxs) < float.Epsilon)
		{
			t = u = -1;
			return false; // Lines are parallel.
		}
 
		float rxsr = 1f / rxs;
		t = cmPxs * rxsr;
		u = cmPxr * rxsr;
 
		return (t >= 0f) && (t <= 1f) && (u >= 0f) && (u <= 1f);
	}
	
	public static float SqDistancePtSegment( float2 a, float2 b, float2 p )
	{
		float2 n = b - a;
		float2 pa = a - p;
 
		float c = math.dot( n, pa );
 
		// Closest point is a
		if ( c > 0.0f )
			return math.dot( pa, pa );
 
		float2 bp = p - b;
 
		// Closest point is b
		if ( math.dot( n, bp ) > 0.0f )
			return math.dot( bp, bp );
 
		// Closest point is between a and b
		float2 e = pa - n * (c / math.dot( n, n ));
 
		return math.dot( e, e );
	}

	public static bool SegmentCircleIntersection(float2 E, float2 L, float2 C, float r, out float t)
	{
		float l2 = math.distancesq(E, L); // i.e. |w-v|^2 -  avoid a sqrt
		float dist;
		if (l2 == 0.0)
		{
			dist = math.distance(C, E);
			t = 0;
			return dist < r;
		}

		// Consider the line extending the segment, parameterized as v + t (w - v).
		// We find projection of point p onto the line. 
		// It falls where t = [(p-v) . (w-v)] / |w-v|^2
		// We clamp t from [0,1] to handle points outside the segment vw.
		t = math.max(0, math.min(1, math.dot(C - E, L - E) / l2));
		float2 projection = E + t * (L - E); // Projection falls on the segment
		dist = math.distance(C, projection);
		return dist < r;

		// var d = L - E;
		// var f = E - C;
		//
		// float a = math.dot(d, d);
		// float b = 2 * math.dot(f,d);
		// float c = math.dot(f,f) - r * r;
		//
		// float discriminant = b * b - 4 * a * c;
		// if (discriminant < 0)
		// {
		// 	// no intersection
		// 	t = -1;
		// 	return false;
		// }
		// else
		// {
		// 	// ray didn't totally miss sphere,
		// 	// so there is a solution to
		// 	// the equation.
		//
		// 	discriminant = math.sqrt(discriminant);
		//
		// 	// either solution may be on or off the ray so need to test both
		// 	// t1 is always the smaller value, because BOTH discriminant and
		// 	// a are nonnegative.
		// 	float t1 = (-b - discriminant) / (2 * a);
		// 	float t2 = (-b + discriminant) / (2 * a);
		//
		// 	// 3x HIT cases:
		// 	//          -o->             --|-->  |            |  --|->
		// 	// Impale(t1 hit,t2 hit), Poke(t1 hit,t2>1), ExitWound(t1<0, t2 hit), 
		//
		// 	// 3x MISS cases:
		// 	//       ->  o                     o ->              | -> |
		// 	// FallShort (t1>1,t2>1), Past (t1<0,t2<0), CompletelyInside(t1<0, t2>1)
		//
		// 	if (t1 >= 0 && t1 <= 1)
		// 	{
		// 		// t1 is the intersection, and it's closer than t2
		// 		// (since t1 uses -b - discriminant)
		// 		// Impale, Poke
		// 		t = t1;
		// 		return true;
		// 	}
		//
		// 	// here t1 didn't intersect so we are either started
		// 	// inside the sphere or completely past it
		// 	if (t2 >= 0 && t2 <= 1)
		// 	{
		// 		// ExitWound
		// 		t = t2;
		// 		return true;
		// 	}
		//
		// 	// CompletelyInside
		// 	if (t1 < 0 && t2 > 1)
		// 	{
		// 		return true;
		// 	}
		//
		// 	// no intn: FallShort, Past
		// 	t = -1;
		// 	return false;
	}
}
}
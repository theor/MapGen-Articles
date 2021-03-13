using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Generation;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Generation
{
    
public class MapGenerator : MonoBehaviour
{
    public enum GizmosModes
    {
        None,
        Points,
        Triangles
    }
    public enum ColorModes
    {
        None,
        TriangleColor,
        Index,
        SlopeVisualization,
        HeightVisualization
    }
    
    [Header("Parameters")]
    // Generated point count
    public int Count = 100;
    // Size of the square generated mesh in world space
    public int Size = 100;
    // Random seed
    public Params GenerationParams  = new Params
    {
        Lacunarity = 2,
        Persistence = .5f,
        Octaves = 4,
        NoiseScale = 40,

        HeightFactor = 1,
    };

    public int SmoothPasses;

    
    [Header("Data")]
    // Generated points
    public TriangleStorage Storage;
    public NativeArray<TriangleData> Tridata;
    public float MinHeight, MaxHeight;
    
    [Header("Debug")]
    
    public GizmosModes GizmosMode;
    public ColorModes ColorMode;
    public GizmosOptions GizmosOptions;

    // Generation entry point
    public void Generate()
    {
        Dispose();
        
        using var points = Generation.GenerateRandomPoints(Count, Size, GenerationParams.Seed, Allocator.TempJob);
        
        Storage = Generation.DelaunayTriangulation(points, Size);
        Tridata = Generation.GenerateHeightMap(Storage, GenerationParams);
        for (int i = 0; i < SmoothPasses; i++)
            Generation.SmoothHeights(ref Tridata, Storage);
        Generation.NormalizeHeights(in Tridata, out MinHeight, out MaxHeight);

        GenerateMeshFromStorage();
    }

    private void GenerateMeshFromStorage()
    {
        var meshbuilder = new MeshBuilder(gameObject);
        meshbuilder.Generate(b =>
        {
            for (var index = 0; index < Storage.Triangles.Length; index++)
            {
                var sFace = Storage.Triangles[index];
                if (sFace.IsDeleted)
                    continue;

                Color c1, c2, c3;
                switch (ColorMode)
                {
                    case ColorModes.HeightVisualization: // when UseTriangleHeight:
                        c1 = c2 = c3 = h(index, Tridata[index]);
                        break;
                    case ColorModes.None:
                        c1 = c2 = c3 = Color.white;
                        break;
                    case ColorModes.TriangleColor:
                        c1 = c2 = c3 = HaltonSequence.ColorFromIndex(index);
                        break;
                    case ColorModes.Index:
                        c1 = c2 = c3 = Color.HSVToRGB(192/255.0f, index / (float)Storage.Triangles.Length, .8f);
                        break;
                    // case ColorModes.SlopeVisualization:
                    //     var c = math.abs(Tridata[index].TriSlope) * 5;
                    //     c1 = c2 = c3 = new Color(c.x, c.x, c.x, 1);
                    //     break;
                    default: throw new InvalidDataException(ColorMode.ToString());
                }

                var height = Tridata[index].Height * GenerationParams.HeightFactor;
                b.AddTriangle(sFace, Storage, height, c1, c2, c3);

                AddQuadOnEdge(height, b, c1, sFace.Edge1);
                AddQuadOnEdge(height, b, c1, sFace.Edge2);
                AddQuadOnEdge(height, b, c1, sFace.Edge3);

                // b.AddTriangle(sFace, Storage,
                //     _params.HeightFactor *
                //     (UseTriangleHeight && Tridata[index].Height > _params.SeaLevel
                //         ? new float3(Tridata[index].Height)
                //         : new float3(
                //             _params
                //                 .SeaLevel)), // new float3(_data[sFace.V1].Height, _data[sFace.V2].Height, _data[sFace.V3].Height)),
                //     c1,
                //     c2,
                //     c3);
            }
        });
        
        Color h(int index, TriangleData tridata)
        {
            var f = math.clamp(math.remap(-1f, 1f, 0f, 1f, tridata.Height), 0f, 1f);

            Color Hex(string s)
            {
                ColorUtility.TryParseHtmlString(s, out var c);
                return c;
            }
            // var x = math.remap(0, 50, 0.5f, 1f, (HaltonSequence.HaltonInt(index, 23, 50)));
            var x = math.remap(-1, 1, 0.5f, 1f, noise.snoise(tridata.Centroid/10));

            // var x = math.remap(0f, 1f, 0.8f, 1f, (float)HaltonSequence.Halton((int) (tridata.Centroid.x*13 + tridata.Centroid.y*23), 7));

            // var colors = new Color[] {Hex("#ce6a6b"), Hex("#ebaca2"), Hex("#bed3c3"), Hex("#4a919e"), Hex("#212e53")};
            var colors = new Color[] {Hex("#090446"), Hex("#786F52"), Hex("#FEB95F"), Hex("#F71735"), Hex("#C2095A")};
            if (f < 0.2f)
                return colors[0] * x;
            if (f < 0.4f)
                return colors[1] * x;
            if (f < 0.6f)
                return colors[2] * x;
            if (f < 0.8f)
                return colors[3] * x;
            return colors[4] * x;
            // if (tridata.Height < GenerationParams.SeaLevel)
            // {
            //     var waterF = math.clamp(math.remap(Min, GenerationParams.SeaLevel, 0, 1, tridata.Height), 0, 1);
            //     waterF = math.pow(waterF, 3);
            //
            //     return new Color(waterF * 0.29f, waterF * 0.61f, waterF * 1f);
            // }
            //
            // if (tridata.WaterFlow >= WaterParams.RiverMinWaterFlow)
            //     return Color.cyan;
            //
            // var height2 = math.clamp(math.remap(GenerationParams.SeaLevel, Max, 0, 1, tridata.Height), 0.2f, 1);
            // return new Color(height2, height2, height2);
        }

        void AddQuadOnEdge(float height, MeshBuilder b, Color c1, Edge edge)
        {
            var va = Storage.F3(edge.A, height);
            var vb = Storage.F3(edge.B, height);
            var vc = va - new float3(0, va.y + GenerationParams.HeightFactor, 0);
            var vd = vb - new float3(0, vb.y + GenerationParams.HeightFactor, 0);
            b.AddTriangle(vb, va, vc, c1);
            b.AddTriangle(vb, vc, vd, c1);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Storage.Points.IsCreated)
            return;

        switch (GizmosMode)
        {
            case GizmosModes.None:
                return;
            case GizmosModes.Points:
                for (var index = 0; index < Storage.Points.Length; index++)
                {
                    var p = Storage.Points[index];
                    Gizmos.color = HaltonSequence.ColorFromIndex(index, v:1);
                    Gizmos.DrawSphere(Geometry.V3(p.Position), 1f);
                    Handles.Label(Geometry.V3(p.Position, 25), index.ToString());
                }
                break;
            case GizmosModes.Triangles:
                for (var index = 0; index < Storage.Triangles.Length; index++)
                {
                    var tri = Storage.Triangles[index];
                    if (tri.IsDeleted)
                        continue;
                    var v1 = Storage.F3(tri.V1);
                    var v2 = Storage.F3(tri.V2);
                    var v3 = Storage.F3(tri.V3);
                    Gizmos.color = HaltonSequence.ColorFromIndex(index, v:1);
                    Gizmos.DrawLine(v1, v2);
                    Gizmos.DrawLine(v2, v3);
                    Gizmos.DrawLine(v3, v1); 
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void Update()
    {
        if(EditorApplication.isCompiling)
            Dispose(); 
    }

    private void Dispose()
    {
        if(Storage.IsCreated)
            Storage.Dispose();
        try
        {
            if(Tridata.IsCreated)
                Tridata.Dispose();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private void OnDisable() => Dispose();

    private void OnDestroy() => Dispose();
}

[Serializable]
public struct GizmosOptions
{
    public int _selectedTriangle;
    public int _selectedPoint;
}
}

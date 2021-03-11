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
    public int Seed;
    
    [Header("Data")]
    // Generated points
    public TriangleStorage Storage;
    
    [Header("Debug")]
    
    public GizmosModes GizmosMode;
    public ColorModes ColorMode;

    // Generation entry point
    public void Generate()
    {
        Dispose();
        using var points = Generation.GenerateRandomPoints(Count, Size, Seed, Allocator.TempJob);
        Storage = Generation.DelaunayTriangulation(points, Size);

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
                    // case ColorModes.HeightVisualization: // when UseTriangleHeight:
                    //     c1 = c2 = c3 = h(Tridata[index]);
                    //     break;
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
                
                b.AddTriangle(sFace, Storage, index / (float)Storage.Triangles.Length, c1, c2, c3);

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

    private void Dispose()
    {
        if(Storage.IsCreated) Storage.Dispose();
    }

    private void OnDisable() => Dispose();

    private void OnDestroy() => Dispose();
}
}

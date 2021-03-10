using System;
using System.Collections;
using System.Collections.Generic;
using Generation;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Header("Parameters")]
    // Generated point count
    public int Count = 100;
    // Size of the generated mesh in world space
    public Vector2Int Size = new Vector2Int(100,100);
    // Random seed
    public int Seed;
    
    [Header("Data")]
    // Generated points
    private NativeArray<float2> _points;

    // Generation entry point
    public void Generate()
    {
        Dispose();
        _points = Generation.Generation.GenerateRandomPoints(Count, Size, Seed);
    }

    private void OnDrawGizmosSelected()
    {
        if (_points.IsCreated)
        {
            for (var i = 0; i < _points.Length; i++)
            {
                var vertex = _points[i];
                Gizmos.color = HaltonSequence.ColorFromIndex(i, 3, 1);
                Gizmos.DrawWireSphere(new Vector3(vertex.x, 0, vertex.y), 1);
            }
        }
    }

    private void Dispose()
    {
        if(_points.IsCreated) _points.Dispose();
    }

    private void OnDisable() => Dispose();

    private void OnDestroy() => Dispose();
}

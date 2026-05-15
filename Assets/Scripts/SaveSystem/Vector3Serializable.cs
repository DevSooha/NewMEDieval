using System;
using UnityEngine;

/// <summary>
/// UnityEngine.Vector3의 직렬화 래퍼.
/// JsonUtility는 Dictionary 안의 Vector3를 직렬화하지 못하므로 이 타입을 사용한다.
/// </summary>
[Serializable]
public struct Vector3Serializable
{
    public float x;
    public float y;
    public float z;

    public Vector3Serializable(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public Vector3Serializable(Vector3 v) : this(v.x, v.y, v.z) { }

    public Vector3 ToVector3() => new Vector3(x, y, z);

    public static implicit operator Vector3(Vector3Serializable s) => s.ToVector3();
    public static implicit operator Vector3Serializable(Vector3 v) => new Vector3Serializable(v);

    public override string ToString() => $"({x:F3}, {y:F3}, {z:F3})";
}

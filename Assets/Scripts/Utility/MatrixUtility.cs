using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using static Unity.Mathematics.math;

public static class MatrixUtility
{
    public static double4x4 inverse(ref this double4x4 db)
    {
        double4x4 reversedValue;
        double n11 = db.c0.x, n12 = db.c1.x, n13 = db.c2.x, n14 = db.c3.x;
        double n21 = db.c0.y, n22 = db.c1.y, n23 = db.c2.y, n24 = db.c3.y;
        double n31 = db.c0.z, n32 = db.c1.z, n33 = db.c2.z, n34 = db.c3.z;
        double n41 = db.c0.w, n42 = db.c1.w, n43 = db.c2.w, n44 = db.c3.w;
        double t11 = n23 * n34 * n42 - n24 * n33 * n42 + n24 * n32 * n43 - n22 * n34 * n43 - n23 * n32 * n44 + n22 * n33 * n44;
        double t12 = n14 * n33 * n42 - n13 * n34 * n42 - n14 * n32 * n43 + n12 * n34 * n43 + n13 * n32 * n44 - n12 * n33 * n44;
        double t13 = n13 * n24 * n42 - n14 * n23 * n42 + n14 * n22 * n43 - n12 * n24 * n43 - n13 * n22 * n44 + n12 * n23 * n44;
        double t14 = n14 * n23 * n32 - n13 * n24 * n32 - n14 * n22 * n33 + n12 * n24 * n33 + n13 * n22 * n34 - n12 * n23 * n34;

        double det = n11 * t11 + n21 * t12 + n31 * t13 + n41 * t14;
        double idet = 1.0 / det;

        reversedValue.c0 = new double4(t11 * idet,
        (n24 * n33 * n41 - n23 * n34 * n41 - n24 * n31 * n43 + n21 * n34 * n43 + n23 * n31 * n44 - n21 * n33 * n44) * idet,
        (n22 * n34 * n41 - n24 * n32 * n41 + n24 * n31 * n42 - n21 * n34 * n42 - n22 * n31 * n44 + n21 * n32 * n44) * idet,
        (n23 * n32 * n41 - n22 * n33 * n41 - n23 * n31 * n42 + n21 * n33 * n42 + n22 * n31 * n43 - n21 * n32 * n43) * idet);

        reversedValue.c1 = new double4(t12 * idet,
        (n13 * n34 * n41 - n14 * n33 * n41 + n14 * n31 * n43 - n11 * n34 * n43 - n13 * n31 * n44 + n11 * n33 * n44) * idet,
        (n14 * n32 * n41 - n12 * n34 * n41 - n14 * n31 * n42 + n11 * n34 * n42 + n12 * n31 * n44 - n11 * n32 * n44) * idet,
        (n12 * n33 * n41 - n13 * n32 * n41 + n13 * n31 * n42 - n11 * n33 * n42 - n12 * n31 * n43 + n11 * n32 * n43) * idet);

        reversedValue.c2 = new double4(t13 * idet,
        (n14 * n23 * n41 - n13 * n24 * n41 - n14 * n21 * n43 + n11 * n24 * n43 + n13 * n21 * n44 - n11 * n23 * n44) * idet,
        (n12 * n24 * n41 - n14 * n22 * n41 + n14 * n21 * n42 - n11 * n24 * n42 - n12 * n21 * n44 + n11 * n22 * n44) * idet,
        (n13 * n22 * n41 - n12 * n23 * n41 - n13 * n21 * n42 + n11 * n23 * n42 + n12 * n21 * n43 - n11 * n22 * n43) * idet);

        reversedValue.c3 = new double4(t14 * idet,
        (n13 * n24 * n31 - n14 * n23 * n31 + n14 * n21 * n33 - n11 * n24 * n33 - n13 * n21 * n34 + n11 * n23 * n34) * idet,
        (n14 * n22 * n31 - n12 * n24 * n31 - n14 * n21 * n32 + n11 * n24 * n32 + n12 * n21 * n34 - n11 * n22 * n34) * idet,
        (n12 * n23 * n31 - n13 * n22 * n31 + n13 * n21 * n32 - n11 * n23 * n32 - n12 * n21 * n33 + n11 * n22 * n33) * idet);
        return reversedValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4x4 toMatrix4x4(ref this double4x4 db)
    {
        return new Matrix4x4((float4)db.c0, (float4)db.c1, (float4)db.c2, (float4)db.c3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double4x4 toDouble4x4(ref this Matrix4x4 db)
    {
        return new double4x4((float4)db.GetColumn(0), (float4)db.GetColumn(1), (float4)db.GetColumn(2), (float4)db.GetColumn(3));
    }

}

public static class VectorUtility
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 GetPlane(float3 normal, float3 inPoint)
    {
        return new float4(normal, -dot(normal, inPoint));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 GetPlane(float3 a, float3 b, float3 c)
    {
        float3 normal = normalize(cross(b - a, c - a));
        return float4(normal, -dot(normal, a));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetDistanceToPlane(float4 plane, float3 inPoint)
    {
        return dot(plane.xyz, inPoint) + plane.w;
    }
}

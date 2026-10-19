using UnityEngine;

/// <summary>
/// 为所有河流地块提供一张共享的高细分河面网格。
/// 网格尺寸与 Unity 内置 Plane 一致（10×10 个局部单位），因此可直接替换而不改变现有 Transform。
/// </summary>
internal static class FloodWaveGridMeshProvider
{
    private const int 细分数 = 32;
    private const float 局部边长 = 10f;

    private static Mesh 共享网格;

    public static void 确保使用高细分网格(Renderer 河水渲染器)
    {
        if (河水渲染器 == null || !河水渲染器.TryGetComponent(out MeshFilter 网格过滤器))
        {
            return;
        }

        Mesh 目标网格 = 获取或创建共享网格();
        if (网格过滤器.sharedMesh != 目标网格)
        {
            网格过滤器.sharedMesh = 目标网格;
        }
    }

    private static Mesh 获取或创建共享网格()
    {
        if (共享网格 != null)
        {
            return 共享网格;
        }

        int 每行顶点数 = 细分数 + 1;
        int 顶点数 = 每行顶点数 * 每行顶点数;
        Vector3[] 顶点 = new Vector3[顶点数];
        Vector3[] 法线 = new Vector3[顶点数];
        Vector2[] 紫外坐标 = new Vector2[顶点数];
        int[] 三角形 = new int[细分数 * 细分数 * 6];

        for (int z = 0; z <= 细分数; z++)
        {
            float v = z / (float)细分数;
            for (int x = 0; x <= 细分数; x++)
            {
                float u = x / (float)细分数;
                int 索引 = z * 每行顶点数 + x;
                顶点[索引] = new Vector3((u - 0.5f) * 局部边长, 0f, (v - 0.5f) * 局部边长);
                法线[索引] = Vector3.up;
                紫外坐标[索引] = new Vector2(u, v);
            }
        }

        int 三角形索引 = 0;
        for (int z = 0; z < 细分数; z++)
        {
            for (int x = 0; x < 细分数; x++)
            {
                int 左下 = z * 每行顶点数 + x;
                int 右下 = 左下 + 1;
                int 左上 = 左下 + 每行顶点数;
                int 右上 = 左上 + 1;

                // 从上方观察时为顺时针，保持法线朝上。
                三角形[三角形索引++] = 左下;
                三角形[三角形索引++] = 左上;
                三角形[三角形索引++] = 右下;
                三角形[三角形索引++] = 右下;
                三角形[三角形索引++] = 左上;
                三角形[三角形索引++] = 右上;
            }
        }

        共享网格 = new Mesh
        {
            name = "洪水浪头共享网格_32x32",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = 顶点,
            normals = 法线,
            uv = 紫外坐标,
            triangles = 三角形
        };
        共享网格.RecalculateBounds();
        return 共享网格;
    }
}

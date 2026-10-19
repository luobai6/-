using System;
using System.Collections.Generic;
using ShanheWeijin.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 创建地图南边界使用的单入口洪水出口河道资源。
/// 该工具只创建表现层与验证场景，不把出口解释为堤坝上的泄水口棋子。
/// </summary>
public static class OutletRiverAssetCreator
{
    private const string 资源目录 = "Assets/Art/河流/出口河流";
    private const string 彩图路径 = 资源目录 + "/T_River_Outlet_Albedo.png";
    private const string 控制图路径 = 资源目录 + "/T_River_Outlet_Control.tga";
    private const string 推进图路径 = 资源目录 + "/T_River_Outlet_NS_Advance.png";
    private const string 河床材质路径 = 资源目录 + "/M_RiverBed_Outlet.mat";
    private const string 河水材质路径 = 资源目录 + "/M_RiverWater_Outlet.mat";
    private const string 预制体路径 = 资源目录 + "/OutletRiver.prefab";
    private const string 地块定义路径 = 资源目录 + "/D_洪水出口.asset";

    private const string 验证资源目录 = "Assets/Art/MapValidation";
    private const string 验证场景路径 = "Assets/Scenes/OutletRiverArtValidation3x3.unity";
    private const string 验证关卡路径 = 验证资源目录 + "/L_洪水出口3x3验证.asset";
    private const string 验证出口定义路径 = 验证资源目录 + "/D_验证洪水出口.asset";
    private const string 普通定义路径 = 验证资源目录 + "/D_验证普通地块.asset";
    private const string 直线定义路径 = 验证资源目录 + "/D_验证直线河道.asset";
    private const string 白模材质路径 = "Assets/Art/MapWhitebox/M_白模底座.mat";
    private const string 直线预制体路径 = "Assets/Art/河流/直河流/StraightRiver.prefab";

    [MenuItem("山河未尽/河道工具/创建或更新洪水出口资源")]
    public static void 创建或更新()
    {
        try
        {
            执行创建或更新();
            GameObject 预制体 = AssetDatabase.LoadAssetAtPath<GameObject>(预制体路径);
            Selection.activeObject = 预制体;
            EditorGUIUtility.PingObject(预制体);
            Debug.Log($"洪水出口资源创建完成：{预制体路径}\n验证场景：{验证场景路径}");
        }
        catch (Exception 异常)
        {
            Debug.LogError($"创建洪水出口资源失败：{异常.Message}\n{异常.StackTrace}");
        }
    }

    /// <summary>供 Unity 批处理模式调用；失败时保留异常以返回非零退出码。</summary>
    public static void CreateOrUpdateBatch()
    {
        执行创建或更新();
        Debug.Log($"洪水出口资源批处理创建完成：{预制体路径}");
    }

    private static void 执行创建或更新()
    {
        AssetDatabase.Refresh();
        配置导入器(彩图路径, true, true, true);
        配置导入器(控制图路径, false, false, true);
        配置导入器(推进图路径, false, false, false);

        Texture2D 彩图 = 加载贴图(彩图路径);
        Texture2D 控制图 = 加载贴图(控制图路径);
        Texture2D 推进图 = 加载贴图(推进图路径);

        Shader 河床Shader = Shader.Find("ShanheWeijin/RiverBedParallaxOutlet");
        Shader 河水Shader = Shader.Find("ShanheWeijin/RiverWaterAndFlood");
        if (河床Shader == null || 河水Shader == null)
        {
            throw new InvalidOperationException("未找到河床或河水 Shader，请先处理 Console 中的 Shader 编译错误。");
        }

        Material 河床材质 = 取得或创建材质(河床材质路径, 河床Shader);
        河床材质.SetTexture("_BaseMap", 彩图);
        河床材质.SetTexture("_ControlMap", 控制图);
        河床材质.SetFloat("_ParallaxStrength", 0.016f);
        河床材质.SetFloat("_DepthDarkening", 0.10f);
        河床材质.SetFloat("_AlphaCutoff", 0.01f);

        Material 河水材质 = 取得或创建材质(河水材质路径, 河水Shader);
        河水材质.SetTexture("_ControlMap", 控制图);
        河水材质.SetTexture("_AdvanceMap", 推进图);
        // 出口是单向终点模块；反向图仅用白图占位，Prefab 上始终关闭反向推进。
        河水材质.SetTexture("_ReverseAdvanceMap", Texture2D.whiteTexture);
        河水材质.SetColor("_WaterColor", new Color(0.40f, 0.62f, 0.66f, 1f));
        河水材质.SetColor("_FloodColor", new Color(0.46f, 0.38f, 0.25f, 1f));
        河水材质.SetColor("_FoamColor", new Color(0.83f, 0.84f, 0.73f, 1f));
        河水材质.SetFloat("_Opacity", 0.44f);
        河水材质.SetFloat("_FloodArrivalProgress", 1f);
        河水材质.SetFloat("_ArrivalSoftness", 0.055f);
        河水材质.SetFloat("_ReverseAdvance", 0f);
        河水材质.SetFloat("_FloodProgress", 0f);
        河水材质.SetFloat("_FloodSoftness", 0.055f);
        河水材质.SetFloat("_MaxFloodExtent", 1f);
        河水材质.SetFloat("_Turbidity", 0.55f);
        河水材质.SetVector("_FlowDirection", new Vector4(0f, -1f, 0f, 0f));
        河水材质.SetFloat("_FlowSpeed", 0.075f);
        河水材质.SetFloat("_LargeRippleScale", 5f);
        河水材质.SetFloat("_SmallRippleScale", 14f);
        河水材质.SetFloat("_RippleStrength", 0.14f);
        河水材质.SetFloat("_FoamWidth", 0.14f);
        河水材质.SetFloat("_FloodWaveEnabled", 1f);
        河水材质.SetFloat("_FloodWaveHeight", 0.40f);
        河水材质.SetFloat("_FloodWaveWidth", 0.10f);
        河水材质.SetFloat("_FloodWaveFoamStrength", 1.15f);
        河水材质.SetFloat("_OuterBankImpactStrength", 0f);

        EditorUtility.SetDirty(河床材质);
        EditorUtility.SetDirty(河水材质);

        创建或替换预制体(河床材质, 河水材质);
        GameObject 出口预制体 = AssetDatabase.LoadAssetAtPath<GameObject>(预制体路径);
        取得或更新定义(地块定义路径, "洪水出口（北入南出）", new Color(0.51f, 0.64f, 0.63f, 1f), 出口预制体);
        创建或更新验证内容(出口预制体);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void 配置导入器(string 路径, bool 使用Srgb, bool 透明边缘, bool 保留输入Alpha)
    {
        TextureImporter 导入器 = AssetImporter.GetAtPath(路径) as TextureImporter;
        if (导入器 == null)
        {
            throw new InvalidOperationException($"无法读取贴图导入设置：{路径}");
        }

        bool 需要重导入 = false;
        if (导入器.sRGBTexture != 使用Srgb)
        {
            导入器.sRGBTexture = 使用Srgb;
            需要重导入 = true;
        }

        if (导入器.mipmapEnabled)
        {
            导入器.mipmapEnabled = false;
            需要重导入 = true;
        }

        if (导入器.alphaIsTransparency != 透明边缘)
        {
            导入器.alphaIsTransparency = 透明边缘;
            需要重导入 = true;
        }

        if (导入器.wrapMode != TextureWrapMode.Clamp)
        {
            导入器.wrapMode = TextureWrapMode.Clamp;
            需要重导入 = true;
        }

        if (导入器.textureCompression != TextureImporterCompression.Uncompressed)
        {
            导入器.textureCompression = TextureImporterCompression.Uncompressed;
            需要重导入 = true;
        }

        TextureImporterAlphaSource 目标Alpha = 保留输入Alpha
            ? TextureImporterAlphaSource.FromInput
            : TextureImporterAlphaSource.None;
        if (导入器.alphaSource != 目标Alpha)
        {
            导入器.alphaSource = 目标Alpha;
            需要重导入 = true;
        }

        if (需要重导入)
        {
            导入器.SaveAndReimport();
        }
    }

    private static Texture2D 加载贴图(string 路径)
    {
        Texture2D 贴图 = AssetDatabase.LoadAssetAtPath<Texture2D>(路径);
        if (贴图 == null)
        {
            throw new InvalidOperationException($"缺少贴图：{路径}");
        }

        return 贴图;
    }

    private static Material 取得或创建材质(string 路径, Shader shader)
    {
        Material 材质 = AssetDatabase.LoadAssetAtPath<Material>(路径);
        if (材质 == null)
        {
            材质 = new Material(shader);
            AssetDatabase.CreateAsset(材质, 路径);
        }
        else
        {
            材质.shader = shader;
        }

        材质.enableInstancing = true;
        return 材质;
    }

    private static void 创建或替换预制体(Material 河床材质, Material 河水材质)
    {
        GameObject 根物体 = new GameObject("OutletRiver");
        try
        {
            创建平面("RiverBed", 根物体.transform, 0.01f, 河床材质);
            GameObject 河水 = 创建平面("RiverWater", 根物体.transform, 0.02f, 河水材质);

            RiverVisualController 控制器 = 根物体.AddComponent<RiverVisualController>();
            SerializedObject 序列化控制器 = new SerializedObject(控制器);
            序列化控制器.FindProperty("河水渲染器").objectReferenceValue = 河水.GetComponent<Renderer>();
            序列化控制器.FindProperty("流动方向").vector2Value = Vector2.down;
            序列化控制器.FindProperty("流动速度").floatValue = 0.075f;
            序列化控制器.FindProperty("洪水到达进度").floatValue = 1f;
            序列化控制器.FindProperty("水头边缘柔和度").floatValue = 0.055f;
            序列化控制器.FindProperty("反向推进").boolValue = false;
            序列化控制器.FindProperty("洪水扩张进度").floatValue = 0f;
            序列化控制器.FindProperty("浑浊程度").floatValue = 0.55f;
            序列化控制器.FindProperty("启用鼓起浪头").boolValue = true;
            序列化控制器.FindProperty("浪头高度").floatValue = 0.40f;
            序列化控制器.FindProperty("浪头宽度").floatValue = 0.10f;
            序列化控制器.FindProperty("浪头水沫强度").floatValue = 1.15f;
            序列化控制器.FindProperty("弯道外岸冲击强度").floatValue = 0f;
            序列化控制器.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(根物体, 预制体路径);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(根物体);
        }
    }

    private static GameObject 创建平面(string 名称, Transform 父级, float 高度, Material 材质)
    {
        GameObject 平面 = GameObject.CreatePrimitive(PrimitiveType.Plane);
        平面.name = 名称;
        平面.transform.SetParent(父级, false);
        平面.transform.localPosition = new Vector3(0f, 高度, 0f);
        平面.transform.localRotation = Quaternion.identity;
        平面.transform.localScale = Vector3.one * 0.1f;

        Collider 碰撞体 = 平面.GetComponent<Collider>();
        if (碰撞体 != null)
        {
            UnityEngine.Object.DestroyImmediate(碰撞体);
        }

        平面.GetComponent<MeshRenderer>().sharedMaterial = 材质;
        return 平面;
    }

    private static TileDefinition 取得或更新定义(string 路径, string 名称, Color 颜色, GameObject 内容)
    {
        TileDefinition 定义 = AssetDatabase.LoadAssetAtPath<TileDefinition>(路径);
        if (定义 == null)
        {
            定义 = ScriptableObject.CreateInstance<TileDefinition>();
            AssetDatabase.CreateAsset(定义, 路径);
        }

        定义.name = 名称;
        定义.中文名称 = 名称;
        定义.白模颜色 = 颜色;
        定义.主要内容预制体 = 内容;
        EditorUtility.SetDirty(定义);
        return 定义;
    }

    private static void 创建或更新验证内容(GameObject 出口预制体)
    {
        Material 白模材质 = AssetDatabase.LoadAssetAtPath<Material>(白模材质路径);
        GameObject 直线预制体 = AssetDatabase.LoadAssetAtPath<GameObject>(直线预制体路径);
        if (白模材质 == null || 直线预制体 == null)
        {
            throw new InvalidOperationException("缺少白模材质或直线河道 Prefab，无法创建出口验证场景。");
        }

        TileDefinition 普通定义 = 取得或更新定义(
            普通定义路径, "验证普通地块", new Color(0.73f, 0.72f, 0.68f, 1f), null);
        TileDefinition 直线定义 = 取得或更新定义(
            直线定义路径, "验证直线河道", new Color(0.64f, 0.69f, 0.67f, 1f), 直线预制体);
        TileDefinition 出口定义 = 取得或更新定义(
            验证出口定义路径, "验证洪水出口", new Color(0.51f, 0.64f, 0.63f, 1f), 出口预制体);

        LevelData 验证关卡 = AssetDatabase.LoadAssetAtPath<LevelData>(验证关卡路径);
        if (验证关卡 == null)
        {
            验证关卡 = ScriptableObject.CreateInstance<LevelData>();
            AssetDatabase.CreateAsset(验证关卡, 验证关卡路径);
        }

        验证关卡.name = "L_洪水出口3x3验证";
        验证关卡.地图宽度 = 3;
        验证关卡.地图长度 = 3;
        验证关卡.地块列表 = new List<TileData>
        {
            新建地块(0, 0, 普通定义),
            新建地块(1, 0, 出口定义),
            新建地块(2, 0, 普通定义),
            新建地块(0, 1, 普通定义),
            新建地块(1, 1, 直线定义),
            新建地块(2, 1, 普通定义),
            新建地块(0, 2, 普通定义),
            新建地块(1, 2, 直线定义),
            新建地块(2, 2, 普通定义)
        };
        EditorUtility.SetDirty(验证关卡);
        AssetDatabase.SaveAssets();

        Scene 原场景 = SceneManager.GetActiveScene();
        Scene 验证场景 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(验证场景);

        try
        {
            GameObject 地图物体 = new GameObject("洪水出口3x3美术验证地图");
            MapGenerator 生成器 = 地图物体.AddComponent<MapGenerator>();
            SerializedObject 生成器序列化 = new SerializedObject(生成器);
            生成器序列化.FindProperty("关卡数据").objectReferenceValue = 验证关卡;
            生成器序列化.FindProperty("默认白模材质").objectReferenceValue = 白模材质;
            生成器序列化.ApplyModifiedPropertiesWithoutUndo();

            if (!生成器.重新生成())
            {
                throw new InvalidOperationException("洪水出口3×3验证地图生成失败，请检查 Console 中的中文错误信息。");
            }

            创建相机();
            创建灯光();
            if (!EditorSceneManager.SaveScene(验证场景, 验证场景路径))
            {
                throw new InvalidOperationException("洪水出口验证场景保存失败。");
            }
        }
        finally
        {
            if (原场景.IsValid() && 原场景.isLoaded)
            {
                SceneManager.SetActiveScene(原场景);
            }

            EditorSceneManager.CloseScene(验证场景, true);
        }
    }

    private static TileData 新建地块(int x, int z, TileDefinition 定义)
    {
        return new TileData
        {
            网格坐标 = new Vector2Int(x, z),
            高度等级 = 0,
            地块定义 = 定义
        };
    }

    private static void 创建相机()
    {
        GameObject 相机物体 = new GameObject("洪水出口验证正交相机");
        Camera 相机 = 相机物体.AddComponent<Camera>();
        相机.orthographic = true;
        相机.orthographicSize = 2.7f;
        相机.clearFlags = CameraClearFlags.SolidColor;
        相机.backgroundColor = new Color(0.48f, 0.48f, 0.46f, 1f);
        Vector3 地图中心 = new Vector3(1f, 0f, 1f);
        相机.transform.position = new Vector3(1f, 6f, -5f);
        相机.transform.rotation = Quaternion.LookRotation(地图中心 - 相机.transform.position);
        相机物体.AddComponent<OrthographicMapCameraController>();
    }

    private static void 创建灯光()
    {
        GameObject 灯光物体 = new GameObject("洪水出口验证方向光");
        Light 灯光 = 灯光物体.AddComponent<Light>();
        灯光.type = LightType.Directional;
        灯光.intensity = 1.2f;
        灯光物体.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
    }
}

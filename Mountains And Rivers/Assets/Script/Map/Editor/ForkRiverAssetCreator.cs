using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 根据已确认的三叉河道贴图创建或更新材质与 Prefab。
/// 该工具只处理表现资源，不写入河网连接或运行时洪水结果。
/// </summary>
public static class ForkRiverAssetCreator
{
    private const string 资源目录 = "Assets/Art/河流/三叉河流";
    private const string 彩图路径 = 资源目录 + "/T_River_Fork_NES_Albedo.png";
    private const string 控制图路径 = 资源目录 + "/T_River_Fork_NES_Control.tga";
    private const string 北侧入流图路径 = 资源目录 + "/T_River_Fork_NES_In_Advance.tga";
    private const string 北侧出流图路径 = 资源目录 + "/T_River_Fork_NES_N_Out_Advance.tga";
    private const string 东侧入流图路径 = 资源目录 + "/T_River_Fork_NES_E_In_Advance.tga";
    private const string 东侧出流图路径 = 资源目录 + "/T_River_Fork_NES_E_Advance.tga";
    private const string 南侧入流图路径 = 资源目录 + "/T_River_Fork_NES_S_In_Advance.tga";
    private const string 南侧出流图路径 = 资源目录 + "/T_River_Fork_NES_S_Advance.tga";
    private const string 河床材质路径 = 资源目录 + "/M_RiverBed_Fork_NES.mat";
    private const string 河水材质路径 = 资源目录 + "/M_RiverWater_Fork_NES.mat";
    private const string 预制体路径 = 资源目录 + "/ForkRiver_NES.prefab";

    [MenuItem("山河未尽/河道工具/创建或更新三叉河道NES资源")]
    public static void 创建或更新()
    {
        try
        {
            配置导入器(彩图路径, true, true);
            配置导入器(控制图路径, false, false);
            配置导入器(北侧入流图路径, false, false);
            配置导入器(北侧出流图路径, false, false);
            配置导入器(东侧入流图路径, false, false);
            配置导入器(东侧出流图路径, false, false);
            配置导入器(南侧入流图路径, false, false);
            配置导入器(南侧出流图路径, false, false);

            Texture2D 彩图 = 加载贴图(彩图路径);
            Texture2D 控制图 = 加载贴图(控制图路径);
            Texture2D 北侧入流图 = 加载贴图(北侧入流图路径);
            Texture2D 北侧出流图 = 加载贴图(北侧出流图路径);
            Texture2D 东侧入流图 = 加载贴图(东侧入流图路径);
            Texture2D 东侧出流图 = 加载贴图(东侧出流图路径);
            Texture2D 南侧入流图 = 加载贴图(南侧入流图路径);
            Texture2D 南侧出流图 = 加载贴图(南侧出流图路径);

            Shader 河床Shader = Shader.Find("ShanheWeijin/RiverBedParallax");
            Shader 河水Shader = Shader.Find("ShanheWeijin/ForkRiverWaterAndFlood");
            if (河床Shader == null || 河水Shader == null)
            {
                throw new InvalidOperationException("未找到河床或三叉河水 Shader，请先检查 Shader 编译错误。");
            }

            Material 河床材质 = 取得或创建材质(河床材质路径, 河床Shader);
            河床材质.SetTexture("_BaseMap", 彩图);
            河床材质.SetTexture("_ControlMap", 控制图);
            河床材质.SetFloat("_ParallaxStrength", 0.018f);
            河床材质.SetFloat("_DepthDarkening", 0.10f);
            河床材质.SetFloat("_AlphaCutoff", 0.01f);

            Material 河水材质 = 取得或创建材质(河水材质路径, 河水Shader);
            河水材质.SetTexture("_ControlMap", 控制图);
            河水材质.SetTexture("_NorthInAdvanceMap", 北侧入流图);
            河水材质.SetTexture("_NorthOutAdvanceMap", 北侧出流图);
            河水材质.SetTexture("_EastInAdvanceMap", 东侧入流图);
            河水材质.SetTexture("_EastOutAdvanceMap", 东侧出流图);
            河水材质.SetTexture("_SouthInAdvanceMap", 南侧入流图);
            河水材质.SetTexture("_SouthOutAdvanceMap", 南侧出流图);
            河水材质.SetFloat("_IncomingPort", 0f);
            河水材质.SetFloat("_EntryArrivalProgress", 0f);
            河水材质.SetFloat("_NorthExitProgress", 0f);
            河水材质.SetFloat("_EastExitProgress", 0f);
            河水材质.SetFloat("_SouthExitProgress", 0f);
            河水材质.SetFloat("_ArrivalSoftness", 0.04f);
            河水材质.SetFloat("_FloodProgress", 0f);
            河水材质.SetFloat("_FloodSoftness", 0.045f);
            河水材质.SetFloat("_MaxFloodExtent", 1f);
            河水材质.SetFloat("_Turbidity", 0.35f);
            河水材质.SetFloat("_FloodWaveEnabled", 1f);
            河水材质.SetFloat("_FloodWaveHeight", 0.35f);
            河水材质.SetFloat("_FloodWaveWidth", 0.08f);
            河水材质.SetFloat("_FloodWaveFoamStrength", 1f);
            河水材质.SetFloat("_ForkImpactStrength", 1f);
            河水材质.SetFloat("_ForkImpactHeightMultiplier", 1.5f);

            EditorUtility.SetDirty(河床材质);
            EditorUtility.SetDirty(河水材质);
            创建或替换预制体(河床材质, 河水材质);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject 预制体 = AssetDatabase.LoadAssetAtPath<GameObject>(预制体路径);
            Selection.activeObject = 预制体;
            EditorGUIUtility.PingObject(预制体);
            Debug.Log($"三叉河道 NES 资源创建完成：{预制体路径}");
        }
        catch (Exception 异常)
        {
            Debug.LogError($"创建三叉河道 NES 资源失败：{异常.Message}\n{异常.StackTrace}");
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

    private static void 配置导入器(string 路径, bool 使用Srgb, bool 透明边缘)
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

        if (导入器.alphaSource != TextureImporterAlphaSource.FromInput)
        {
            导入器.alphaSource = TextureImporterAlphaSource.FromInput;
            需要重导入 = true;
        }

        if (需要重导入)
        {
            导入器.SaveAndReimport();
        }
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
        GameObject 根物体 = new GameObject("ForkRiver_NES");
        try
        {
            创建平面("RiverBed", 根物体.transform, 0.01f, 河床材质);
            GameObject 河水 = 创建平面("RiverWater", 根物体.transform, 0.02f, 河水材质);

            ForkRiverVisualController 控制器 = 根物体.AddComponent<ForkRiverVisualController>();
            SerializedObject 序列化控制器 = new SerializedObject(控制器);
            序列化控制器.FindProperty("河水渲染器").objectReferenceValue = 河水.GetComponent<Renderer>();
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
}

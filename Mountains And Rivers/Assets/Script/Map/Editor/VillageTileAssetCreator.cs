using System;
using System.Collections.Generic;
using System.IO;
using ShanheWeijin.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 导入常态村落 A 的四层透明画片，并创建村落预制体、地块定义与 3×3 美术验证场景。
/// 只负责表现层，不添加洪水、破坏或玩法逻辑。
/// </summary>
public static class VillageTileAssetCreator
{
    private const string 资源目录 = "Assets/Art/村落/常态村落A";
    private const string 后层路径 = 资源目录 + "/T_Village_Normal_A_Back.png";
    private const string 主层路径 = 资源目录 + "/T_Village_Normal_A_Main.png";
    private const string 前层路径 = 资源目录 + "/T_Village_Normal_A_Front.png";
    private const string 炊烟路径 = 资源目录 + "/T_Village_Normal_A_Smoke.png";
    private const string 预制体路径 = 资源目录 + "/VillageTile_Normal_A.prefab";
    private const string 地块定义路径 = 资源目录 + "/D_常态村落_A.asset";
    private const string 验证关卡路径 = 资源目录 + "/L_常态村落_A_3x3验证.asset";
    private const string 验证场景路径 = "Assets/Scenes/VillageTileNormalAValidation3x3.unity";

    private const string 普通地块定义路径 = "Assets/Art/Map/Terrain/Definitions/D_普通地块_A_静地.asset";
    private const string 地块材质路径 = "Assets/Art/Map/Terrain/Materials/M_Terrain_Base_A.mat";
    private const string 自动创建标记 = "山河未尽.常态村落A资源已自动创建.v1";

    [InitializeOnLoadMethod]
    private static void 缺失时自动创建()
    {
        EditorApplication.delayCall += () =>
        {
            bool 资源已齐全 =
                AssetDatabase.LoadAssetAtPath<GameObject>(预制体路径) != null &&
                AssetDatabase.LoadAssetAtPath<TileDefinition>(地块定义路径) != null &&
                AssetDatabase.LoadAssetAtPath<LevelData>(验证关卡路径) != null &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(验证场景路径) != null;
            if (SessionState.GetBool(自动创建标记, false) || 资源已齐全)
            {
                return;
            }

            SessionState.SetBool(自动创建标记, true);
            if (!File.Exists(后层路径) || !File.Exists(主层路径) ||
                !File.Exists(前层路径) || !File.Exists(炊烟路径))
            {
                Debug.LogWarning("常态村落 A 自动创建已跳过：四张透明画片尚未齐全。");
                return;
            }

            try
            {
                执行创建或更新();
                Debug.Log("常态村落 A 缺失资源已自动创建，可打开 3×3 验证场景检查层次与尺度。");
            }
            catch (Exception 异常)
            {
                Debug.LogError($"常态村落 A 自动创建失败：{异常.Message}\n{异常.StackTrace}");
            }
        };
    }

    [MenuItem("山河未尽/美术/创建或更新常态村落A")]
    public static void 创建或更新()
    {
        try
        {
            执行创建或更新();
            GameObject 预制体 = AssetDatabase.LoadAssetAtPath<GameObject>(预制体路径);
            Selection.activeObject = 预制体;
            EditorGUIUtility.PingObject(预制体);
            Debug.Log($"常态村落 A 已创建：{预制体路径}\n验证场景：{验证场景路径}");
        }
        catch (Exception 异常)
        {
            Debug.LogError($"创建常态村落 A 失败：{异常.Message}\n{异常.StackTrace}");
        }
    }

    /// <summary>供 Unity 批处理或自动化验收调用；失败时保留异常。</summary>
    public static void CreateOrUpdateBatch()
    {
        执行创建或更新();
        Debug.Log($"常态村落 A 批处理创建完成：{预制体路径}");
    }

    private static void 执行创建或更新()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("请退出播放模式后创建常态村落资源。");
        }

        AssetDatabase.Refresh();
        Sprite 后层 = 导入画片(后层路径);
        Sprite 主层 = 导入画片(主层路径);
        Sprite 前层 = 导入画片(前层路径);
        Sprite 炊烟 = 导入画片(炊烟路径);

        GameObject 预制体 = 创建或替换预制体(后层, 主层, 前层, 炊烟);
        TileDefinition 村落定义 = 创建或更新地块定义(预制体);
        创建或更新验证内容(村落定义);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static Sprite 导入画片(string 路径)
    {
        AssetDatabase.ImportAsset(路径, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter 导入器 = AssetImporter.GetAtPath(路径) as TextureImporter;
        if (导入器 == null)
        {
            throw new InvalidOperationException($"无法读取村落画片：{路径}");
        }

        Texture2D 贴图 = AssetDatabase.LoadAssetAtPath<Texture2D>(路径);
        if (贴图 == null || 贴图.width <= 0)
        {
            throw new InvalidOperationException($"村落画片无效：{路径}");
        }

        // 每张画片都归一为约一格宽，后续替换分辨率时不改变场景尺寸。
        导入器.textureType = TextureImporterType.Sprite;
        导入器.spriteImportMode = SpriteImportMode.Single;
        导入器.spritePixelsPerUnit = 贴图.width;
        导入器.alphaSource = TextureImporterAlphaSource.FromInput;
        导入器.alphaIsTransparency = true;
        导入器.mipmapEnabled = false;
        导入器.sRGBTexture = true;
        导入器.wrapMode = TextureWrapMode.Clamp;
        导入器.filterMode = FilterMode.Bilinear;
        导入器.textureCompression = TextureImporterCompression.Uncompressed;
        导入器.SaveAndReimport();

        Sprite 精灵 = AssetDatabase.LoadAssetAtPath<Sprite>(路径);
        if (精灵 == null)
        {
            throw new InvalidOperationException($"村落画片未能导入为 Sprite：{路径}");
        }

        return 精灵;
    }

    private static GameObject 创建或替换预制体(Sprite 后层, Sprite 主层, Sprite 前层, Sprite 炊烟)
    {
        GameObject 根物体 = new GameObject("VillageTile_Normal_A");
        try
        {
            创建画片(根物体.transform, "BackVillage", 后层,
                new Vector3(0f, 0.205f, 0.16f), 0.94f, 10, Color.white);
            创建画片(根物体.transform, "MainVillage", 主层,
                new Vector3(0f, 0.225f, 0.015f), 0.98f, 20, Color.white);
            创建画片(根物体.transform, "FrontVillage", 前层,
                new Vector3(0f, 0.19f, -0.15f), 1.02f, 30, Color.white);
            创建画片(根物体.transform, "VillageSmoke", 炊烟,
                new Vector3(0.015f, 0.34f, -0.01f), 0.72f, 40,
                new Color(0.92f, 0.94f, 0.91f, 0.62f));

            GameObject 保存结果 = PrefabUtility.SaveAsPrefabAsset(根物体, 预制体路径);
            if (保存结果 == null)
            {
                throw new InvalidOperationException("常态村落预制体保存失败。");
            }

            return 保存结果;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(根物体);
        }
    }

    private static SpriteRenderer 创建画片(
        Transform 父级,
        string 名称,
        Sprite 精灵,
        Vector3 局部位置,
        float 缩放,
        int 排序值,
        Color 颜色)
    {
        GameObject 画片 = new GameObject(名称);
        画片.transform.SetParent(父级, false);
        画片.transform.localPosition = 局部位置;
        画片.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
        画片.transform.localScale = Vector3.one * 缩放;

        SpriteRenderer 渲染器 = 画片.AddComponent<SpriteRenderer>();
        渲染器.sprite = 精灵;
        渲染器.color = 颜色;
        渲染器.sortingOrder = 排序值;
        渲染器.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        渲染器.receiveShadows = false;
        return 渲染器;
    }

    private static TileDefinition 创建或更新地块定义(GameObject 预制体)
    {
        TileDefinition 定义 = AssetDatabase.LoadAssetAtPath<TileDefinition>(地块定义路径);
        if (定义 == null)
        {
            定义 = ScriptableObject.CreateInstance<TileDefinition>();
            AssetDatabase.CreateAsset(定义, 地块定义路径);
        }

        定义.name = "D_常态村落_A";
        定义.中文名称 = "常态村落 A（安居）";
        定义.白模颜色 = new Color(0.66f, 0.65f, 0.50f, 1f);
        定义.主要内容预制体 = 预制体;
        EditorUtility.SetDirty(定义);
        return 定义;
    }

    private static void 创建或更新验证内容(TileDefinition 村落定义)
    {
        TileDefinition 普通定义 = AssetDatabase.LoadAssetAtPath<TileDefinition>(普通地块定义路径);
        Material 地块材质 = AssetDatabase.LoadAssetAtPath<Material>(地块材质路径);
        if (普通定义 == null || 地块材质 == null)
        {
            throw new InvalidOperationException("无法创建村落验证场景：普通地块定义或正式地块材质缺失。");
        }

        LevelData 验证关卡 = AssetDatabase.LoadAssetAtPath<LevelData>(验证关卡路径);
        if (验证关卡 == null)
        {
            验证关卡 = ScriptableObject.CreateInstance<LevelData>();
            AssetDatabase.CreateAsset(验证关卡, 验证关卡路径);
        }

        验证关卡.name = "L_常态村落_A_3x3验证";
        验证关卡.地图宽度 = 3;
        验证关卡.地图长度 = 3;
        验证关卡.地块列表 = new List<TileData>();
        for (int z = 0; z < 3; z++)
        {
            for (int x = 0; x < 3; x++)
            {
                验证关卡.地块列表.Add(new TileData
                {
                    网格坐标 = new Vector2Int(x, z),
                    高度等级 = 0,
                    地块定义 = x == 1 && z == 1 ? 村落定义 : 普通定义
                });
            }
        }
        EditorUtility.SetDirty(验证关卡);
        AssetDatabase.SaveAssets();

        Scene 原场景 = SceneManager.GetActiveScene();
        Scene 验证场景 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(验证场景);
        try
        {
            GameObject 地图物体 = new GameObject("常态村落A_3x3验证地图");
            MapGenerator 生成器 = 地图物体.AddComponent<MapGenerator>();
            SerializedObject 序列化生成器 = new SerializedObject(生成器);
            序列化生成器.FindProperty("关卡数据").objectReferenceValue = 验证关卡;
            序列化生成器.FindProperty("地块尺寸").floatValue = 1f;
            序列化生成器.FindProperty("每级高度").floatValue = 0.5f;
            序列化生成器.FindProperty("地图基准高度").floatValue = -0.25f;
            序列化生成器.FindProperty("默认白模材质").objectReferenceValue = 地块材质;
            序列化生成器.ApplyModifiedPropertiesWithoutUndo();

            if (!生成器.重新生成())
            {
                throw new InvalidOperationException("常态村落 A 的 3×3 地图生成失败。");
            }

            创建验证相机();
            创建验证灯光();
            if (!EditorSceneManager.SaveScene(验证场景, 验证场景路径))
            {
                throw new InvalidOperationException("常态村落 A 验证场景保存失败。");
            }
        }
        finally
        {
            if (原场景.IsValid() && 原场景.isLoaded)
            {
                SceneManager.SetActiveScene(原场景);
            }

            if (验证场景.IsValid() && 验证场景.isLoaded)
            {
                EditorSceneManager.CloseScene(验证场景, true);
            }
        }
    }

    private static void 创建验证相机()
    {
        GameObject 相机物体 = new GameObject("常态村落A验证正交相机");
        Camera 相机 = 相机物体.AddComponent<Camera>();
        相机.orthographic = true;
        相机.orthographicSize = 2.45f;
        相机.clearFlags = CameraClearFlags.SolidColor;
        相机.backgroundColor = new Color(0.48f, 0.49f, 0.46f, 1f);
        Vector3 中心 = new Vector3(1f, 0.18f, 1f);
        相机物体.transform.position = new Vector3(1f, 5.8f, -4.7f);
        相机物体.transform.rotation = Quaternion.LookRotation(中心 - 相机物体.transform.position);
        相机物体.AddComponent<OrthographicMapCameraController>();
    }

    private static void 创建验证灯光()
    {
        GameObject 灯光物体 = new GameObject("常态村落A验证方向光");
        Light 灯光 = 灯光物体.AddComponent<Light>();
        灯光.type = LightType.Directional;
        灯光.intensity = 0.78f;
        灯光.color = new Color(1f, 0.95f, 0.86f, 1f);
        灯光物体.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
    }
}

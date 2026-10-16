using System.Collections.Generic;
using ShanheWeijin.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>创建独立示例资产与场景，不修改当前正在编辑的地块场景。</summary>
public static class WhiteboxSampleCreator
{
    private const string Root = "Assets/Art/MapWhitebox";
    private const string ScenePath = "Assets/Scenes/WhiteboxMapSample.unity";
    private const string MaterialPath = Root + "/M_白模底座.mat";
    private const string PlainDefinitionPath = Root + "/D_普通地块.asset";
    private const string RiverDefinitionPath = Root + "/D_直线河道.asset";
    private const string LevelPath = Root + "/L_白模测试.asset";
    private const string RiverPrefabPath = "Assets/Art/Effects/River/StraightRiver.prefab";

    [MenuItem("山河未尽/白模地图/创建示例场景")]
    public static void 创建示例场景()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("请退出播放模式后创建白模示例场景。");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            Debug.LogWarning($"示例场景已存在，为保护已有修改未覆盖：{ScenePath}");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            return;
        }

        GameObject 河流预制体 = AssetDatabase.LoadAssetAtPath<GameObject>(RiverPrefabPath);
        Shader 白模着色器 = Shader.Find("Universal Render Pipeline/Lit");
        if (河流预制体 == null || 白模着色器 == null)
        {
            Debug.LogError("无法创建白模示例：请确认 StraightRiver 预制体和 URP/Lit Shader 存在。");
            return;
        }

        if (!AssetDatabase.IsValidFolder(Root))
        {
            AssetDatabase.CreateFolder("Assets/Art", "MapWhitebox");
        }

        Material 白模材质 = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (白模材质 == null)
        {
            白模材质 = new Material(白模着色器) { name = "M_白模底座" };
            AssetDatabase.CreateAsset(白模材质, MaterialPath);
        }

        TileDefinition 普通定义 = 取得或创建定义(PlainDefinitionPath, "普通地块",
            new Color(0.73f, 0.72f, 0.68f, 1f), null);
        TileDefinition 河流定义 = 取得或创建定义(RiverDefinitionPath, "南北直线河道",
            new Color(0.64f, 0.69f, 0.67f, 1f), 河流预制体);

        LevelData 关卡 = AssetDatabase.LoadAssetAtPath<LevelData>(LevelPath);
        if (关卡 == null)
        {
            关卡 = ScriptableObject.CreateInstance<LevelData>();
            关卡.name = "L_白模测试";
            关卡.地图宽度 = 3;
            关卡.地图长度 = 3;
            关卡.地块列表 = new List<TileData>();
            for (int z = 0; z < 3; z++)
            {
                for (int x = 0; x < 3; x++)
                {
                    关卡.地块列表.Add(new TileData
                    {
                        网格坐标 = new Vector2Int(x, z),
                        高度等级 = (x == 2 && z != 1) ? 1 : 0,
                        地块定义 = x == 1 && z == 1 ? 河流定义 : 普通定义
                    });
                }
            }
            AssetDatabase.CreateAsset(关卡, LevelPath);
        }
        AssetDatabase.SaveAssets();

        Scene 原场景 = SceneManager.GetActiveScene();
        Scene 示例场景 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(示例场景);

        try
        {
            GameObject 地图物体 = new GameObject("白模地图生成器");
            MapGenerator 生成器 = 地图物体.AddComponent<MapGenerator>();
            SerializedObject 生成器序列化 = new SerializedObject(生成器);
            生成器序列化.FindProperty("关卡数据").objectReferenceValue = 关卡;
            生成器序列化.FindProperty("默认白模材质").objectReferenceValue = 白模材质;
            生成器序列化.ApplyModifiedPropertiesWithoutUndo();

            GameObject 相机物体 = new GameObject("白模正交相机");
            Camera 相机 = 相机物体.AddComponent<Camera>();
            相机.orthographic = true;
            相机.orthographicSize = 4.2f;
            相机.clearFlags = CameraClearFlags.SolidColor;
            相机.backgroundColor = new Color(0.48f, 0.48f, 0.46f, 1f);
            相机物体.transform.position = new Vector3(1f, 7f, -7f);
            相机物体.transform.rotation = Quaternion.LookRotation(new Vector3(1f, 0f, 1f) - 相机物体.transform.position);
            相机物体.AddComponent<OrthographicMapCameraController>();

            GameObject 灯光物体 = new GameObject("白模方向光");
            Light 灯光 = 灯光物体.AddComponent<Light>();
            灯光.type = LightType.Directional;
            灯光.intensity = 1.2f;
            灯光物体.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            if (!生成器.重新生成())
            {
                throw new System.InvalidOperationException("示例地图生成失败；请检查 Console 中的中文错误信息。");
            }

            if (!EditorSceneManager.SaveScene(示例场景, ScenePath))
            {
                throw new System.InvalidOperationException("示例场景保存失败。");
            }

            Debug.Log($"白模示例创建完成：{ScenePath}。双击场景后进入 Play Mode 测试相机。", 生成器);
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
        }
        catch (System.Exception 异常)
        {
            Debug.LogError($"创建白模示例失败：{异常}");
        }
        finally
        {
            if (原场景.IsValid() && 原场景.isLoaded)
            {
                SceneManager.SetActiveScene(原场景);
            }
            EditorSceneManager.CloseScene(示例场景, true);
        }
    }

    [MenuItem("山河未尽/白模地图/刷新示例场景")]
    public static void 刷新示例场景()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("请退出播放模式后刷新白模示例场景。");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            Debug.LogWarning("尚无白模示例场景，请先执行“创建示例场景”。");
            return;
        }

        Scene 原场景 = SceneManager.GetActiveScene();
        Scene 示例场景 = SceneManager.GetSceneByPath(ScenePath);
        bool 已由工具打开 = !示例场景.IsValid() || !示例场景.isLoaded;
        if (已由工具打开)
        {
            示例场景 = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        try
        {
            if (示例场景.isDirty)
            {
                Debug.LogWarning("示例场景有未保存的修改，请先自行保存或放弃修改，再执行刷新。");
                return;
            }

            MapGenerator 生成器 = null;
            foreach (GameObject 根物体 in 示例场景.GetRootGameObjects())
            {
                生成器 = 根物体.GetComponent<MapGenerator>();
                if (生成器 != null) break;
            }

            if (生成器 == null || !生成器.重新生成())
            {
                Debug.LogError("白模示例刷新失败；场景中缺少有效的地图生成器。");
                return;
            }

            EditorSceneManager.SaveScene(示例场景);
            Debug.Log("白模示例已刷新并保存，地块颜色会在重新打开场景后恢复。", 生成器);
        }
        finally
        {
            if (已由工具打开)
            {
                if (原场景.IsValid() && 原场景.isLoaded)
                {
                    SceneManager.SetActiveScene(原场景);
                }
                EditorSceneManager.CloseScene(示例场景, true);
            }
        }
    }

    private static TileDefinition 取得或创建定义(string 路径, string 名称, Color 颜色, GameObject 内容)
    {
        TileDefinition 已存在 = AssetDatabase.LoadAssetAtPath<TileDefinition>(路径);
        if (已存在 != null)
        {
            return 已存在;
        }

        TileDefinition 定义 = ScriptableObject.CreateInstance<TileDefinition>();
        定义.name = 名称;
        定义.中文名称 = 名称;
        定义.白模颜色 = 颜色;
        定义.主要内容预制体 = 内容;
        AssetDatabase.CreateAsset(定义, 路径);
        return 定义;
    }
}

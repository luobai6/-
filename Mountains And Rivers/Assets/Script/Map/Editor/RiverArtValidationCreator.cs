using System.Collections.Generic;
using ShanheWeijin.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 创建独立的 3×3 河道美术验证场景，用于检查拼接、透明边缘、旋转和洪水表现。
/// 该工具不修改正式关卡，也不把测试用旋转规则写入运行时数据。
/// </summary>
public static class RiverArtValidationCreator
{
    private const string 验证资源目录 = "Assets/Art/MapValidation";
    private const string 验证场景路径 = "Assets/Scenes/RiverArtValidation3x3.unity";
    private const string 验证关卡路径 = 验证资源目录 + "/L_河道3x3美术验证.asset";

    private const string 普通定义路径 = 验证资源目录 + "/D_验证普通地块.asset";
    private const string 直线定义路径 = 验证资源目录 + "/D_验证直线河道.asset";
    private const string 转弯定义路径 = 验证资源目录 + "/D_验证转弯河道.asset";
    private const string 山地定义路径 = 验证资源目录 + "/D_验证山地.asset";

    private const string 白模材质路径 = "Assets/Art/MapWhitebox/M_白模底座.mat";
    private const string 直线预制体路径 = "Assets/Art/河流/直河流/StraightRiver.prefab";
    private const string 转弯预制体路径 = "Assets/Art/河流/转弯河流/CornerRiver_NE.prefab";
    private const string 山地预制体路径 = "Assets/Art/山/山1/Miuntain.prefab";

    [MenuItem("山河未尽/美术验证/创建河道3×3验证场景")]
    public static void 创建验证场景()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("请退出播放模式后创建河道美术验证场景。");
            return;
        }

        SceneAsset 已有场景 = AssetDatabase.LoadAssetAtPath<SceneAsset>(验证场景路径);
        if (已有场景 != null)
        {
            修正已有验证场景();
            return;
        }

        Material 白模材质 = AssetDatabase.LoadAssetAtPath<Material>(白模材质路径);
        GameObject 直线预制体 = AssetDatabase.LoadAssetAtPath<GameObject>(直线预制体路径);
        GameObject 转弯预制体 = AssetDatabase.LoadAssetAtPath<GameObject>(转弯预制体路径);
        GameObject 山地预制体 = AssetDatabase.LoadAssetAtPath<GameObject>(山地预制体路径);

        if (白模材质 == null || 直线预制体 == null || 转弯预制体 == null || 山地预制体 == null)
        {
            Debug.LogError("无法创建河道3×3验证场景：请确认白模材质、直线河道、转弯河道和山地预制体均存在。");
            return;
        }

        确保资源目录存在();

        TileDefinition 普通定义 = 取得或更新定义(
            普通定义路径, "验证普通地块", new Color(0.73f, 0.72f, 0.68f, 1f), null);
        TileDefinition 直线定义 = 取得或更新定义(
            直线定义路径, "验证直线河道", new Color(0.64f, 0.69f, 0.67f, 1f), 直线预制体);
        TileDefinition 转弯定义 = 取得或更新定义(
            转弯定义路径, "验证转弯河道", new Color(0.64f, 0.69f, 0.67f, 1f), 转弯预制体);
        TileDefinition 山地定义 = 取得或更新定义(
            山地定义路径, "验证山地", new Color(0.61f, 0.62f, 0.56f, 1f), 山地预制体);

        LevelData 验证关卡 = ScriptableObject.CreateInstance<LevelData>();
        验证关卡.name = "L_河道3x3美术验证";
        验证关卡.地图宽度 = 3;
        验证关卡.地图长度 = 3;
        验证关卡.地块列表 = 创建地块列表(普通定义, 直线定义, 转弯定义, 山地定义);
        AssetDatabase.CreateAsset(验证关卡, 验证关卡路径);
        AssetDatabase.SaveAssets();

        Scene 原场景 = SceneManager.GetActiveScene();
        Scene 验证场景 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(验证场景);

        try
        {
            GameObject 地图物体 = new GameObject("河道3x3美术验证地图");
            MapGenerator 生成器 = 地图物体.AddComponent<MapGenerator>();
            SerializedObject 生成器序列化 = new SerializedObject(生成器);
            生成器序列化.FindProperty("关卡数据").objectReferenceValue = 验证关卡;
            生成器序列化.FindProperty("默认白模材质").objectReferenceValue = 白模材质;
            生成器序列化.ApplyModifiedPropertiesWithoutUndo();

            if (!生成器.重新生成())
            {
                throw new System.InvalidOperationException("河道3×3验证地图生成失败，请检查 Console 中的中文错误信息。");
            }

            // 右侧直线河道由南北方向旋转为东西方向。
            Transform 横向河道锚点 = 地图物体.transform.Find("GeneratedMap/TileRoot_2_1/PrimaryContent");
            if (横向河道锚点 == null || 横向河道锚点.childCount == 0)
            {
                throw new System.InvalidOperationException("未找到右侧直线河道实例，无法完成横向旋转。");
            }
            横向河道锚点.GetChild(0).localRotation = Quaternion.Euler(0f, 90f, 0f);

            // 当前转弯河道原图的开口方向与验证路径相反，需要旋转 180° 后连接北侧与东侧河道。
            Transform 转弯河道锚点 = 地图物体.transform.Find("GeneratedMap/TileRoot_1_1/PrimaryContent");
            if (转弯河道锚点 == null || 转弯河道锚点.childCount == 0)
            {
                throw new System.InvalidOperationException("未找到中央转弯河道实例，无法完成 180° 旋转。");
            }
            转弯河道锚点.GetChild(0).localRotation = Quaternion.Euler(0f, 180f, 0f);

            创建相机();
            创建灯光();

            if (!EditorSceneManager.SaveScene(验证场景, 验证场景路径))
            {
                throw new System.InvalidOperationException("河道3×3验证场景保存失败。");
            }

            Debug.Log($"河道3×3美术验证场景创建完成：{验证场景路径}");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(验证场景路径));
        }
        catch (System.Exception 异常)
        {
            Debug.LogError($"创建河道3×3验证场景失败：{异常}");
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

    [MenuItem("山河未尽/美术验证/修正已有河道3×3验证场景")]
    public static void 修正已有验证场景()
    {
        SceneAsset 已有场景 = AssetDatabase.LoadAssetAtPath<SceneAsset>(验证场景路径);
        if (已有场景 == null)
        {
            Debug.LogWarning("尚未创建河道3×3验证场景，请先执行创建命令。");
            return;
        }

        Scene 原活动场景 = SceneManager.GetActiveScene();
        Scene 验证场景 = SceneManager.GetSceneByPath(验证场景路径);
        bool 由工具打开 = !验证场景.IsValid() || !验证场景.isLoaded;
        if (由工具打开)
        {
            验证场景 = EditorSceneManager.OpenScene(验证场景路径, OpenSceneMode.Additive);
        }

        try
        {
            GameObject 地图物体 = null;
            Camera 验证相机 = null;
            foreach (GameObject 根物体 in 验证场景.GetRootGameObjects())
            {
                if (根物体.name == "河道3x3美术验证地图")
                {
                    地图物体 = 根物体;
                }
                else if (根物体.name == "河道验证正交相机")
                {
                    验证相机 = 根物体.GetComponent<Camera>();
                }
            }

            if (地图物体 == null || 验证相机 == null)
            {
                throw new System.InvalidOperationException("验证场景缺少地图或正交相机，无法自动修正。");
            }

            Transform 横向河道锚点 = 地图物体.transform.Find("GeneratedMap/TileRoot_2_1/PrimaryContent");
            Transform 转弯河道锚点 = 地图物体.transform.Find("GeneratedMap/TileRoot_1_1/PrimaryContent");
            if (横向河道锚点 == null || 横向河道锚点.childCount == 0 ||
                转弯河道锚点 == null || 转弯河道锚点.childCount == 0)
            {
                throw new System.InvalidOperationException("验证场景中的河道层级不完整，无法自动修正。");
            }

            横向河道锚点.GetChild(0).localRotation = Quaternion.Euler(0f, 90f, 0f);
            转弯河道锚点.GetChild(0).localRotation = Quaternion.Euler(0f, 180f, 0f);
            配置验证相机(验证相机);

            EditorSceneManager.MarkSceneDirty(验证场景);
            EditorSceneManager.SaveScene(验证场景);
            Debug.Log("已修正河道3×3验证场景：相机与网格轴对齐，中央转弯河道旋转 180°。");
            EditorGUIUtility.PingObject(已有场景);
        }
        catch (System.Exception 异常)
        {
            Debug.LogError($"修正河道3×3验证场景失败：{异常}");
        }
        finally
        {
            if (原活动场景.IsValid() && 原活动场景.isLoaded)
            {
                SceneManager.SetActiveScene(原活动场景);
            }

            if (由工具打开 && 验证场景.IsValid() && 验证场景.isLoaded)
            {
                EditorSceneManager.CloseScene(验证场景, true);
            }
        }
    }

    private static List<TileData> 创建地块列表(
        TileDefinition 普通定义,
        TileDefinition 直线定义,
        TileDefinition 转弯定义,
        TileDefinition 山地定义)
    {
        return new List<TileData>
        {
            // 南排（Z=0）
            新建地块(0, 0, 普通定义),
            新建地块(1, 0, 普通定义),
            新建地块(2, 0, 山地定义),

            // 中排（Z=1）：山地、北转东河道、东西直河道
            新建地块(0, 1, 山地定义),
            新建地块(1, 1, 转弯定义),
            新建地块(2, 1, 直线定义),

            // 北排（Z=2）：普通地块、南北直河道、山地
            新建地块(0, 2, 普通定义),
            新建地块(1, 2, 直线定义),
            新建地块(2, 2, 山地定义)
        };
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

    private static void 确保资源目录存在()
    {
        if (!AssetDatabase.IsValidFolder(验证资源目录))
        {
            AssetDatabase.CreateFolder("Assets/Art", "MapValidation");
        }
    }

    private static void 创建相机()
    {
        GameObject 相机物体 = new GameObject("河道验证正交相机");
        Camera 相机 = 相机物体.AddComponent<Camera>();
        配置验证相机(相机);
        相机物体.AddComponent<OrthographicMapCameraController>();
    }

    private static void 配置验证相机(Camera 相机)
    {
        相机.orthographic = true;
        相机.orthographicSize = 2.7f;
        相机.clearFlags = CameraClearFlags.SolidColor;
        相机.backgroundColor = new Color(0.48f, 0.48f, 0.46f, 1f);
        // 相机沿地图南北轴观察，避免地块在画面中呈菱形；X 边保持水平，Z 边保持竖直。
        Vector3 地图中心 = new Vector3(1f, 0f, 1f);
        相机.transform.position = new Vector3(1f, 6f, -5f);
        相机.transform.rotation = Quaternion.LookRotation(地图中心 - 相机.transform.position);
    }

    private static void 创建灯光()
    {
        GameObject 灯光物体 = new GameObject("河道验证方向光");
        Light 灯光 = 灯光物体.AddComponent<Light>();
        灯光.type = LightType.Directional;
        灯光.intensity = 1.2f;
        灯光物体.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
    }
}

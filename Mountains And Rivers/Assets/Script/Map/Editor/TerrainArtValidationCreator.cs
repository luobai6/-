using System.Collections.Generic;
using ShanheWeijin.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 创建普通地块正式皮肤的独立验证场景。
/// 只使用美术验证数据，不修改正式关卡或河道出入口资源。
/// </summary>
public static class TerrainArtValidationCreator
{
    private const string 验证目录 = "Assets/Art/Map/Terrain/Validation";
    private const string 验证关卡路径 = 验证目录 + "/L_普通地块3x3验证.asset";
    private const string 验证场景路径 = "Assets/Scenes/TerrainArtValidation3x3.unity";
    private const string 普通地块定义路径 = "Assets/Art/Map/Terrain/Definitions/D_普通地块_A_静地.asset";
    private const string 正式地块材质路径 = "Assets/Art/Map/Terrain/Materials/M_Terrain_Base_A.mat";
    private const string 山体预制体路径 = "Assets/Art/山/山1/Miuntain.prefab";
    private const string 暖岸占位材质路径 = 验证目录 + "/M_暖石河岸_仅供配色验证.mat";
    private const string 水面占位材质路径 = 验证目录 + "/M_临时水面_仅供配色验证.mat";

    [MenuItem("山河未尽/美术验证/创建或更新普通地块统一皮肤3×3验证场景")]
    public static void CreateOrUpdateValidationScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("请退出播放模式后创建普通地块验证场景。");
            return;
        }

        TileDefinition 普通地块定义 = AssetDatabase.LoadAssetAtPath<TileDefinition>(普通地块定义路径);
        if (普通地块定义 == null)
        {
            Debug.LogError("无法创建普通地块验证场景：普通地块定义缺失。");
            return;
        }

        确保验证目录存在();
        Material 正式地块材质 = AssetDatabase.LoadAssetAtPath<Material>(正式地块材质路径);
        if (正式地块材质 == null)
        {
            Debug.LogError($"无法创建普通地块验证场景：正式材质缺失，路径为 {正式地块材质路径}。");
            return;
        }

        LevelData 验证关卡 = 创建或更新验证关卡(普通地块定义);
        Scene 原场景 = SceneManager.GetActiveScene();
        Scene 验证场景 = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(验证场景);

        try
        {
            GameObject 地图物体 = new GameObject("普通地块统一皮肤3x3验证地图");
            MapGenerator 生成器 = 地图物体.AddComponent<MapGenerator>();
            SerializedObject 生成器序列化 = new SerializedObject(生成器);
            生成器序列化.FindProperty("关卡数据").objectReferenceValue = 验证关卡;
            生成器序列化.FindProperty("地块尺寸").floatValue = 1f;
            生成器序列化.FindProperty("每级高度").floatValue = 0.5f;
            生成器序列化.FindProperty("地图基准高度").floatValue = -0.25f;
            生成器序列化.FindProperty("默认白模材质").objectReferenceValue = 正式地块材质;
            生成器序列化.ApplyModifiedPropertiesWithoutUndo();

            if (!生成器.重新生成())
            {
                throw new System.InvalidOperationException("普通地块3×3地图生成失败，请检查 Console 中的中文诊断。");
            }

            创建视觉参照(地图物体.transform);
            创建相机();
            创建灯光();

            if (!EditorSceneManager.SaveScene(验证场景, 验证场景路径))
            {
                throw new System.InvalidOperationException("普通地块验证场景保存失败。");
            }

            Debug.Log($"普通地块统一皮肤3×3验证场景创建完成：{验证场景路径}。当前使用方正阶梯与统一纹理，不包含圆缓坡肩；色值仍可结合山体参照继续调整。");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(验证场景路径));
        }
        catch (System.Exception 异常)
        {
            Debug.LogError($"创建普通地块验证场景失败：{异常}");
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

    private static LevelData 创建或更新验证关卡(TileDefinition 普通地块定义)
    {
        LevelData 验证关卡 = AssetDatabase.LoadAssetAtPath<LevelData>(验证关卡路径);
        if (验证关卡 == null)
        {
            验证关卡 = ScriptableObject.CreateInstance<LevelData>();
            AssetDatabase.CreateAsset(验证关卡, 验证关卡路径);
        }

        验证关卡.name = "L_普通地块3x3验证";
        验证关卡.地图宽度 = 3;
        验证关卡.地图长度 = 3;
        验证关卡.地块列表 = new List<TileData>
        {
            新建地块(0, 0, 0, 普通地块定义),
            新建地块(1, 0, 0, 普通地块定义),
            新建地块(2, 0, 0, 普通地块定义),
            新建地块(0, 1, 0, 普通地块定义),
            新建地块(1, 1, 0, 普通地块定义),
            新建地块(2, 1, 0, 普通地块定义),
            新建地块(0, 2, 0, 普通地块定义),
            新建地块(1, 2, 1, 普通地块定义),
            新建地块(2, 2, 2, 普通地块定义)
        };
        EditorUtility.SetDirty(验证关卡);
        AssetDatabase.SaveAssets();
        return 验证关卡;
    }

    private static TileData 新建地块(int x, int z, int 高度, TileDefinition 定义)
    {
        return new TileData
        {
            网格坐标 = new Vector2Int(x, z),
            高度等级 = 高度,
            地块定义 = 定义
        };
    }

    private static void 创建相机()
    {
        GameObject 相机物体 = new GameObject("普通地块统一皮肤验证正交相机");
        Camera 相机 = 相机物体.AddComponent<Camera>();
        相机.orthographic = true;
        相机.orthographicSize = 3.05f;
        相机.clearFlags = CameraClearFlags.SolidColor;
        相机.backgroundColor = new Color(0.48f, 0.49f, 0.46f, 1f);
        Vector3 地图中心 = new Vector3(1f, 0.28f, 1.05f);
        相机.transform.position = new Vector3(1f, 6.15f, -5.45f);
        相机.transform.rotation = Quaternion.LookRotation(地图中心 - 相机.transform.position);
        相机物体.AddComponent<OrthographicMapCameraController>();
    }

    private static void 创建视觉参照(Transform 父级)
    {
        GameObject 参照根节点 = new GameObject("统一地块皮肤美术参照_不参与玩法");
        参照根节点.transform.SetParent(父级, false);

        Material 暖岸材质 = 创建或更新纯色材质(
            暖岸占位材质路径,
            "M_暖石河岸_仅供配色验证",
            new Color(0.765f, 0.702f, 0.557f, 1f));
        Material 水面材质 = 创建或更新纯色材质(
            水面占位材质路径,
            "M_临时水面_仅供配色验证",
            new Color(0.34f, 0.50f, 0.51f, 1f));

        创建无碰撞带状物("暖石河岸_仅供配色验证", 参照根节点.transform,
            new Vector3(0f, 0.012f, 0.55f), new Vector3(0.42f, 0.018f, 2.05f), 暖岸材质);
        创建无碰撞带状物("临时水面_仅供冷暖分离验证", 参照根节点.transform,
            new Vector3(0f, 0.024f, 0.55f), new Vector3(0.25f, 0.018f, 2.05f), 水面材质);

        GameObject 山体预制体 = AssetDatabase.LoadAssetAtPath<GameObject>(山体预制体路径);
        if (山体预制体 == null)
        {
            Debug.LogWarning($"未找到山体参照：{山体预制体路径}。场景仍会生成，但无法检查山脚接地与综合色调。");
            return;
        }

        GameObject 山体参照 = (GameObject)PrefabUtility.InstantiatePrefab(山体预制体);
        山体参照.name = "现有山体综合色调参照";
        山体参照.transform.SetParent(参照根节点.transform, false);
        山体参照.transform.localPosition = new Vector3(0.92f, 0.035f, 0.62f);
        山体参照.transform.localRotation = Quaternion.Euler(0f, 8f, 0f);
        山体参照.transform.localScale = Vector3.one * 1.15f;
    }

    private static void 创建无碰撞带状物(string 名称, Transform 父级, Vector3 位置, Vector3 尺寸, Material 材质)
    {
        GameObject 带状物 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        带状物.name = 名称;
        带状物.transform.SetParent(父级, false);
        带状物.transform.localPosition = 位置;
        带状物.transform.localScale = 尺寸;
        带状物.GetComponent<MeshRenderer>().sharedMaterial = 材质;
        Object.DestroyImmediate(带状物.GetComponent<Collider>());
    }

    private static Material 创建或更新纯色材质(string 路径, string 名称, Color 颜色)
    {
        Material 材质 = AssetDatabase.LoadAssetAtPath<Material>(路径);
        if (材质 == null)
        {
            Shader 着色器 = Shader.Find("Universal Render Pipeline/Unlit");
            if (着色器 == null)
            {
                着色器 = Shader.Find("Unlit/Color");
            }

            if (着色器 == null)
            {
                throw new System.InvalidOperationException("无法创建验证材质：未找到 URP Unlit 或 Unlit/Color Shader。");
            }

            材质 = new Material(着色器);
            AssetDatabase.CreateAsset(材质, 路径);
        }

        材质.name = 名称;
        if (材质.HasProperty("_BaseColor"))
        {
            材质.SetColor("_BaseColor", 颜色);
        }
        if (材质.HasProperty("_Color"))
        {
            材质.SetColor("_Color", 颜色);
        }
        EditorUtility.SetDirty(材质);
        return 材质;
    }

    private static void 创建灯光()
    {
        GameObject 灯光物体 = new GameObject("普通地块验证方向光");
        Light 灯光 = 灯光物体.AddComponent<Light>();
        灯光.type = LightType.Directional;
        灯光.intensity = 0.8f;
        灯光.color = new Color(1f, 0.95f, 0.86f, 1f);
        灯光物体.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
    }

    private static void 确保验证目录存在()
    {
        if (!AssetDatabase.IsValidFolder(验证目录))
        {
            AssetDatabase.CreateFolder("Assets/Art/Map/Terrain", "Validation");
        }
    }
}

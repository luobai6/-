using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 检查普通地块统一皮肤的资源接线，不参与运行时玩法。
/// </summary>
public static class TerrainSkinResourceValidator
{
    private const string 着色器名称 = "ShanheWeijin/TerrainTileSkin";
    private const string 材质路径 = "Assets/Art/Map/Terrain/Materials/M_Terrain_Base_A.mat";
    private const string 验证场景路径 = "Assets/Scenes/TerrainArtValidation3x3.unity";

    private static readonly string[] 必需属性 =
    {
        "_TopMap",
        "_SideMap",
        "_EdgeMask",
        "_TopTint",
        "_SideTopTint",
        "_SideBottomTint",
        "_EdgeTint",
        "_EdgeLineTint",
        "_EdgeWidth",
        "_EdgeLineWidth",
        "_VerticalEdgeLineWidth",
        "_EdgeMaskWorldScale",
        "_TopEdgeFlags",
        "_SideGradientStrength",
        "_WorldScale"
    };

    [MenuItem("山河未尽/美术验证/检查普通地块统一皮肤资源")]
    public static void ValidateResources()
    {
        List<string> 错误 = new List<string>();
        Shader 着色器 = Shader.Find(着色器名称);
        if (着色器 == null)
        {
            错误.Add($"未找到 Shader：{着色器名称}");
        }

        Material 材质 = AssetDatabase.LoadAssetAtPath<Material>(材质路径);
        if (材质 == null)
        {
            错误.Add($"未找到正式地块材质：{材质路径}");
        }
        else
        {
            if (着色器 != null && 材质.shader != 着色器)
            {
                错误.Add($"正式地块材质没有使用 {着色器名称}。");
            }

            foreach (string 属性 in 必需属性)
            {
                if (!材质.HasProperty(属性))
                {
                    错误.Add($"正式地块材质缺少 Shader 属性：{属性}");
                }
            }
        }

        检查验证场景(错误);

        if (错误.Count > 0)
        {
            Debug.LogError("普通地块统一皮肤资源检查失败：\n- " + string.Join("\n- ", 错误));
            return;
        }

        Debug.Log("普通地块统一皮肤资源检查通过：正式材质字段完整，验证场景中没有圆缓坡肩对象或资源引用。");
    }

    private static void 检查验证场景(List<string> 错误)
    {
        SceneAsset 场景资源 = AssetDatabase.LoadAssetAtPath<SceneAsset>(验证场景路径);
        if (场景资源 == null)
        {
            错误.Add($"未找到验证场景：{验证场景路径}");
            return;
        }

        Scene 已打开场景 = SceneManager.GetSceneByPath(验证场景路径);
        bool 原本已打开 = 已打开场景.IsValid() && 已打开场景.isLoaded;
        Scene 场景 = 原本已打开
            ? 已打开场景
            : EditorSceneManager.OpenScene(验证场景路径, OpenSceneMode.Additive);

        try
        {
            foreach (GameObject 根对象 in 场景.GetRootGameObjects())
            {
                foreach (Transform 子对象 in 根对象.GetComponentsInChildren<Transform>(true))
                {
                    if (子对象.name.Contains("坡肩"))
                    {
                        错误.Add($"验证场景仍包含坡肩对象：{子对象.name}");
                    }

                    MeshFilter 网格 = 子对象.GetComponent<MeshFilter>();
                    if (网格 != null && 网格.sharedMesh != null)
                    {
                        string 路径 = AssetDatabase.GetAssetPath(网格.sharedMesh);
                        if (路径.Contains("Mesh_圆缓坡肩"))
                        {
                            错误.Add($"验证场景仍引用圆缓坡肩网格：{路径}");
                        }
                    }
                }
            }
        }
        finally
        {
            if (!原本已打开 && 场景.IsValid() && 场景.isLoaded)
            {
                EditorSceneManager.CloseScene(场景, true);
            }
        }
    }
}

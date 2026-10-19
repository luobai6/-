using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>渲染常态村落 A 的独立 3×3 验证预览，不修改用户当前场景。</summary>
public static class VillageTilePreviewCapture
{
    private const string 验证场景路径 = "Assets/Scenes/VillageTileNormalAValidation3x3.unity";
    private const string 预览图路径 = "Assets/Art/村落/常态村落A/VillageTile_Normal_A_Preview.png";
    private const string 自动捕获标记 = "山河未尽.常态村落A预览已自动捕获.v2";

    [InitializeOnLoadMethod]
    private static void 缺失时自动捕获()
    {
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(自动捕获标记, false) ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(验证场景路径) == null)
            {
                return;
            }

            SessionState.SetBool(自动捕获标记, true);
            try
            {
                捕获预览();
            }
            catch (Exception 异常)
            {
                Debug.LogError($"常态村落 A 预览捕获失败：{异常.Message}\n{异常.StackTrace}");
            }
        };
    }

    [MenuItem("山河未尽/美术验证/捕获常态村落A预览图")]
    public static void 捕获预览()
    {
        Scene 原活动场景 = SceneManager.GetActiveScene();
        Scene 验证场景 = EditorSceneManager.OpenScene(验证场景路径, OpenSceneMode.Additive);
        try
        {
            Camera 验证相机 = null;
            foreach (GameObject 根物体 in 验证场景.GetRootGameObjects())
            {
                Camera 候选 = 根物体.GetComponentInChildren<Camera>(true);
                if (候选 != null)
                {
                    验证相机 = 候选;
                    break;
                }
            }

            if (验证相机 == null)
            {
                throw new InvalidOperationException("验证场景中没有找到 Camera。");
            }

            const int 尺寸 = 1024;
            RenderTexture 渲染目标 = new RenderTexture(尺寸, 尺寸, 24, RenderTextureFormat.ARGB32);
            Texture2D 预览图 = new Texture2D(尺寸, 尺寸, TextureFormat.RGBA32, false, false);
            RenderTexture 原活动目标 = RenderTexture.active;
            RenderTexture 原相机目标 = 验证相机.targetTexture;
            int 原相机剔除遮罩 = 验证相机.cullingMask;
            Dictionary<GameObject, int> 原图层 = new Dictionary<GameObject, int>();
            try
            {
                // 验证场景以 Additive 方式打开；临时放到独立层，避免拍进用户当前场景。
                const int 独立预览层 = 31;
                foreach (GameObject 根物体 in 验证场景.GetRootGameObjects())
                {
                    记录并设置图层(根物体, 独立预览层, 原图层);
                }
                验证相机.cullingMask = 1 << 独立预览层;
                验证相机.targetTexture = 渲染目标;
                RenderTexture.active = 渲染目标;
                验证相机.Render();
                预览图.ReadPixels(new Rect(0, 0, 尺寸, 尺寸), 0, 0);
                预览图.Apply(false, false);
                File.WriteAllBytes(预览图路径, 预览图.EncodeToPNG());
            }
            finally
            {
                验证相机.targetTexture = 原相机目标;
                验证相机.cullingMask = 原相机剔除遮罩;
                foreach (KeyValuePair<GameObject, int> 项 in 原图层)
                {
                    if (项.Key != null)
                    {
                        项.Key.layer = 项.Value;
                    }
                }
                RenderTexture.active = 原活动目标;
                UnityEngine.Object.DestroyImmediate(渲染目标);
                UnityEngine.Object.DestroyImmediate(预览图);
            }

            AssetDatabase.ImportAsset(预览图路径, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"常态村落 A 预览图已生成：{预览图路径}");
        }
        finally
        {
            if (验证场景.IsValid() && 验证场景.isLoaded)
            {
                EditorSceneManager.CloseScene(验证场景, true);
            }

            if (原活动场景.IsValid() && 原活动场景.isLoaded)
            {
                SceneManager.SetActiveScene(原活动场景);
            }
        }
    }

    private static void 记录并设置图层(GameObject 物体, int 图层, IDictionary<GameObject, int> 原图层)
    {
        原图层[物体] = 物体.layer;
        物体.layer = 图层;
        foreach (Transform 子物体 in 物体.transform)
        {
            记录并设置图层(子物体.gameObject, 图层, 原图层);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using ShanheWeijin.Map;
using UnityEditor;
using UnityEngine;

/// <summary>导入源头瀑布帧，并创建可直接放入地块的预制体与地块定义。</summary>
public static class RiverSourceWaterfallAssetCreator
{
    private const string 根目录 = "Assets/Art/河流/源头瀑布";
    private const string 常态目录 = 根目录 + "/帧序列/常态瀑布";
    private const string 洪水涌出目录 = 根目录 + "/帧序列/洪水涌出";
    private const string 山谷后层路径 = 根目录 + "/山谷/T_River_Source_Valley_Back.png";
    private const string 山谷中层路径 = 根目录 + "/山谷/T_River_Source_Valley_Middle.png";
    private const string 山谷前层路径 = 根目录 + "/山谷/T_River_Source_Valley_Front.png";
    private const string 预制体路径 = 根目录 + "/RiverSourceWaterfall.prefab";
    private const string 地块定义路径 = 根目录 + "/D_河流源头.asset";
    private const string 自动创建标记 = "山河未尽.源头瀑布三层山谷资源已自动创建.v3";

    [InitializeOnLoadMethod]
    private static void 缺失时自动创建()
    {
        EditorApplication.delayCall += () =>
        {
            GameObject 现有预制体 = AssetDatabase.LoadAssetAtPath<GameObject>(预制体路径);
            bool 已有山谷分层 = 现有预制体 != null &&
                现有预制体.transform.Find("ValleyBack") != null &&
                现有预制体.transform.Find("ValleyMiddle") != null &&
                现有预制体.transform.Find("ValleyFront") != null;
            if (SessionState.GetBool(自动创建标记, false) || 已有山谷分层)
            {
                return;
            }

            SessionState.SetBool(自动创建标记, true);
            if (Directory.Exists(常态目录) && Directory.Exists(洪水涌出目录))
            {
                创建或更新();
            }
        };
    }

    [MenuItem("山河未尽/美术/创建或更新源头瀑布")]
    public static void 创建或更新()
    {
        try
        {
            Sprite[] 常态帧 = 导入帧组(常态目录, "T_River_Source_Normal_");
            Sprite[] 洪水涌出帧 = 导入帧组(洪水涌出目录, "T_River_Source_FloodSurge_");
            Sprite 山谷后层 = 导入单张画片(山谷后层路径, 100f);
            Sprite 山谷中层 = 导入单张画片(山谷中层路径, 100f);
            Sprite 山谷前层 = 导入单张画片(山谷前层路径, 100f);
            if (常态帧.Length != 8 || 洪水涌出帧.Length != 16)
            {
                Debug.LogError($"创建源头瀑布失败：需要常态 8 帧、洪水涌出 16 帧，当前为 {常态帧.Length} 和 {洪水涌出帧.Length}。");
                return;
            }
            if (山谷后层 == null || 山谷中层 == null || 山谷前层 == null)
            {
                Debug.LogError("创建源头瀑布失败：缺少山谷后层、中层或前层透明画片。");
                return;
            }

            GameObject 根物体 = new GameObject("RiverSourceWaterfall");
            try
            {
                创建画片(根物体.transform, "ValleyBack", 山谷后层,
                    new Vector3(0f, 0.29f, 0.24f), 0.075f, 10);

                创建画片(根物体.transform, "ValleyMiddle", 山谷中层,
                    new Vector3(0f, 0.25f, 0.12f), 0.07f, 15);

                SpriteRenderer 渲染器 = 创建画片(根物体.transform, "WaterfallSprite", 常态帧[0],
                    new Vector3(0f, 0.30f, 0f), 0.28f, 20);

                创建画片(根物体.transform, "ValleyFront", 山谷前层,
                    new Vector3(0f, 0.22f, -0.18f), 0.075f, 30);

                RiverSourceWaterfallController 控制器 = 根物体.AddComponent<RiverSourceWaterfallController>();
                SerializedObject 序列化控制器 = new SerializedObject(控制器);
                序列化控制器.FindProperty("瀑布渲染器").objectReferenceValue = 渲染器;
                写入帧组(序列化控制器.FindProperty("常态瀑布帧"), 常态帧);
                写入帧组(序列化控制器.FindProperty("洪水涌出帧"), 洪水涌出帧);
                序列化控制器.ApplyModifiedPropertiesWithoutUndo();

                GameObject 预制体 = PrefabUtility.SaveAsPrefabAsset(根物体, 预制体路径);
                创建或更新地块定义(预制体);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(根物体);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("源头瀑布已创建：山谷后层、中层、瀑布动画、山谷前层已分开导入；常态 8 帧循环，洪水涌出 16 帧播放后停在末帧。");
        }
        catch (Exception 异常)
        {
            Debug.LogError("创建源头瀑布资源失败：" + 异常);
        }
    }

    private static Sprite[] 导入帧组(string 目录, string 文件名前缀)
    {
        string[] 文件 = Directory.GetFiles(目录, 文件名前缀 + "*.png", SearchOption.TopDirectoryOnly);
        Array.Sort(文件, StringComparer.Ordinal);
        List<Sprite> 帧 = new List<Sprite>();
        foreach (string 系统路径 in 文件)
        {
            if (系统路径.EndsWith("SpriteSheet.png", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string 资源路径 = 系统路径.Replace('\\', '/');
            TextureImporter 导入器 = AssetImporter.GetAtPath(资源路径) as TextureImporter;
            if (导入器 == null)
            {
                AssetDatabase.ImportAsset(资源路径, ImportAssetOptions.ForceSynchronousImport);
                导入器 = AssetImporter.GetAtPath(资源路径) as TextureImporter;
            }

            if (导入器 == null)
            {
                continue;
            }

            导入器.textureType = TextureImporterType.Sprite;
            导入器.spriteImportMode = SpriteImportMode.Single;
            导入器.spritePixelsPerUnit = 256f;
            导入器.alphaIsTransparency = true;
            导入器.mipmapEnabled = false;
            导入器.filterMode = FilterMode.Bilinear;
            导入器.textureCompression = TextureImporterCompression.Uncompressed;
            导入器.SaveAndReimport();

            Sprite 精灵 = AssetDatabase.LoadAssetAtPath<Sprite>(资源路径);
            if (精灵 != null)
            {
                帧.Add(精灵);
            }
        }
        return 帧.ToArray();
    }

    private static Sprite 导入单张画片(string 资源路径, float 每单位像素)
    {
        AssetDatabase.ImportAsset(资源路径, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter 导入器 = AssetImporter.GetAtPath(资源路径) as TextureImporter;
        if (导入器 == null)
        {
            return null;
        }

        导入器.textureType = TextureImporterType.Sprite;
        导入器.spriteImportMode = SpriteImportMode.Single;
        导入器.spritePixelsPerUnit = 每单位像素;
        导入器.alphaIsTransparency = true;
        导入器.mipmapEnabled = false;
        导入器.filterMode = FilterMode.Bilinear;
        导入器.textureCompression = TextureImporterCompression.Uncompressed;
        导入器.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(资源路径);
    }

    private static SpriteRenderer 创建画片(
        Transform 父物体,
        string 名称,
        Sprite 精灵,
        Vector3 局部位置,
        float 缩放,
        int 排序值)
    {
        GameObject 画片 = new GameObject(名称);
        画片.transform.SetParent(父物体, false);
        画片.transform.localPosition = 局部位置;
        画片.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
        画片.transform.localScale = Vector3.one * 缩放;

        SpriteRenderer 渲染器 = 画片.AddComponent<SpriteRenderer>();
        渲染器.sprite = 精灵;
        渲染器.sortingOrder = 排序值;
        return 渲染器;
    }

    private static void 写入帧组(SerializedProperty 数组属性, IReadOnlyList<Sprite> 帧组)
    {
        数组属性.arraySize = 帧组.Count;
        for (int i = 0; i < 帧组.Count; i++)
        {
            数组属性.GetArrayElementAtIndex(i).objectReferenceValue = 帧组[i];
        }
    }

    private static void 创建或更新地块定义(GameObject 预制体)
    {
        TileDefinition 定义 = AssetDatabase.LoadAssetAtPath<TileDefinition>(地块定义路径);
        if (定义 == null)
        {
            定义 = ScriptableObject.CreateInstance<TileDefinition>();
            AssetDatabase.CreateAsset(定义, 地块定义路径);
        }

        定义.中文名称 = "河流源头（瀑布）";
        定义.白模颜色 = new Color(0.48f, 0.58f, 0.55f, 1f);
        定义.主要内容预制体 = 预制体;
        EditorUtility.SetDirty(定义);
    }
}

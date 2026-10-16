using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace ShanheWeijin.Map
{
    /// <summary>将关卡数据生成可检查的白模层级；只管理自己的 GeneratedMap 子节点。</summary>
    public sealed class MapGenerator : MonoBehaviour
    {
        [Header("关卡数据")]
        [SerializeField, Tooltip("用于生成地图的 LevelData 资产。")]
        private LevelData 关卡数据;

        [Header("白模尺寸")]
        [SerializeField, Min(0.01f), Tooltip("单格地块在 X、Z 平面上的世界尺寸。")]
        private float 地块尺寸 = 1f;

        [SerializeField, Min(0.01f), Tooltip("高度等级每增加 1，顶面抬高的距离。")]
        private float 每级高度 = 0.5f;

        [SerializeField, Tooltip("白模底座统一从这个 Y 高度延伸至地块顶面；须低于所有地块顶面。")]
        private float 地图基准高度 = -0.25f;

        [SerializeField, Tooltip("白模底座使用的 URP 材质；每格颜色由 MaterialPropertyBlock 覆盖。")]
        private Material 默认白模材质;

        [SerializeField, HideInInspector]
        private Transform 生成结果根节点;

        public float 计算地块顶面高度(int 高度等级) => 高度等级 * 每级高度;

        public Vector3 计算地块中心(Vector2Int 网格坐标)
        {
            return transform.TransformPoint(new Vector3(
                网格坐标.x * 地块尺寸,
                0f,
                网格坐标.y * 地块尺寸));
        }

        public bool 重新生成()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Debug.LogWarning("白模生成只在编辑状态使用。", this);
                return false;
            }

            if (!验证整体参数())
            {
                return false;
            }

            GameObject 新结果 = new GameObject("GeneratedMap");
            新结果.transform.SetParent(transform, false);
            int 已生成 = 0;
            int 已跳过 = 0;
            HashSet<Vector2Int> 已占用坐标 = new HashSet<Vector2Int>();

            try
            {
                foreach (TileData 地块 in 关卡数据.地块列表)
                {
                    if (地块 == null)
                    {
                        已跳过++;
                        Debug.LogWarning("地块列表中存在空数据项，已跳过。", this);
                        continue;
                    }

                    Vector2Int 坐标 = 地块.网格坐标;
                    if (坐标.x < 0 || 坐标.y < 0 || 坐标.x >= 关卡数据.地图宽度 || 坐标.y >= 关卡数据.地图长度)
                    {
                        已跳过++;
                        Debug.LogWarning($"地块 ({坐标.x}, {坐标.y}) 超出地图范围，已跳过。", this);
                        continue;
                    }

                    if (已占用坐标.Contains(坐标))
                    {
                        已跳过++;
                        Debug.LogWarning($"地块 ({坐标.x}, {坐标.y}) 坐标重复，仅保留首次出现的地块。", this);
                        continue;
                    }

                    float 顶面高度 = 计算地块顶面高度(地块.高度等级);
                    if (float.IsNaN(顶面高度) || float.IsInfinity(顶面高度) || 顶面高度 <= 地图基准高度)
                    {
                        已跳过++;
                        Debug.LogWarning($"地块 ({坐标.x}, {坐标.y}) 的顶面不高于地图基准高度，已跳过。", this);
                        continue;
                    }

                    已占用坐标.Add(坐标);
                    创建地块(新结果.transform, 地块, 顶面高度);
                    已生成++;
                }
            }
            catch (Exception 异常)
            {
                DestroyImmediate(新结果);
                Debug.LogError($"白模生成失败，原地图已保留：{异常}", this);
                return false;
            }

            int 撤销组 = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("重新生成白模地图");
            Undo.RecordObject(this, "记录白模生成结果");
            if (是自己生成的根节点(生成结果根节点))
            {
                Undo.DestroyObjectImmediate(生成结果根节点.gameObject);
            }

            Undo.RegisterCreatedObjectUndo(新结果, "重新生成白模地图");
            生成结果根节点 = 新结果.transform;
            Undo.CollapseUndoOperations(撤销组);
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log($"白模生成完成：成功 {已生成} 格，跳过 {已跳过} 格。", this);
            return true;
#else
            Debug.LogWarning("白模生成器需要在 Unity 编辑器中使用。", this);
            return false;
#endif
        }

        public void 清除生成结果()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Debug.LogWarning("请在编辑状态清除白模生成结果。", this);
                return;
            }

            if (!是自己生成的根节点(生成结果根节点))
            {
                Debug.LogWarning("没有可清除的本生成器结果。", this);
                return;
            }

            Undo.RecordObject(this, "清除白模生成结果");
            Undo.DestroyObjectImmediate(生成结果根节点.gameObject);
            生成结果根节点 = null;
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        private bool 验证整体参数()
        {
            if (关卡数据 == null || 关卡数据.地图宽度 <= 0 || 关卡数据.地图长度 <= 0 ||
                关卡数据.地块列表 == null || float.IsNaN(地块尺寸) || float.IsInfinity(地块尺寸) || 地块尺寸 <= 0f ||
                float.IsNaN(每级高度) || float.IsInfinity(每级高度) || 每级高度 <= 0f ||
                float.IsNaN(地图基准高度) || float.IsInfinity(地图基准高度) ||
                默认白模材质 == null)
            {
                Debug.LogError("白模参数无效：请指定关卡数据和白模材质，并检查地图尺寸、地块尺寸、每级高度及地图基准高度。原地图未更改。", this);
                return false;
            }

            return true;
        }

        private bool 是自己生成的根节点(Transform 候选)
        {
            return 候选 != null && 候选.parent == transform && 候选.name == "GeneratedMap";
        }

        private void 创建地块(Transform 地图根节点, TileData 地块, float 顶面高度)
        {
            Vector2Int 坐标 = 地块.网格坐标;
            GameObject 地块根节点 = new GameObject($"TileRoot_{坐标.x}_{坐标.y}");
            地块根节点.transform.SetParent(地图根节点, false);
            地块根节点.transform.localPosition = new Vector3(坐标.x * 地块尺寸, 0f, 坐标.y * 地块尺寸);

            GameObject 底座 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            底座.name = "白模底座";
            底座.transform.SetParent(地块根节点.transform, false);
            底座.transform.localPosition = new Vector3(0f, (地图基准高度 + 顶面高度) * 0.5f, 0f);
            底座.transform.localScale = new Vector3(地块尺寸, 顶面高度 - 地图基准高度, 地块尺寸);

            MeshRenderer 渲染器 = 底座.GetComponent<MeshRenderer>();
            渲染器.sharedMaterial = 默认白模材质;
            底座.AddComponent<WhiteboxTileColor>().设置颜色(
                地块.地块定义 != null ? 地块.地块定义.白模颜色 : Color.white);

            GameObject 内容锚点 = new GameObject("PrimaryContent");
            内容锚点.transform.SetParent(地块根节点.transform, false);
            内容锚点.transform.localPosition = new Vector3(0f, 顶面高度, 0f);

            GameObject 内容预制体 = 地块.地块定义 != null ? 地块.地块定义.主要内容预制体 : null;
            if (内容预制体 != null)
            {
#if UNITY_EDITOR
                GameObject 实例 = PrefabUtility.InstantiatePrefab(内容预制体, 内容锚点.transform) as GameObject;
                if (实例 == null)
                {
                    throw new InvalidOperationException($"地块 ({坐标.x}, {坐标.y}) 的主要内容不是有效预制体。");
                }
#endif
            }
        }
    }
}

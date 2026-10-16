using System;
using UnityEngine;

namespace ShanheWeijin.Map
{
    /// <summary>单个地块的关卡数据；网格坐标的 Y 对应场景 Z 轴。</summary>
    [Serializable]
    public sealed class TileData
    {
        [Tooltip("地图网格中的 X、Z 坐标；这里的 Y 对应场景 Z 轴。")]
        public Vector2Int 网格坐标;

        [Tooltip("离散地势等级，实际高度由地图生成器统一换算。")]
        public int 高度等级;

        [Tooltip("该地块的白模颜色和主要内容预制体；留空时只生成默认底座。")]
        public TileDefinition 地块定义;
    }
}

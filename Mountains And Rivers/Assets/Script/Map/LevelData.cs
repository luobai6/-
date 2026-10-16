using System.Collections.Generic;
using UnityEngine;

namespace ShanheWeijin.Map
{
    /// <summary>地图数据是关卡的来源；生成后的场景层级仅用于表现。</summary>
    [CreateAssetMenu(fileName = "关卡数据", menuName = "山河未尽/关卡数据")]
    public sealed class LevelData : ScriptableObject
    {
        [Min(1)]
        [Tooltip("地图在 X 方向拥有的网格数量。")]
        public int 地图宽度 = 3;

        [Min(1)]
        [Tooltip("地图在 Z 方向拥有的网格数量。")]
        public int 地图长度 = 3;

        [Tooltip("按列表顺序生成地块；重复坐标只采用首次出现的数据。")]
        public List<TileData> 地块列表 = new List<TileData>();
    }
}

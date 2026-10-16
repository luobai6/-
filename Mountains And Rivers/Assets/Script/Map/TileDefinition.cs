using UnityEngine;

namespace ShanheWeijin.Map
{
    /// <summary>地块视觉定义；不保存坐标、高度或运行时水量。</summary>
    [CreateAssetMenu(fileName = "地块定义", menuName = "山河未尽/地块定义")]
    public sealed class TileDefinition : ScriptableObject
    {
        [Tooltip("在关卡编辑器和团队文档中显示的地块名称。")]
        public string 中文名称 = "普通地块";

        [Tooltip("白模底座顶面和侧面的预览颜色。")]
        public Color 白模颜色 = new Color(0.7f, 0.7f, 0.65f, 1f);

        [Tooltip("放在地块顶面中心的视觉预制体，例如 StraightRiver。")]
        public GameObject 主要内容预制体;
    }
}

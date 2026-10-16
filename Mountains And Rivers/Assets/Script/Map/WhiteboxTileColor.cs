using UnityEngine;

namespace ShanheWeijin.Map
{
    /// <summary>场景重开后重新应用白模颜色；MaterialPropertyBlock 本身不会被场景序列化。</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class WhiteboxTileColor : MonoBehaviour
    {
        [SerializeField, Tooltip("该地块底座的白模预览颜色。")]
        private Color 白模颜色 = Color.white;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public void 设置颜色(Color 颜色)
        {
            白模颜色 = 颜色;
            应用颜色();
        }

        private void OnEnable() => 应用颜色();

        private void OnValidate() => 应用颜色();

        private void 应用颜色()
        {
            MeshRenderer 渲染器 = GetComponent<MeshRenderer>();
            if (渲染器 == null)
            {
                return;
            }

            MaterialPropertyBlock 属性块 = new MaterialPropertyBlock();
            渲染器.GetPropertyBlock(属性块);
            属性块.SetColor(BaseColorId, 白模颜色);
            渲染器.SetPropertyBlock(属性块);
        }
    }
}

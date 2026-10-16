using UnityEngine;

/// <summary>
/// 统一控制一格河流的流向与洪水表现。
/// 使用 MaterialPropertyBlock，避免在运行时为每个地块复制材质。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class RiverVisualController : MonoBehaviour
{
    [Header("渲染对象")]
    [SerializeField]
    [Tooltip("使用“河水与洪水”材质的 Renderer。")]
    private Renderer 河水渲染器;

    [Header("水流")]
    [SerializeField]
    [Tooltip("贴图 UV 空间中的流向。直线河道默认从上往下为 (0, -1)。")]
    private Vector2 流动方向 = new Vector2(0f, -1f);

    [SerializeField, Range(0f, 0.5f)]
    [Tooltip("只改变波纹移动速度，不影响游戏逻辑中的实际流速。")]
    private float 流动速度 = 0.06f;

    [Header("洪水")]
    [SerializeField, Range(0f, 1f)]
    [Tooltip("0 为平常河道，1 为控制图蓝通道允许的最大淹没范围。")]
    private float 洪水扩张进度;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("洪水越浑浊，颜色越偏向材质中的洪水颜色。")]
    private float 浑浊程度 = 0.35f;

    private static readonly int FlowDirectionId = Shader.PropertyToID("_FlowDirection");
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    private static readonly int FloodProgressId = Shader.PropertyToID("_FloodProgress");
    private static readonly int TurbidityId = Shader.PropertyToID("_Turbidity");

    private MaterialPropertyBlock 属性块;

    public float 洪水进度
    {
        get => 洪水扩张进度;
        set
        {
            洪水扩张进度 = Mathf.Clamp01(value);
            应用参数();
        }
    }

    private void OnEnable()
    {
        应用参数();
    }

    private void OnValidate()
    {
        应用参数();
    }

    /// <summary>
    /// 当河道旋转或水流方向发生变化时，可由地块系统调用。
    /// </summary>
    public void 设置流向(Vector2 新流向)
    {
        流动方向 = 新流向.sqrMagnitude > 0.0001f ? 新流向.normalized : Vector2.down;
        应用参数();
    }

    private void 应用参数()
    {
        if (河水渲染器 == null)
        {
            return;
        }

        属性块 ??= new MaterialPropertyBlock();
        河水渲染器.GetPropertyBlock(属性块);

        Vector2 安全流向 = 流动方向.sqrMagnitude > 0.0001f ? 流动方向.normalized : Vector2.down;
        属性块.SetVector(FlowDirectionId, new Vector4(安全流向.x, 安全流向.y, 0f, 0f));
        属性块.SetFloat(FlowSpeedId, 流动速度);
        属性块.SetFloat(FloodProgressId, 洪水扩张进度);
        属性块.SetFloat(TurbidityId, 浑浊程度);

        河水渲染器.SetPropertyBlock(属性块);
    }
}

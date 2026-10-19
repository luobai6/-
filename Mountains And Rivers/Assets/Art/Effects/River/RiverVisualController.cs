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
    [Tooltip("控制洪水沿河道从源头向出口推进。0 为尚未到达，1 为到达整格出口。")]
    private float 洪水到达进度 = 1f;

    [SerializeField, Range(0.001f, 0.2f)]
    [Tooltip("控制推进水头边缘的过渡宽度。数值越大，水头边缘越柔和。")]
    private float 水头边缘柔和度 = 0.04f;

    [SerializeField]
    [Tooltip("开启后改用材质中的“反向水头推进遮罩”，使洪水从原出口向原入口推进。")]
    private bool 反向推进;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("0 为平常河道，1 为控制图蓝通道允许的最大淹没范围。")]
    private float 洪水扩张进度;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("洪水越浑浊，颜色越偏向材质中的洪水颜色。")]
    private float 浑浊程度 = 0.35f;

    [Header("洪水浪头")]
    [SerializeField]
    [Tooltip("启用后，河面会在洪水推进边界处鼓起一条低矮浪脊。")]
    private bool 启用鼓起浪头 = true;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("浪脊的局部高度。当河面 Plane 缩放为 0.1 时，0.35 约等于 0.035 个世界单位。")]
    private float 浪头高度 = 0.35f;

    [SerializeField, Range(0.005f, 0.2f)]
    [Tooltip("浪头沿推进方向占用的宽度。建议从 0.08 开始调整。")]
    private float 浪头宽度 = 0.08f;

    [SerializeField, Range(0f, 2f)]
    [Tooltip("控制浪脊顶部白色水沫的明显程度。")]
    private float 浪头水沫强度 = 1f;

    [SerializeField, Range(0f, 2f)]
    [Tooltip("转弯河道可调高此值，表现水流挤压外侧河岸；直道保持 0。")]
    private float 弯道外岸冲击强度;

    private static readonly int FlowDirectionId = Shader.PropertyToID("_FlowDirection");
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    private static readonly int FloodArrivalProgressId = Shader.PropertyToID("_FloodArrivalProgress");
    private static readonly int ArrivalSoftnessId = Shader.PropertyToID("_ArrivalSoftness");
    private static readonly int ReverseAdvanceId = Shader.PropertyToID("_ReverseAdvance");
    private static readonly int FloodProgressId = Shader.PropertyToID("_FloodProgress");
    private static readonly int TurbidityId = Shader.PropertyToID("_Turbidity");
    private static readonly int FloodWaveEnabledId = Shader.PropertyToID("_FloodWaveEnabled");
    private static readonly int FloodWaveHeightId = Shader.PropertyToID("_FloodWaveHeight");
    private static readonly int FloodWaveWidthId = Shader.PropertyToID("_FloodWaveWidth");
    private static readonly int FloodWaveFoamStrengthId = Shader.PropertyToID("_FloodWaveFoamStrength");
    private static readonly int OuterBankImpactStrengthId = Shader.PropertyToID("_OuterBankImpactStrength");

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

    public float 水头进度
    {
        get => 洪水到达进度;
        set
        {
            洪水到达进度 = Mathf.Clamp01(value);
            应用参数();
        }
    }

    /// <summary>
    /// 设置洪水是否改用独立的反向推进遮罩。
    /// </summary>
    public void 设置反向推进(bool 是否反向)
    {
        反向推进 = 是否反向;
        应用参数();
    }

    private void OnEnable()
    {
        FloodWaveGridMeshProvider.确保使用高细分网格(河水渲染器);
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
        属性块.SetFloat(FloodArrivalProgressId, 洪水到达进度);
        属性块.SetFloat(ArrivalSoftnessId, 水头边缘柔和度);
        属性块.SetFloat(ReverseAdvanceId, 反向推进 ? 1f : 0f);
        属性块.SetFloat(FloodProgressId, 洪水扩张进度);
        属性块.SetFloat(TurbidityId, 浑浊程度);
        属性块.SetFloat(FloodWaveEnabledId, 启用鼓起浪头 ? 1f : 0f);
        属性块.SetFloat(FloodWaveHeightId, 浪头高度);
        属性块.SetFloat(FloodWaveWidthId, 浪头宽度);
        属性块.SetFloat(FloodWaveFoamStrengthId, 浪头水沫强度);
        属性块.SetFloat(OuterBankImpactStrengthId, 弯道外岸冲击强度);

        河水渲染器.SetPropertyBlock(属性块);
    }
}

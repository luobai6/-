using UnityEngine;

public enum 三叉来水端口
{
    北侧 = 0,
    东侧 = 1,
    南侧 = 2
}

/// <summary>
/// 控制三叉河道任一端口来水，以及其余两个出口的独立洪水推进。
/// 使用 MaterialPropertyBlock，避免运行时复制材质。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ForkRiverVisualController : MonoBehaviour
{
    [Header("渲染对象")]
    [SerializeField]
    [Tooltip("使用“分叉河水与洪水”材质的 Renderer。")]
    private Renderer 河水渲染器;

    [Header("来水方向")]
    [SerializeField]
    [Tooltip("选择洪水从北、东或南侧进入。对应方向的支路推进值会被忽略。")]
    private 三叉来水端口 来水端口 = 三叉来水端口.北侧;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("洪水从当前来水端口推进至分叉中心。")]
    private float 入流推进进度;

    [Header("出口推进")]
    [SerializeField, Range(0f, 1f)]
    [Tooltip("洪水从中心推进至北侧。北侧为来水端口时，此值会被忽略。")]
    private float 北支路推进进度;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("洪水从中心推进至东侧。东侧为来水端口时，此值会被忽略。")]
    private float 东支路推进进度;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("洪水从中心推进至南侧。南侧为来水端口时，此值会被忽略。")]
    private float 南支路推进进度;

    [SerializeField, Range(0.001f, 0.2f)]
    [Tooltip("控制所有水头边缘的过渡宽度。数值越大，边缘越柔和。")]
    private float 水头边缘柔和度 = 0.04f;

    [Header("洪水外观")]
    [SerializeField, Range(0f, 1f)]
    [Tooltip("0 为平常河道，1 为控制图蓝通道允许的最大漫溢范围。")]
    private float 洪水扩张进度;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("洪水越浑浊，颜色越偏向材质中的洪水颜色。")]
    private float 浑浊程度 = 0.35f;

    private static readonly int IncomingPortId = Shader.PropertyToID("_IncomingPort");
    private static readonly int EntryArrivalProgressId = Shader.PropertyToID("_EntryArrivalProgress");
    private static readonly int NorthExitProgressId = Shader.PropertyToID("_NorthExitProgress");
    private static readonly int EastExitProgressId = Shader.PropertyToID("_EastExitProgress");
    private static readonly int SouthExitProgressId = Shader.PropertyToID("_SouthExitProgress");
    private static readonly int ArrivalSoftnessId = Shader.PropertyToID("_ArrivalSoftness");
    private static readonly int FloodProgressId = Shader.PropertyToID("_FloodProgress");
    private static readonly int TurbidityId = Shader.PropertyToID("_Turbidity");

    private MaterialPropertyBlock 属性块;

    public void 设置来水端口(三叉来水端口 新来水端口)
    {
        来水端口 = 新来水端口;
        应用参数();
    }

    /// <summary>
    /// 一次设置入流和三个方向的出口推进进度；当前来水方向的出口值会被忽略。
    /// </summary>
    public void 设置分段进度(float 入流, float 北支路, float 东支路, float 南支路)
    {
        入流推进进度 = Mathf.Clamp01(入流);
        北支路推进进度 = Mathf.Clamp01(北支路);
        东支路推进进度 = Mathf.Clamp01(东支路);
        南支路推进进度 = Mathf.Clamp01(南支路);
        应用参数();
    }

    public void 设置洪水扩张进度(float 进度)
    {
        洪水扩张进度 = Mathf.Clamp01(进度);
        应用参数();
    }

    public void 重置洪水表现()
    {
        入流推进进度 = 0f;
        北支路推进进度 = 0f;
        东支路推进进度 = 0f;
        南支路推进进度 = 0f;
        洪水扩张进度 = 0f;
        应用参数();
    }

    private void OnEnable()
    {
        应用参数();
    }

    private void OnValidate()
    {
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
        属性块.SetFloat(IncomingPortId, (float)来水端口);
        属性块.SetFloat(EntryArrivalProgressId, 入流推进进度);
        属性块.SetFloat(NorthExitProgressId, 北支路推进进度);
        属性块.SetFloat(EastExitProgressId, 东支路推进进度);
        属性块.SetFloat(SouthExitProgressId, 南支路推进进度);
        属性块.SetFloat(ArrivalSoftnessId, 水头边缘柔和度);
        属性块.SetFloat(FloodProgressId, 洪水扩张进度);
        属性块.SetFloat(TurbidityId, 浑浊程度);
        河水渲染器.SetPropertyBlock(属性块);
    }
}

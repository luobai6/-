using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 控制河流源头瀑布的逐帧动画。
/// 当前包含“常态循环”和“洪水涌出并停在末帧”两个阶段；洪水持续循环资源完成后可直接补入第三组帧。
/// </summary>
[DisallowMultipleComponent]
public sealed class RiverSourceWaterfallController : MonoBehaviour
{
    private enum 播放阶段
    {
        常态循环,
        洪水涌出,
        洪水末帧
    }

    [Header("渲染对象")]
    [SerializeField]
    [Tooltip("显示瀑布序列帧的 SpriteRenderer。")]
    private SpriteRenderer 瀑布渲染器;

    [Header("常态瀑布")]
    [SerializeField]
    [Tooltip("常态瀑布的循环帧，按播放顺序排列。")]
    private Sprite[] 常态瀑布帧;

    [SerializeField, Min(1f)]
    [Tooltip("常态瀑布每秒播放多少帧。建议 8 帧。")]
    private float 常态帧率 = 8f;

    [Header("洪水涌出")]
    [SerializeField]
    [Tooltip("从常态瀑布过渡到洪水瀑布的帧，按播放顺序排列。")]
    private Sprite[] 洪水涌出帧;

    [SerializeField, Min(1f)]
    [Tooltip("洪水涌出每秒播放多少帧。16 张帧以 12 帧/秒播放时约持续 1.33 秒。")]
    private float 洪水涌出帧率 = 12f;

    [SerializeField]
    [Tooltip("洪水涌出播放完成时触发，后续可在这里衔接洪水持续循环或结算表现。")]
    private UnityEvent 洪水涌出播放完成;

    private 播放阶段 当前阶段 = 播放阶段.常态循环;
    private float 播放时间;
    private int 当前帧序号 = -1;

    /// <summary>当前是否已经进入洪水状态。</summary>
    public bool 已进入洪水 => 当前阶段 != 播放阶段.常态循环;

    private void Awake()
    {
        显示帧(常态瀑布帧, 0);
    }

    private void OnEnable()
    {
        if (当前帧序号 < 0)
        {
            恢复常态();
        }
    }

    private void Update()
    {
        if (瀑布渲染器 == null)
        {
            return;
        }

        播放时间 += Time.deltaTime;
        switch (当前阶段)
        {
            case 播放阶段.常态循环:
                播放循环(常态瀑布帧, 常态帧率);
                break;
            case 播放阶段.洪水涌出:
                播放一次洪水涌出();
                break;
        }
    }

    /// <summary>由洪水结算流程在洪水从源头出现时调用。</summary>
    [ContextMenu("测试/播放洪水涌出")]
    public void 播放洪水涌出()
    {
        if (洪水涌出帧 == null || 洪水涌出帧.Length == 0)
        {
            Debug.LogWarning("源头瀑布缺少洪水涌出帧。", this);
            return;
        }

        当前阶段 = 播放阶段.洪水涌出;
        播放时间 = 0f;
        当前帧序号 = -1;
        显示帧(洪水涌出帧, 0);
    }

    /// <summary>重置关卡或退出洪水预览时调用。</summary>
    [ContextMenu("测试/恢复常态瀑布")]
    public void 恢复常态()
    {
        当前阶段 = 播放阶段.常态循环;
        播放时间 = 0f;
        当前帧序号 = -1;
        显示帧(常态瀑布帧, 0);
    }

    private void 播放循环(Sprite[] 帧组, float 帧率)
    {
        if (帧组 == null || 帧组.Length == 0)
        {
            return;
        }

        int 序号 = Mathf.FloorToInt(播放时间 * 帧率) % 帧组.Length;
        显示帧(帧组, 序号);
    }

    private void 播放一次洪水涌出()
    {
        int 帧数 = 洪水涌出帧.Length;
        int 序号 = Mathf.FloorToInt(播放时间 * 洪水涌出帧率);
        if (序号 < 帧数)
        {
            显示帧(洪水涌出帧, 序号);
            return;
        }

        显示帧(洪水涌出帧, 帧数 - 1);
        当前阶段 = 播放阶段.洪水末帧;
        洪水涌出播放完成?.Invoke();
    }

    private void 显示帧(Sprite[] 帧组, int 序号)
    {
        if (瀑布渲染器 == null || 帧组 == null || 帧组.Length == 0)
        {
            return;
        }

        int 安全序号 = Mathf.Clamp(序号, 0, 帧组.Length - 1);
        if (安全序号 == 当前帧序号 && 瀑布渲染器.sprite == 帧组[安全序号])
        {
            return;
        }

        当前帧序号 = 安全序号;
        瀑布渲染器.sprite = 帧组[安全序号];
    }
}

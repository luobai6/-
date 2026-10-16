using UnityEngine;

namespace ShanheWeijin.Map
{
    /// <summary>固定观察角度的地图相机；只改变 XZ 位置和正交尺寸。</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class OrthographicMapCameraController : MonoBehaviour
    {
        [Header("键盘移动")]
        [SerializeField, Min(0f), Tooltip("WASD 或方向键沿地图 XZ 平面移动的速度。")]
        private float 键盘移动速度 = 5f;

        [Header("滚轮缩放")]
        [SerializeField, Min(0f), Tooltip("鼠标滚轮每滚动一格改变的正交尺寸。")]
        private float 滚轮缩放速度 = 2f;

        [SerializeField, Min(0.01f), Tooltip("允许的最小正交尺寸；值越小，画面越近。")]
        private float 最小缩放 = 2f;

        [SerializeField, Min(0.01f), Tooltip("允许的最大正交尺寸；值越大，画面越远。")]
        private float 最大缩放 = 15f;

        [Header("地图边界")]
        [SerializeField, Tooltip("开启后将相机中心的 X、Z 位置限制在下方范围内。")]
        private bool 限制移动范围;

        [SerializeField, Tooltip("允许的相机中心最小 X、Z 坐标。")]
        private Vector2 移动范围最小值 = new Vector2(-1f, -1f);

        [SerializeField, Tooltip("允许的相机中心最大 X、Z 坐标。")]
        private Vector2 移动范围最大值 = new Vector2(4f, 4f);

        private Camera 地图相机;
        private bool 已提示相机错误;
        private bool 正在拖动;
        private Vector3 上一帧鼠标位置;

        public float 限制缩放(float 目标尺寸)
        {
            float 最小值 = Mathf.Max(0.01f, 最小缩放);
            float 最大值 = Mathf.Max(最小值, 最大缩放);
            return Mathf.Clamp(目标尺寸, 最小值, 最大值);
        }

        private void Awake()
        {
            地图相机 = GetComponent<Camera>();
        }

        private void OnValidate()
        {
            最小缩放 = Mathf.Max(0.01f, 最小缩放);
            最大缩放 = Mathf.Max(最小缩放, 最大缩放);
        }

        private void Update()
        {
            if (地图相机 == null)
            {
                地图相机 = GetComponent<Camera>();
            }

            if (地图相机 == null || !地图相机.orthographic)
            {
                if (!已提示相机错误)
                {
                    Debug.LogWarning("地图相机控制器需要正交 Camera；相机投影设置未被修改。", this);
                    已提示相机错误 = true;
                }
                return;
            }

            已提示相机错误 = false;
            float 水平输入 = Input.GetAxisRaw("Horizontal");
            float 纵向输入 = Input.GetAxisRaw("Vertical");
            Vector3 键盘方向 = new Vector3(水平输入, 0f, 纵向输入);
            if (键盘方向.sqrMagnitude > 1f)
            {
                键盘方向.Normalize();
            }
            平移(键盘方向 * (键盘移动速度 * Time.unscaledDeltaTime));

            if (Input.GetMouseButtonDown(2))
            {
                正在拖动 = true;
                上一帧鼠标位置 = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(2))
            {
                正在拖动 = false;
            }

            if (正在拖动 && Input.GetMouseButton(2))
            {
                if (尝试取得地面交点(上一帧鼠标位置, out Vector3 上一帧交点) &&
                    尝试取得地面交点(Input.mousePosition, out Vector3 当前交点))
                {
                    平移(上一帧交点 - 当前交点);
                }
                上一帧鼠标位置 = Input.mousePosition;
            }

            float 滚轮输入 = Input.mouseScrollDelta.y;
            if (Mathf.Abs(滚轮输入) > 0.001f)
            {
                地图相机.orthographicSize = 限制缩放(
                    地图相机.orthographicSize - 滚轮输入 * 滚轮缩放速度);
            }
        }

        private bool 尝试取得地面交点(Vector3 屏幕位置, out Vector3 交点)
        {
            Plane 地图平面 = new Plane(Vector3.up, Vector3.zero);
            Ray 射线 = 地图相机.ScreenPointToRay(屏幕位置);
            if (地图平面.Raycast(射线, out float 距离))
            {
                交点 = 射线.GetPoint(距离);
                return true;
            }

            交点 = default;
            return false;
        }

        private void 平移(Vector3 位移)
        {
            if (位移.sqrMagnitude <= 0f)
            {
                return;
            }

            Vector3 目标位置 = transform.position + new Vector3(位移.x, 0f, 位移.z);
            if (限制移动范围)
            {
                目标位置.x = Mathf.Clamp(目标位置.x,
                    Mathf.Min(移动范围最小值.x, 移动范围最大值.x),
                    Mathf.Max(移动范围最小值.x, 移动范围最大值.x));
                目标位置.z = Mathf.Clamp(目标位置.z,
                    Mathf.Min(移动范围最小值.y, 移动范围最大值.y),
                    Mathf.Max(移动范围最小值.y, 移动范围最大值.y));
            }
            transform.position = 目标位置;
        }
    }
}

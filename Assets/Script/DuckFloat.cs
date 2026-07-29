using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 挂在漂浮物体 GameObject 上。
/// 功能：锁定水面高度 + 上下浮动 + 轻微摇晃 + 自动漂移 + 定时产生涟漪。
/// 依赖：场景中存在 WaterSurface，且 WaterSurface 已暴露 WaterBounds 属性。
/// </summary>
public class DuckFloat : MonoBehaviour
{
    [Header("water reference")]
    [SerializeField] private WaterSurface waterSurface;

    [Header("floating settings")]
    [Header("Model Orientation (根据鸭子模型朝向调整)")]
    [Tooltip("若物体进场后朝向错误，调整此值。默认0。常见值：180（模型背面朝前）")]
    [SerializeField] private float modelYawOffset = 0f;
    [Tooltip("漂浮物体底部距离水面的高度")]
    [SerializeField] private float aboveWaterHeight = 0.5f; // 漂浮物体底部距离水面的高度
    [Tooltip("上下浮动幅度")]
    [SerializeField] private float bobAmplitude = 2f; // 上下浮动幅度
    [Tooltip("上下浮动频率")]
    [SerializeField] private float bobFrequency = 1f; // 上下浮动频率

    [Header("Tilt settings")]
    [Tooltip("倾斜幅度")]
    [SerializeField] private float tiltAmplitude = 5f; // 倾斜幅度
    [Tooltip("倾斜频率")]
    [SerializeField] private float tiltFrequency = 0.7f; // 倾斜频率

    [Header("Drift settings")]
    [Tooltip("漂移速度")]
    [SerializeField] private float driftSpeed = 20f; // 漂移速度
    [Tooltip("换方向间隔（秒）")]
    [SerializeField] private float directionChangeInterval = 4f;
    [Tooltip("距离池边多近时强制转向（米）")]
    [SerializeField] private float edgeMargin = 15f;
    [Tooltip("转向时的平滑速度")]
    [SerializeField] private float turnSpeed = 2f;

    [Header("Ripple Settings")]
    [Tooltip("漂移时产生涟漪的时间间隔（秒）")]
    [SerializeField] private float rippleInterval = 0.5f;
    [SerializeField] private float rippleRadius = 0.025f;
    [SerializeField] private float rippleStrength = 0.1f;
    [SerializeField] private float foamRadius = 0.04f;
    [SerializeField] private float foamStrength = 0.35f;

    //内部状态
    private float phaseOffset;// 用于让不同物体的 bob 和 tilt 不完全同步
    private float baseRotX;  
    private float rippleTimer = 0f;
    private float directionChangeTimer = 0f;
    private Vector3 moveDir;
    private float currentYaw;//当前朝向角 （Y轴旋转）

    private void Start()
    {
        
        baseRotX  = transform.eulerAngles.x;
        phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        currentYaw = transform.eulerAngles.y;
        PickNewDirection();
    }

    private void Update()
    {
        if (waterSurface == null) return;

        UpdateDrift();
        UpdateHeight();
        UpdateRotation();
        UpdateRipple();
    }

    //漂移（水平移动）
    private void UpdateDrift()
    {
        directionChangeTimer += Time.deltaTime;
        
        //定时换方向
        if (directionChangeTimer >= directionChangeInterval)
        {
            PickNewDirection();
            directionChangeTimer = 0f;
        }

        //靠近边缘时强制转向
        
        //Bounds bounds = waterSurface.WaterBounds;
        // 用 WaterSurface 的 Transform 推算池边界（Unity Plane mesh 原始大小 10×10）
        Transform wt = waterSurface.transform;
        float halfX = wt.localScale.x * 5f;
        float halfZ = wt.localScale.z * 5f;
        Vector3 center = wt.position;
        Vector3 pos = transform.position;
        


        bool nearEdge = false;
        if (pos.x < center.x - halfX + edgeMargin) { moveDir.x =  Mathf.Abs(moveDir.x); nearEdge = true; }
        if (pos.x > center.x + halfX - edgeMargin) { moveDir.x = -Mathf.Abs(moveDir.x); nearEdge = true; }
        if (pos.z < center.z - halfZ + edgeMargin) { moveDir.z =  Mathf.Abs(moveDir.z); nearEdge = true; }
        if (pos.z > center.z + halfZ - edgeMargin) { moveDir.z = -Mathf.Abs(moveDir.z); nearEdge = true; }

        if (nearEdge)
        {
            moveDir.y = 0f;
            moveDir.Normalize();
            directionChangeTimer = 0f;
        }

        transform.position += moveDir * driftSpeed * Time.deltaTime;

        
    }

    //高度（锁水面＋浮动）
    private void UpdateHeight()
    {
        float waterY = waterSurface.WaterY;
        float bob = Mathf.Sin(Time.time * bobFrequency + phaseOffset) * bobAmplitude;

        Vector3 pos = transform.position;
        pos.y = waterY + aboveWaterHeight + bob;
        transform.position = pos;
    }

    //旋转（轻微摇晃）
    private void UpdateRotation()
    {
        // Y 轴：平滑转向漂移方向
        if (moveDir.sqrMagnitude > 0.001f)
        {
            float targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg + modelYawOffset;
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * turnSpeed);
        }

        // Z 轴：左右轻微摇晃（模拟水面起伏）
        float tiltZ = Mathf.Sin(Time.time * tiltFrequency + phaseOffset) * tiltAmplitude;

        // X 轴：始终等于初始值（如 -90），绝不修改
        transform.rotation = Quaternion.Euler(baseRotX, currentYaw, tiltZ);
    }

    // ── 涟漪注入 ──────────────────────────────────
    private void UpdateRipple()
    {
        rippleTimer += Time.deltaTime;
        if (rippleTimer >= rippleInterval)
        {
            waterSurface.InjectRippleAndFoamWorld(
                transform.position,
                rippleRadius, rippleStrength,
                foamRadius, foamStrength);
            rippleTimer = 0f;
        }
    }

    // ── 随机选取漂移方向 ──────────────────────────
    private void PickNewDirection()
    {
        // 在当前方向基础上偏转 ±90°，不会突然掉头
        float angle = Random.Range(-90f, 90f);
        moveDir = Quaternion.Euler(0, angle, 0) * (moveDir == Vector3.zero
            ? Vector3.forward
            : moveDir);
        moveDir.y = 0f;
        moveDir.Normalize();
        directionChangeTimer = 0f;
    }

    // ── 编辑器可视化（Scene视图显示边界） ─────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (waterSurface == null) return;
        Transform wt = waterSurface.transform;
        float halfX = wt.localScale.x * 5f - edgeMargin;
        float halfZ = wt.localScale.z * 5f - edgeMargin;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            new Vector3(wt.position.x, transform.position.y, wt.position.z),
            new Vector3(halfX * 2, 0.1f, halfZ * 2));
    }
#endif



}

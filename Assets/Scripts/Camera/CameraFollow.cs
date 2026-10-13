using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("跟随目标")]
    public Transform target;

    [Header("构图")]
    public Vector3 offset = new Vector3(8f, 5f, -10f);
    public float lookAhead = 8f;
    public float lookHeight = 1f;

    [Header("平滑")]
    public float positionSmoothTime = 0.18f;
    public float rotationSmoothSpeed = 8f;

    private Vector3 currentVelocity;

    public void SnapToTarget()
    {
        if (target == null) return;
        currentVelocity = Vector3.zero;
        transform.position = target.position + offset;
        Vector3 lookTarget = target.position + Vector3.forward * lookAhead + Vector3.up * lookHeight;
        Vector3 direction = lookTarget - transform.position;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        // 直线路径使用固定世界坐标偏移。
        Vector3 desiredPosition =
            target.position + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            positionSmoothTime
        );

        // 不盯着玩家中心，而是看向玩家前方。
        Vector3 lookTarget =
            target.position +
            Vector3.forward * lookAhead +
            Vector3.up * lookHeight;

        Vector3 lookDirection = lookTarget - transform.position;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation =
                Quaternion.LookRotation(lookDirection, Vector3.up);

            float rotationBlend =
                1f - Mathf.Exp(
                    -rotationSmoothSpeed * Time.deltaTime
                );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                rotationBlend
            );
        }
    }
}

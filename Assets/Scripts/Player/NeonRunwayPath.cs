using UnityEngine;

// Matches the world-space centerline used by PulseSceneBuilder.
// This helper is specific to the original, unmoved NeonRunway scene.
public static class NeonRunwayPath
{
    public const float StartDistance = -12f;
    public const float EndDistance = 150f;
    private const float BendStart = 25f;
    private const float Radius = 75f;

    public static Vector3 GetPoint(float distance)
    {
        if (distance <= BendStart)
            return new Vector3(0f, 0f, distance);

        float angle = (distance - BendStart) / Radius;
        return new Vector3(
            Radius * (1f - Mathf.Cos(angle)),
            0f,
            BendStart + Radius * Mathf.Sin(angle)
        );
    }

    public static Vector3 GetForward(float distance)
    {
        float angle = Mathf.Max(0f, distance - BendStart) / Radius;
        return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
    }

    // Project the actual physics position onto the straight or circular part.
    // Progress therefore does not keep advancing when a wall blocks the player.
    public static float FindDistance(Vector3 position)
    {
        float straight = Mathf.Clamp(position.z, StartDistance, BendStart);
        float angle = Mathf.Atan2(position.z - BendStart, Radius - position.x);
        float curved = BendStart + Radius * Mathf.Clamp(
            angle, 0f, (EndDistance - BendStart) / Radius);

        position.y = 0f;
        return (position - GetPoint(straight)).sqrMagnitude <=
               (position - GetPoint(curved)).sqrMagnitude
            ? straight : curved;
    }
}

using UnityEngine;
using toio;

public class collision : MonoBehaviour
{
    [Header("SAT判定")]
    public float cubeSize = 20f;
    public float hitMargin = 2f;
    public bool isHit;

    public bool CheckCollision(Cube cube_player, Cube cube_Oni)
    {
        if(cube_player == null) {
            isHit = false;
            return false;
        }
        if(cube_Oni == null) {
            isHit = false;
            return false;
        }
        if(!cube_player.isGrounded || !cube_Oni.isGrounded) {
            isHit = false;
            return false;
        }

        Vector2[] playerCorners = GetCubeCorners(cube_player);
        Vector2[] oniCorners = GetCubeCorners(cube_Oni);

        isHit = IsHitSAT(playerCorners, oniCorners);
        return isHit;
    }

    /// <summary>
    /// shooterの前方に伸ばしたレーザーと、targetの回転した正方形が交わるか判定する。
    /// </summary>
    public bool CheckLaserCollision(Cube shooter, Cube target, float range, float laserHalfWidth = 0f)
    {
        if (shooter == null || target == null) return false;
        if (!shooter.isGrounded || !target.isGrounded) return false;
        if (range <= 0f) return false;

        float shooterRad = shooter.angle * Mathf.Deg2Rad;
        Vector2 laserDirection = new Vector2(Mathf.Cos(shooterRad), Mathf.Sin(shooterRad));

        // プレイヤーCubeの中心ではなく、前面からレーザーを出す。
        float shooterHalfSize = cubeSize * 0.5f + hitMargin;
        Vector2 laserOrigin = shooter.pos + laserDirection * shooterHalfSize;

        // 敵Cubeの向きに合わせたローカル座標系を作る。
        float targetRad = target.angle * Mathf.Deg2Rad;
        Vector2 targetXAxis = new Vector2(Mathf.Cos(targetRad), Mathf.Sin(targetRad));
        Vector2 targetYAxis = new Vector2(-Mathf.Sin(targetRad), Mathf.Cos(targetRad));
        Vector2 originFromTarget = laserOrigin - target.pos;

        Vector2 localOrigin = new Vector2(
            Vector2.Dot(originFromTarget, targetXAxis),
            Vector2.Dot(originFromTarget, targetYAxis)
        );
        Vector2 localDirection = new Vector2(
            Vector2.Dot(laserDirection, targetXAxis),
            Vector2.Dot(laserDirection, targetYAxis)
        );

        float targetHalfSize = cubeSize * 0.5f + hitMargin + Mathf.Max(0f, laserHalfWidth);
        float enterDistance = 0f;
        float exitDistance = range;

        if (!IntersectsSlab(localOrigin.x, localDirection.x, targetHalfSize,
                ref enterDistance, ref exitDistance)) return false;
        if (!IntersectsSlab(localOrigin.y, localDirection.y, targetHalfSize,
                ref enterDistance, ref exitDistance)) return false;

        return enterDistance <= exitDistance;
    }

    bool IntersectsSlab(float origin, float direction, float halfSize,
        ref float enterDistance, ref float exitDistance)
    {
        if (Mathf.Abs(direction) < 0.0001f)
        {
            return origin >= -halfSize && origin <= halfSize;
        }

        float distance1 = (-halfSize - origin) / direction;
        float distance2 = (halfSize - origin) / direction;
        if (distance1 > distance2)
        {
            float temporary = distance1;
            distance1 = distance2;
            distance2 = temporary;
        }

        enterDistance = Mathf.Max(enterDistance, distance1);
        exitDistance = Mathf.Min(exitDistance, distance2);
        return enterDistance <= exitDistance;
    }

    Vector2[] GetCubeCorners(Cube cube)
    {
        float half = cubeSize * 0.5f + hitMargin;
        float rad = cube.angle * Mathf.Deg2Rad;

        Vector2 center = cube.pos;
        Vector2 xAxis = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        Vector2 yAxis = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));

        return new Vector2[]
        {
            center + xAxis * half + yAxis * half,
            center - xAxis * half + yAxis * half,
            center - xAxis * half - yAxis * half,
            center + xAxis * half - yAxis * half,
        };
    }

    bool IsHitSAT(Vector2[] playerCorners, Vector2[] oniCorners)
    {
        if(HasSeparatingAxis(playerCorners, oniCorners)) return false;
        if(HasSeparatingAxis(oniCorners, playerCorners)) return false;

        return true;
    }

    bool HasSeparatingAxis(Vector2[] cornersA, Vector2[] cornersB)
    {
        for(int i = 0; i < cornersA.Length; i++)
        {
            Vector2 p1 = cornersA[i];
            Vector2 p2 = cornersA[(i + 1) % cornersA.Length];
            Vector2 edge = p2 - p1;
            Vector2 axis = new Vector2(-edge.y, edge.x).normalized;

            Project(cornersA, axis, out float minA, out float maxA);
            Project(cornersB, axis, out float minB, out float maxB);

            if(maxA < minB || maxB < minA)
            {
                return true;
            }
        }

        return false;
    }

    void Project(Vector2[] corners, Vector2 axis, out float min, out float max)
    {
        min = Vector2.Dot(corners[0], axis);
        max = min;

        for(int i = 1; i < corners.Length; i++)
        {
            float value = Vector2.Dot(corners[i], axis);
            min = Mathf.Min(min, value);
            max = Mathf.Max(max, value);
        }
    }
}

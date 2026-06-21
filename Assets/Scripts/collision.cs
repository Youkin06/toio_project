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

using UnityEngine;
using toio;

public class OniController : MonoBehaviour
{
    [Header("追跡設定")]
    public int speed = 45;
    public int turnSpeed = 30;
    public float stopDistance = 30f;
    public float angleTolerance = 15f;

    int lastMoveLeft = int.MinValue;
    int lastMoveRight = int.MinValue;

    void Update()
    {
        ChasePlayer();
    }

    void ChasePlayer()
    {
        if(oniGokko_GameManager.instance == null) return;

        Cube cube_player = oniGokko_GameManager.instance.cube_player;
        Cube cube_Oni = oniGokko_GameManager.instance.cube_Oni;

        if(cube_player == null) return;
        if(cube_Oni == null) return;
        if(!cube_player.isGrounded || !cube_Oni.isGrounded) return;

        Vector2 diff = cube_player.pos - cube_Oni.pos;
        float distance = diff.magnitude;

        if(distance < stopDistance)
        {
            MoveOni(0, 0);
            return;
        }

        float targetAngle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        float angleDiff = Mathf.DeltaAngle(cube_Oni.angle, targetAngle);

        int left = speed;
        int right = speed;

        if(Mathf.Abs(angleDiff) > angleTolerance)
        {
            if(angleDiff > 0)
            {
                left += turnSpeed;
                right -= turnSpeed;
            }
            else
            {
                left -= turnSpeed;
                right += turnSpeed;
            }
        }

        left = Mathf.Clamp(left, -100, 100);
        right = Mathf.Clamp(right, -100, 100);

        MoveOni(left, right);
    }

    void MoveOni(int left, int right)
    {
        if(oniGokko_GameManager.instance == null) return;

        Cube cube_Oni = oniGokko_GameManager.instance.cube_Oni;
        if(cube_Oni == null) return;
        if(left == lastMoveLeft && right == lastMoveRight) return;

        cube_Oni.Move(left, right, 0, Cube.ORDER_TYPE.Strong);
        lastMoveLeft = left;
        lastMoveRight = right;
    }
}

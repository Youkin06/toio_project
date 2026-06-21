using UnityEngine;
namespace toio.Samples.Sample_ConnectName
{
    public class player_move_manager : MonoBehaviour
    {
        public static player_move_manager instance;

        [Header("移動変数")]
        int speed = 60;
        int turnSpeed = 10;
        int left = 0;
        int right = 0;
        int lastMoveLeft = int.MinValue;
        int lastMoveRight = int.MinValue;

        [Header("押した判定")]
        public bool forwardButtonPressed;
        public bool backButtonPressed;
        public bool leftButtonPressed;
        public bool rightButtonPressed;

        void Awake()
        {
            instance = this;
        }

        void Update()
        {   
            control_PC();
            MoveCube();
        }

        void MoveCube()
        {
            if(oniGokko_GameManager.instance == null) return;

            Cube cube_player = oniGokko_GameManager.instance.cube_player;
            
            if(cube_player == null) return;
            if (left == lastMoveLeft && right == lastMoveRight) return;

            cube_player.Move(left, right, 0, Cube.ORDER_TYPE.Strong);

            lastMoveLeft = left;
            lastMoveRight = right;
        }

        void control_PC()
        {
            left = 0;
            right = 0;
            if (Input.GetKey(KeyCode.W) || forwardButtonPressed)
            {
                left += speed;
                right += speed;
            }
            if (Input.GetKey(KeyCode.S) || backButtonPressed)
            {
                left -= speed;
                right -= speed;
            }
            if (Input.GetKey(KeyCode.A) || leftButtonPressed)
            {
                left -= turnSpeed;
                right += turnSpeed;
            }
            if (Input.GetKey(KeyCode.D) || rightButtonPressed)
            {
                left += turnSpeed;
                right -= turnSpeed;
            }
            
            left = Mathf.Clamp(left, -100, 100);
            right = Mathf.Clamp(right, -100, 100);
        }
    }
}

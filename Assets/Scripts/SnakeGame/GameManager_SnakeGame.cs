using UnityEngine;
using toio;
using System.Linq;

public class GameManager_SnakeGame : MonoBehaviour
{
    [Header("接続方式")]
    public ConnectType connectType;

    [Header("Cube ID")]
    public string controller_id = "08FC46E9-1BC9-43D1-E360-A18C7D226902";
    public string snake_id = "8EB87781-3EDE-64ED-C989-8F8407E8166B";
    public string snake_id_1 = "34A1975D-97F5-6FDF-BA85-A950886C2B4C";
    public string snake_id_2 = "2363D218-9D33-BC01-6EFD-FD966F900FA3";
    public string snake_id_3 = "A201BF83-C425-BD5A-658A-F86C66453C78";
    public string snake_id_4 = "36406584-9463-4711-B7AC-9732C0E33C1B";

    [Header("スネーク操作")]
    public int forwardSpeed = 45;
    public int turnSpeed = 12;
    public float steeringGain = 0.4f;
    public float angleTolerance = 10f;
    public float controllerAngleTolerance = 1f;

    [Header("Cube")]
    Cube controller_c;
    Cube[] snake_c;
    const int SnakeCount = 5;
    int lastMoveLeft = int.MinValue;
    int lastMoveRight = int.MinValue;
    float targetAngle;
    bool hasTargetAngle;

    async void Start()
    {
        snake_c = new Cube[SnakeCount];

        CubeScanner scanner = new CubeScanner(connectType);
        BLEPeripheralInterface[] peripherals = await scanner.NearScan(20, 5f);

        if(peripherals == null)
        {
            Debug.LogError("Cubeのスキャンに失敗しました。");
            return;
        }

        BLEPeripheralInterface controllerPeripheral = peripherals.FirstOrDefault(peripheral =>
            peripheral != null && string.Equals(peripheral.device_address, controller_id, System.StringComparison.OrdinalIgnoreCase));
        BLEPeripheralInterface snakePeripheral = peripherals.FirstOrDefault(peripheral =>
            peripheral != null && string.Equals(peripheral.device_address, snake_id, System.StringComparison.OrdinalIgnoreCase));
        BLEPeripheralInterface snakePeripheral1 = peripherals.FirstOrDefault(peripheral =>
            peripheral != null && string.Equals(peripheral.device_address, snake_id_1, System.StringComparison.OrdinalIgnoreCase));
        BLEPeripheralInterface snakePeripheral2 = peripherals.FirstOrDefault(peripheral =>
            peripheral != null && string.Equals(peripheral.device_address, snake_id_2, System.StringComparison.OrdinalIgnoreCase));
        BLEPeripheralInterface snakePeripheral3 = peripherals.FirstOrDefault(peripheral =>
            peripheral != null && string.Equals(peripheral.device_address, snake_id_3, System.StringComparison.OrdinalIgnoreCase));
        BLEPeripheralInterface snakePeripheral4 = peripherals.FirstOrDefault(peripheral =>
            peripheral != null && string.Equals(peripheral.device_address, snake_id_4, System.StringComparison.OrdinalIgnoreCase));

        if (controllerPeripheral == null || snakePeripheral == null || snakePeripheral1 == null || snakePeripheral2 == null
            || snakePeripheral3 == null || snakePeripheral4 == null)
        {
            Debug.LogError("指定されたIDのCubeが見つかりませんでした。");
            return;
        }

        CubeConnecter connecter = new CubeConnecter(connectType);
        controller_c = await connecter.Connect(controllerPeripheral);
        snake_c[0] = await connecter.Connect(snakePeripheral);
        snake_c[1] = await connecter.Connect(snakePeripheral1);
        snake_c[2] = await connecter.Connect(snakePeripheral2);
        snake_c[3] = await connecter.Connect(snakePeripheral3);
        snake_c[4] = await connecter.Connect(snakePeripheral4);

        if (controller_c == null || snake_c.Any(cube => cube == null))
        {
            Debug.LogError("指定されたIDのCube接続に失敗しました。");
            return;
        }

        targetAngle = controller_c.angle;
        hasTargetAngle = true;
    }

    void Update()
    {
        if(controller_c == null || snake_c == null || snake_c.Length == 0 || snake_c[0] == null) return;
        if(!snake_c[0].isGrounded)
        {
            MoveSnake(0, 0);
            return;
        }

        MoveSnakeToControllerAngle();
    }

    void MoveSnakeToControllerAngle()
    {
        Cube snakeHead = snake_c[0];
        UpdateTargetAngle();

        float angleDiff = Mathf.DeltaAngle(snakeHead.angle, targetAngle);
        int steering = 0;

        if(Mathf.Abs(angleDiff) > angleTolerance)
        {
            steering = Mathf.RoundToInt(Mathf.Clamp(angleDiff * steeringGain, -turnSpeed, turnSpeed));
        }

        int left = Mathf.Clamp(forwardSpeed + steering, -100, 100);
        int right = Mathf.Clamp(forwardSpeed - steering, -100, 100);

        MoveSnake(left, right);
    }

    void UpdateTargetAngle()
    {
        if(!hasTargetAngle)
        {
            targetAngle = controller_c.angle;
            hasTargetAngle = true;
            return;
        }

        if(Mathf.Abs(Mathf.DeltaAngle(targetAngle, controller_c.angle)) >= controllerAngleTolerance)
        {
            targetAngle = controller_c.angle;
        }
    }

    void MoveSnake(int left, int right)
    {
        if(left == lastMoveLeft && right == lastMoveRight) return;

        snake_c[0].Move(left, right, 0, Cube.ORDER_TYPE.Strong);
        lastMoveLeft = left;
        lastMoveRight = right;
    }

}

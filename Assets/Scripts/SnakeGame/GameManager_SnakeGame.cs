using UnityEngine;
using toio;

public class GameManager_SnakeGame : MonoBehaviour
{
    [Header("接続方式")]
    public ConnectType connectType;

    [Header("Cube ID")]
    public string controller_id = "08FC46E9-1BC9-43D1-E360-A18C7D226902";
    public string snake_id = "8EB87781-3EDE-64ED-C989-8F8407E8166B";

    [Header("スネーク操作")]
    public int forwardSpeed = 45;
    public int turnSpeed = 12;
    public float steeringGain = 0.4f;
    public float angleTolerance = 10f;
    public float controllerAngleTolerance = 1f;

    [Header("Cube")]
    Cube controller_c;
    Cube[] snake_c;
    int lastMoveLeft = int.MinValue;
    int lastMoveRight = int.MinValue;
    float targetAngle;
    bool hasTargetAngle;

    async void Start()
    {
        snake_c = new Cube[1];

        CubeScanner scanner = new CubeScanner(connectType);
        BLEPeripheralInterface[] peripherals = await scanner.NearScan(20, 5f);

        BLEPeripheralInterface controllerPeripheral = FindPeripheralById(peripherals, controller_id, nameof(controller_id));
        BLEPeripheralInterface snakePeripheral = FindPeripheralById(peripherals, snake_id, nameof(snake_id));

        if (controllerPeripheral == null || snakePeripheral == null)
        {
            Debug.LogError("指定されたIDのCubeが見つかりませんでした。");
            return;
        }

        CubeConnecter connecter = new CubeConnecter(connectType);
        controller_c = await connecter.Connect(controllerPeripheral);
        snake_c[0] = await connecter.Connect(snakePeripheral);

        if (controller_c == null || snake_c[0] == null)
        {
            Debug.LogError("指定されたIDのCube接続に失敗しました。");
            return;
        }

        targetAngle = controller_c.angle;
        hasTargetAngle = true;
        Debug.Log("controller_c : " + controller_c.id);
        Debug.Log("snake_c : " + snake_c[0].id);
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

    BLEPeripheralInterface FindPeripheralById(BLEPeripheralInterface[] peripherals, string targetId, string roleName)
    {
        if(peripherals == null) return null;

        foreach(BLEPeripheralInterface peripheral in peripherals)
        {
            if(peripheral == null) continue;
            if(!string.Equals(peripheral.device_address, targetId, System.StringComparison.OrdinalIgnoreCase)) continue;

            Debug.Log(roleName + " : " + peripheral.device_name + " / " + peripheral.device_address);
            return peripheral;
        }

        Debug.LogError(roleName + " が見つかりません。ID=" + targetId);
        return null;
    }
}

using UnityEngine;
using toio;
using toio.Navigation;
using toio.Simulator;
using System.Collections.Generic;
using System.Linq;

public class GameManager_SnakeGame : MonoBehaviour
{
    enum SnakeGameState
    {
        Running,
        ReleaseAfterHeadSwitch
    }

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
    public float angleTolerance = 15f;
    public float controllerAngleTolerance = 10f;

    [Header("ステージ範囲")]
    public float matMinX = 98f;
    public float matMaxX = 402f;
    public float matMinY = 142f;
    public float matMaxY = 358f;
    public float matSafetyMargin = 15f;
    public bool keepSnakeInsideMat = false;
    public bool stopSnakeOutsideStage = true;
    public bool requireControllerInsideStage = true;

    [Header("ステージ範囲Gizmo")]
    [SerializeField] bool showStageGizmo = true;
    [Tooltip("Simulator上のMat。未指定の場合はScene内のMatを自動検索し、見つからなければSDKのデフォルト変換で描画する")]
    [SerializeField] Mat stageGizmoTarget;
    [SerializeField] Color stageGizmoOuterColor = new Color(0f, 0.75f, 1f, 1f);
    [SerializeField] Color stageGizmoSafeColor = new Color(0.1f, 1f, 0.25f, 1f);
    [SerializeField] Color stageGizmoCenterColor = new Color(1f, 0.85f, 0f, 1f);
    [SerializeField, Min(0f)] float stageGizmoHeightOffset = 0.01f;

    [Header("取得と頭切替")]
    public float cubeSize = 32f;
    public float joinDistance = 42f;
    public float releaseAfterHeadSwitchSeconds = 0.15f;

    [Header("後続Cube操作")]
    public float segmentSpacing = 58f;
    public float minimumSegmentClearance = 40f;
    public float followTargetTolerance = 10f;
    public float trailSampleDistance = 6f;
    public float trailKeepMargin = 120f;

    [Header("Navigator追尾")]
    public Navigator.Mode bodyNavigatorMode = Navigator.Mode.AVOID;
    public int bodyFollowMaxSpeed = 45;
    public int bodyFollowRotateTime = 250;
    public bool useNavigatorPrediction = true;

    [Header("Debug")]
    public bool debugHeadSwitch;

    [Header("Cube")]
    Cube controller_c;
    Cube[] snake_c;
    CubeNavigator[] snakeNavigators;
    const int SnakeCount = 5;

    struct TrailPoint
    {
        public Vector2 pos;
        public float distance;

        public TrailPoint(Vector2 pos, float distance)
        {
            this.pos = pos;
            this.distance = distance;
        }
    }

    readonly List<int> snakeOrder = new List<int>();
    readonly List<TrailPoint> headTrail = new List<TrailPoint>();
    int nextJoinIndex = 1;

    int[] lastMoveLeft;
    int[] lastMoveRight;

    SnakeGameState state = SnakeGameState.Running;
    float stateStartedAt;
    float targetAngle;
    bool hasTargetAngle;

    async void Start()
    {
        snake_c = new Cube[SnakeCount];
        lastMoveLeft = new int[SnakeCount];
        lastMoveRight = new int[SnakeCount];

        for(int i = 0; i < SnakeCount; i++)
        {
            lastMoveLeft[i] = int.MinValue;
            lastMoveRight[i] = int.MinValue;
        }

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

        if(controllerPeripheral == null || snakePeripheral == null || snakePeripheral1 == null || snakePeripheral2 == null
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

        if(controller_c == null || snake_c.Any(cube => cube == null))
        {
            Debug.LogError("指定されたIDのCube接続に失敗しました。");
            return;
        }

        InitializeSnakeNavigators();
        SetSnakeLedColors();
        ResetSnakeState();
        StopAllSnakes();
    }

    void Update()
    {
        if(!IsReady()) return;

        UpdateSnakeNavigators();

        switch(state)
        {
            case SnakeGameState.Running:
                UpdateRunning();
                break;
            case SnakeGameState.ReleaseAfterHeadSwitch:
                UpdateReleaseAfterHeadSwitch();
                break;
        }
    }

    void ResetSnakeState()
    {
        snakeOrder.Clear();
        snakeOrder.Add(0);

        nextJoinIndex = 1;
        state = SnakeGameState.Running;

        targetAngle = controller_c.angle;
        hasTargetAngle = true;
        ResetHeadTrailToCurrentOrder();
        RefreshSnakeNavigatorRelations();
    }

    bool IsReady()
    {
        return controller_c != null
            && snake_c != null
            && snake_c.Length == SnakeCount
            && snake_c.All(cube => cube != null)
            && snakeNavigators != null
            && snakeNavigators.Length == SnakeCount
            && snakeNavigators.All(navigator => navigator != null)
            && snakeOrder.Count > 0;
    }

    void UpdateRunning()
    {
        if(!IsControllerUsable())
        {
            StopAllSnakes();
            return;
        }

        int headIndex = GetCurrentHeadIndex();
        if(!CanMoveSnake(headIndex))
        {
            StopAllSnakes();
            return;
        }

        if(TryJoinNextSnake())
        {
            return;
        }

        MoveHeadToControllerAngle(headIndex);
        UpdateHeadTrail(headIndex);
        MoveBodySnakes();
        StopUnjoinedSnakes();
    }

    bool TryJoinNextSnake()
    {
        if(nextJoinIndex >= SnakeCount) return false;
        if(!IsSnakeGrounded(nextJoinIndex)) return false;

        Cube head = snake_c[GetCurrentHeadIndex()];
        Cube next = snake_c[nextJoinIndex];
        float distance = Vector2.Distance(head.pos, next.pos);
        if(distance > GetJoinDistance()) return false;

        StopAllSnakes();

        int newHeadIndex = nextJoinIndex;
        if(!snakeOrder.Contains(newHeadIndex))
        {
            snakeOrder.Insert(0, newHeadIndex);
        }

        if(nextJoinIndex == newHeadIndex)
        {
            nextJoinIndex++;
        }

        targetAngle = controller_c.angle;
        hasTargetAngle = true;
        ResetHeadTrailToCurrentOrder();
        RefreshSnakeNavigatorRelations();
        state = SnakeGameState.ReleaseAfterHeadSwitch;
        stateStartedAt = Time.time;
        StopAllSnakes();

        if(debugHeadSwitch)
        {
            Debug.Log($"Head switch complete: {string.Join(" -> ", snakeOrder.Select(index => $"snake{index}"))}");
        }

        return true;
    }

    void UpdateReleaseAfterHeadSwitch()
    {
        StopAllSnakes();

        if(Time.time - stateStartedAt < releaseAfterHeadSwitchSeconds) return;

        state = SnakeGameState.Running;
    }

    void MoveHeadToControllerAngle(int headIndex)
    {
        Cube snakeHead = snake_c[headIndex];
        bool returningToMatCenter = keepSnakeInsideMat && MustReturnToMatCenter(snakeHead);
        if(returningToMatCenter)
        {
            Vector2 toCenter = GetMatCenter() - snakeHead.pos;
            if(toCenter.sqrMagnitude > 1f)
            {
                targetAngle = Mathf.Atan2(toCenter.y, toCenter.x) * Mathf.Rad2Deg;
                hasTargetAngle = true;
            }
        }
        else
        {
            UpdateTargetAngle();
        }

        float angleDiff = Mathf.DeltaAngle(snakeHead.angle, targetAngle);
        int steering = 0;

        if(Mathf.Abs(angleDiff) > angleTolerance)
        {
            steering = Mathf.RoundToInt(Mathf.Clamp(angleDiff * steeringGain, -turnSpeed, turnSpeed));
        }

        int left = Mathf.Clamp(forwardSpeed + steering, -100, 100);
        int right = Mathf.Clamp(forwardSpeed - steering, -100, 100);

        MoveSnake(headIndex, left, right);
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

    void MoveBodySnakes()
    {
        for(int orderIndex = 1; orderIndex < snakeOrder.Count; orderIndex++)
        {
            int bodyIndex = snakeOrder[orderIndex];
            int previousIndex = snakeOrder[orderIndex - 1];

            if(!CanMoveSnake(bodyIndex) || !CanMoveSnake(previousIndex))
            {
                StopSnake(bodyIndex);
                continue;
            }

            if(Vector2.Distance(snake_c[bodyIndex].pos, snake_c[previousIndex].pos) < GetMinimumSegmentClearance())
            {
                StopSnake(bodyIndex);
                continue;
            }

            if(!TryGetTrailTarget(orderIndex, out Vector2 targetPosition))
            {
                StopSnake(bodyIndex);
                continue;
            }

            MoveSnakeTowardTrailTarget(bodyIndex, targetPosition);
        }
    }

    void MoveSnakeTowardTrailTarget(int bodyIndex, Vector2 targetPosition)
    {
        if(!IsSnakeJoined(bodyIndex))
        {
            StopSnake(bodyIndex);
            return;
        }

        Cube bodyCube = snake_c[bodyIndex];
        CubeNavigator navigator = snakeNavigators[bodyIndex];
        if(navigator == null)
        {
            StopSnake(bodyIndex);
            return;
        }

        Vector2 diff = targetPosition - bodyCube.pos;
        float distance = diff.magnitude;

        if(distance <= followTargetTolerance)
        {
            StopSnake(bodyIndex);
            return;
        }

        if(!CanSendMoveOrder(bodyIndex)) return;

        targetPosition = ClampToStage(targetPosition);
        navigator.mode = bodyNavigatorMode;
        navigator.usePred = useNavigatorPrediction;
        navigator.Navi2Target(
            targetPosition,
            maxSpd: GetBodyFollowMaxSpeed(),
            rotateTime: Mathf.Max(1, bodyFollowRotateTime),
            tolerance: Mathf.Max(1f, followTargetTolerance)
        ).Exec();
        InvalidateMoveCache(bodyIndex);
    }

    void ResetHeadTrailToCurrentOrder()
    {
        headTrail.Clear();

        if(snake_c == null || snakeOrder.Count == 0) return;

        float cumulativeDistance = 0f;
        Vector2 previousPosition = Vector2.zero;
        bool hasPreviousPosition = false;

        for(int orderIndex = snakeOrder.Count - 1; orderIndex >= 0; orderIndex--)
        {
            int snakeIndex = snakeOrder[orderIndex];
            if(snakeIndex < 0 || snakeIndex >= SnakeCount || snake_c[snakeIndex] == null) continue;

            Vector2 position = snake_c[snakeIndex].pos;
            if(hasPreviousPosition)
            {
                cumulativeDistance += Vector2.Distance(previousPosition, position);
            }

            headTrail.Add(new TrailPoint(position, cumulativeDistance));
            previousPosition = position;
            hasPreviousPosition = true;
        }
    }

    void UpdateHeadTrail(int headIndex)
    {
        if(!CanMoveSnake(headIndex)) return;

        Vector2 headPosition = snake_c[headIndex].pos;
        if(headTrail.Count == 0)
        {
            headTrail.Add(new TrailPoint(headPosition, 0f));
            return;
        }

        TrailPoint lastPoint = headTrail[headTrail.Count - 1];
        float moved = Vector2.Distance(lastPoint.pos, headPosition);
        if(moved < Mathf.Max(1f, trailSampleDistance)) return;

        headTrail.Add(new TrailPoint(headPosition, lastPoint.distance + moved));
        TrimHeadTrail();
    }

    void TrimHeadTrail()
    {
        if(headTrail.Count <= 2) return;

        float latestDistance = headTrail[headTrail.Count - 1].distance;
        float oldestNeededDistance = latestDistance - GetRequiredTrailDistance();

        while(headTrail.Count > 2 && headTrail[1].distance < oldestNeededDistance)
        {
            headTrail.RemoveAt(0);
        }
    }

    bool TryGetTrailTarget(int orderIndex, out Vector2 targetPosition)
    {
        targetPosition = Vector2.zero;
        if(headTrail.Count == 0) return false;

        float targetDistance = headTrail[headTrail.Count - 1].distance - orderIndex * GetSegmentSpacing();
        if(targetDistance <= headTrail[0].distance)
        {
            targetPosition = headTrail[0].pos;
            return true;
        }

        for(int i = headTrail.Count - 1; i > 0; i--)
        {
            TrailPoint current = headTrail[i];
            TrailPoint previous = headTrail[i - 1];
            if(previous.distance > targetDistance) continue;

            float segmentLength = current.distance - previous.distance;
            float t = segmentLength <= 0.001f
                ? 0f
                : Mathf.Clamp01((targetDistance - previous.distance) / segmentLength);
            targetPosition = Vector2.Lerp(previous.pos, current.pos, t);
            return true;
        }

        targetPosition = headTrail[0].pos;
        return true;
    }

    bool MustReturnToMatCenter(Cube cube)
    {
        if(cube == null || !cube.isGrounded) return false;

        return !IsInsideMatSafe(cube.pos);
    }

    void InitializeSnakeNavigators()
    {
        CubeNavigator.ClearGNavigators();

        snakeNavigators = new CubeNavigator[SnakeCount];
        RectInt matRect = GetMatRect();
        int borderMargin = Mathf.RoundToInt(Mathf.Max(0f, matSafetyMargin));
        int wallWidth = Mathf.Max(1, borderMargin);

        for(int i = 0; i < SnakeCount; i++)
        {
            CubeNavigator navigator = new CubeNavigator(snake_c[i], bodyNavigatorMode);
            navigator.usePred = useNavigatorPrediction;
            navigator.handle.SetBorderRect(matRect, borderMargin);
            navigator.ClearWall();
            navigator.AddBorder(wallWidth, matRect);
            snakeNavigators[i] = navigator;
        }

        RefreshSnakeNavigatorRelations();
    }

    void UpdateSnakeNavigators()
    {
        for(int i = 0; i < SnakeCount; i++)
        {
            if(snakeNavigators[i] == null) continue;
            if(!IsSnakeJoined(i)) continue;

            snakeNavigators[i].mode = bodyNavigatorMode;
            snakeNavigators[i].usePred = useNavigatorPrediction;
            snakeNavigators[i].Update();
        }
    }

    void RefreshSnakeNavigatorRelations()
    {
        if(snakeNavigators == null || snakeNavigators.Length != SnakeCount) return;

        for(int i = 0; i < SnakeCount; i++)
        {
            if(snakeNavigators[i] == null) continue;
            snakeNavigators[i].ClearOther();
        }

        for(int orderIndex = 0; orderIndex < snakeOrder.Count; orderIndex++)
        {
            int snakeIndex = snakeOrder[orderIndex];
            if(!IsValidSnakeIndex(snakeIndex) || snakeNavigators[snakeIndex] == null) continue;

            for(int otherOrderIndex = 0; otherOrderIndex < snakeOrder.Count; otherOrderIndex++)
            {
                if(orderIndex == otherOrderIndex) continue;

                int otherIndex = snakeOrder[otherOrderIndex];
                if(!IsValidSnakeIndex(otherIndex) || snakeNavigators[otherIndex] == null) continue;

                snakeNavigators[snakeIndex].AddOther(snakeNavigators[otherIndex]);
            }
        }
    }

    void OnDestroy()
    {
        CubeNavigator.ClearGNavigators();
    }

    Vector2 GetMatCenter()
    {
        return new Vector2((matMinX + matMaxX) * 0.5f, (matMinY + matMaxY) * 0.5f);
    }

    RectInt GetMatRect()
    {
        int minX = Mathf.RoundToInt(Mathf.Min(matMinX, matMaxX));
        int maxX = Mathf.RoundToInt(Mathf.Max(matMinX, matMaxX));
        int minY = Mathf.RoundToInt(Mathf.Min(matMinY, matMaxY));
        int maxY = Mathf.RoundToInt(Mathf.Max(matMinY, matMaxY));

        return new RectInt(minX, minY, Mathf.Max(1, maxX - minX), Mathf.Max(1, maxY - minY));
    }

    Vector2 ClampToStage(Vector2 position)
    {
        return new Vector2(
            Mathf.Clamp(position.x, Mathf.Min(matMinX, matMaxX), Mathf.Max(matMinX, matMaxX)),
            Mathf.Clamp(position.y, Mathf.Min(matMinY, matMaxY), Mathf.Max(matMinY, matMaxY))
        );
    }

    void OnDrawGizmos()
    {
        if(!showStageGizmo) return;

        Mat targetMat = GetStageGizmoTarget();

        float minX = Mathf.Min(matMinX, matMaxX);
        float maxX = Mathf.Max(matMinX, matMaxX);
        float minY = Mathf.Min(matMinY, matMaxY);
        float maxY = Mathf.Max(matMinY, matMaxY);

        DrawStageGizmoRect(targetMat, minX, maxX, minY, maxY, stageGizmoOuterColor);

        float safeMinX = minX + matSafetyMargin;
        float safeMaxX = maxX - matSafetyMargin;
        float safeMinY = minY + matSafetyMargin;
        float safeMaxY = maxY - matSafetyMargin;
        if(safeMinX < safeMaxX && safeMinY < safeMaxY)
        {
            DrawStageGizmoRect(targetMat, safeMinX, safeMaxX, safeMinY, safeMaxY, stageGizmoSafeColor);
        }

        Gizmos.color = stageGizmoCenterColor;
        Vector3 center = MatCoordToGizmoWorld(targetMat, (minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        const float centerMarkSize = 0.035f;
        Gizmos.DrawLine(center + Vector3.left * centerMarkSize, center + Vector3.right * centerMarkSize);
        Gizmos.DrawLine(center + Vector3.forward * centerMarkSize, center + Vector3.back * centerMarkSize);
        Gizmos.DrawSphere(center, centerMarkSize * 0.35f);
    }

    Mat GetStageGizmoTarget()
    {
        if(stageGizmoTarget != null) return stageGizmoTarget;
        return FindObjectOfType<Mat>();
    }

    void DrawStageGizmoRect(Mat targetMat, float minX, float maxX, float minY, float maxY, Color color)
    {
        Vector3 bottomLeft = MatCoordToGizmoWorld(targetMat, minX, minY);
        Vector3 bottomRight = MatCoordToGizmoWorld(targetMat, maxX, minY);
        Vector3 topRight = MatCoordToGizmoWorld(targetMat, maxX, maxY);
        Vector3 topLeft = MatCoordToGizmoWorld(targetMat, minX, maxY);

        Gizmos.color = color;
        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, topRight);
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topLeft, bottomLeft);
    }

    Vector3 MatCoordToGizmoWorld(Mat targetMat, float x, float y)
    {
        return Mat.MatCoord2UnityCoord(x, y, targetMat) + Vector3.up * stageGizmoHeightOffset;
    }

    bool IsInsideMatSafe(Vector2 position)
    {
        return position.x >= GetSafeMinX()
            && position.x <= GetSafeMaxX()
            && position.y >= GetSafeMinY()
            && position.y <= GetSafeMaxY();
    }

    bool IsInsideMat(Vector2 position)
    {
        return position.x >= Mathf.Min(matMinX, matMaxX)
            && position.x <= Mathf.Max(matMinX, matMaxX)
            && position.y >= Mathf.Min(matMinY, matMaxY)
            && position.y <= Mathf.Max(matMinY, matMaxY);
    }

    bool CanMoveSnake(int snakeIndex)
    {
        if(!IsSnakeJoined(snakeIndex)) return false;
        if(!IsSnakeGrounded(snakeIndex)) return false;
        if(!stopSnakeOutsideStage) return true;

        return IsInsideMat(snake_c[snakeIndex].pos);
    }

    bool IsControllerUsable()
    {
        if(controller_c == null) return false;
        if(!requireControllerInsideStage) return true;

        return controller_c.isGrounded && IsInsideMat(controller_c.pos);
    }

    float GetSafeMinX()
    {
        return Mathf.Min(matMinX, matMaxX) + matSafetyMargin;
    }

    float GetSafeMaxX()
    {
        return Mathf.Max(matMinX, matMaxX) - matSafetyMargin;
    }

    float GetSafeMinY()
    {
        return Mathf.Min(matMinY, matMaxY) + matSafetyMargin;
    }

    float GetSafeMaxY()
    {
        return Mathf.Max(matMinY, matMaxY) - matSafetyMargin;
    }

    float GetJoinDistance()
    {
        return Mathf.Max(1f, joinDistance);
    }

    float GetSegmentSpacing()
    {
        return Mathf.Max(segmentSpacing, GetMinimumSegmentClearance());
    }

    float GetRequiredTrailDistance()
    {
        return Mathf.Max(0, snakeOrder.Count - 1) * GetSegmentSpacing() + Mathf.Max(0f, trailKeepMargin);
    }

    float GetMinimumSegmentClearance()
    {
        return Mathf.Max(minimumSegmentClearance, cubeSize * 0.9f);
    }

    int GetBodyFollowMaxSpeed()
    {
        return Mathf.Clamp(bodyFollowMaxSpeed, 1, 100);
    }

    int GetCurrentHeadIndex()
    {
        return snakeOrder[0];
    }

    bool IsSnakeGrounded(int snakeIndex)
    {
        return IsValidSnakeIndex(snakeIndex)
            && snake_c[snakeIndex] != null
            && snake_c[snakeIndex].isGrounded;
    }

    bool CanSendMoveOrder(int snakeIndex)
    {
        return IsValidSnakeIndex(snakeIndex)
            && snake_c != null
            && snake_c[snakeIndex] != null
            && snake_c[snakeIndex].isConnected
            && CubeOrderBalancer.Instance.IsOrderable(snake_c[snakeIndex]);
    }

    void StopAllSnakes()
    {
        for(int i = 0; i < SnakeCount; i++)
        {
            StopSnake(i);
        }
    }

    bool IsSnakeJoined(int snakeIndex)
    {
        return snakeOrder.Contains(snakeIndex);
    }

    bool IsValidSnakeIndex(int snakeIndex)
    {
        return snakeIndex >= 0 && snakeIndex < SnakeCount;
    }

    void StopUnjoinedSnakes()
    {
        for(int i = 0; i < SnakeCount; i++)
        {
            if(snakeOrder.Contains(i)) continue;
            StopSnake(i);
        }
    }

    void StopSnake(int snakeIndex)
    {
        if(!IsValidSnakeIndex(snakeIndex)) return;
        if(snake_c == null || snake_c[snakeIndex] == null) return;
        if(lastMoveLeft != null && lastMoveRight != null
            && lastMoveLeft[snakeIndex] == 0 && lastMoveRight[snakeIndex] == 0) return;

        if(snakeNavigators != null && snakeNavigators.Length == SnakeCount && snakeNavigators[snakeIndex] != null)
        {
            snakeNavigators[snakeIndex].handle.Stop();
        }
        else
        {
            snake_c[snakeIndex].Move(0, 0, 0, Cube.ORDER_TYPE.Strong);
        }

        if(lastMoveLeft == null || lastMoveRight == null) return;
        lastMoveLeft[snakeIndex] = 0;
        lastMoveRight[snakeIndex] = 0;
    }

    void MoveSnake(int snakeIndex, int left, int right)
    {
        if(!IsValidSnakeIndex(snakeIndex)) return;
        if(!IsSnakeJoined(snakeIndex)) return;
        if(snake_c == null || snake_c[snakeIndex] == null) return;
        if(lastMoveLeft != null && lastMoveRight != null
            && left == lastMoveLeft[snakeIndex] && right == lastMoveRight[snakeIndex]) return;
        if(!CanSendMoveOrder(snakeIndex)) return;

        snake_c[snakeIndex].Move(left, right, 0, Cube.ORDER_TYPE.Strong);

        if(lastMoveLeft == null || lastMoveRight == null) return;
        lastMoveLeft[snakeIndex] = left;
        lastMoveRight[snakeIndex] = right;
    }

    void InvalidateMoveCache(int snakeIndex)
    {
        if(!IsValidSnakeIndex(snakeIndex)) return;
        if(lastMoveLeft == null || lastMoveRight == null) return;

        lastMoveLeft[snakeIndex] = int.MinValue;
        lastMoveRight[snakeIndex] = int.MinValue;
    }

    void SetSnakeLedColors()
    {
        snake_c[0].TurnLedOn(255, 0, 0, 0);
        snake_c[1].TurnLedOn(0, 120, 255, 0);
        snake_c[2].TurnLedOn(0, 255, 0, 0);
        snake_c[3].TurnLedOn(255, 180, 0, 0);
        snake_c[4].TurnLedOn(180, 0, 255, 0);
    }
}

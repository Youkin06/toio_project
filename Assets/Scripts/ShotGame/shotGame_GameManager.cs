using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using toio;
using Cysharp.Threading.Tasks;
using TMPro;
using DG.Tweening;

public class shotGame_GameManager : MonoBehaviour
{
    const int RequiredCubeCount = 6;
    const int PatrolTargetCount = 4;
    const string Player0ButtonListenerKey = "shotGame_Player0Button";
    const string Player1ButtonListenerKey = "shotGame_Player1Button";
    const string HitReactionListenerKey = "shotGame_HitReaction";

    // 6台確認時に取得した、各toioのBLEアドレス。
    const string Player0Address = "A201BF83-C425-BD5A-658A-F86C66453C78";
    const string Player1Address = "36406584-9463-4711-B7AC-9732C0E33C1B";
    const string Enemy0Player0Address = "34A1975D-97F5-6FDF-BA85-A950886C2B4C";
    const string Enemy1Player0Address = "08FC46E9-1BC9-43D1-E360-A18C7D226902";
    const string Enemy0Player1Address = "8EB87781-3EDE-64ED-C989-8F8407E8166B";
    const string Enemy1Player1Address = "2363D218-9D33-BC01-6EFD-FD966F900FA3";

    [Header("接続方式")]
    public ConnectType connectType;

    [Header("Cube")]
    CubeManager cm;
    Cube player0_cube;
    Cube player1_cube;
    Cube enemy0_player0_cube; // プレイヤー0が倒すべき敵
    Cube enemy1_player0_cube; // プレイヤー0が倒すべき敵
    Cube enemy0_player1_cube; // プレイヤー1が倒すべき敵
    Cube enemy1_player1_cube; // プレイヤー1が倒すべき敵

    Cube[] allEnemies;
    collision collisionManager;
    readonly Dictionary<Cube, HitReactionState> activeHitReactions =
        new Dictionary<Cube, HitReactionState>();
    readonly Dictionary<Cube, int> patrolTargetIndices = new Dictionary<Cube, int>();
    int nextHitReactionConfigId = 100;

    enum HitReactionPhase
    {
        Rotating,
        WaitingForMovement
    }

    enum BgmPlaybackState
    {
        None,
        GameStart,
        GamePlaying,
        GameEnd
    }

    sealed class HitReactionState
    {
        public int configId;
        public HitReactionPhase phase;
        public Vector2 resumePosition;
        public bool resumeCommandSent;
    }

    sealed class ProgressCubeManager : CubeManager
    {
        public ProgressCubeManager(ConnectType type) : base(type)
        {
        }

        public async UniTask<Cube[]> MultiConnectWithProgress(
            int cubeCount,
            Action<int> onConnectedCountChanged)
        {
            BLEPeripheralInterface[] peripherals = await scanner.NearScan(cubeCount);
            List<Cube> connectedCubes = new List<Cube>();
            if (peripherals == null) return connectedCubes.ToArray();

            foreach (BLEPeripheralInterface peripheral in peripherals)
            {
                if (peripheral == null) continue;

                Cube cube;
                if (cubeTable.TryGetValue(peripheral.device_address, out cube))
                {
                    await connecter.ReConnect(cube);
                }
                else
                {
                    cube = await connecter.Connect(peripheral);
                    if (cube != null) AddCube(cube);
                }

                if (cube != null && cube.isConnected)
                {
                    connectedCubes.Add(cube);
                }
                onConnectedCountChanged?.Invoke(connectedCubes.Count);
            }

            return connectedCubes.ToArray();
        }
    }

    [Header("ポイント")]
    [SerializeField] int point_player0;
    [SerializeField] int point_player1;
    [SerializeField] TMP_Text point_player0_text;
    [SerializeField] TMP_Text point_player1_text;

    [Header("タイマー")]
    [Tooltip("ゲームの残り時間（秒）")]
    [SerializeField, Min(1)] int timer = 60;
    [SerializeField] TMP_Text timer_text;

    [Header("得点 / 接続状態 / タイマー演出")]
    [SerializeField] float connectionStatusY = -210f;
    [SerializeField] float timerY = 0f;
    [SerializeField, Min(1f)] float connectionStatusFontSize = 80f;
    [SerializeField, Min(1f)] float timerFontSize = 100f;
    [SerializeField, Min(0.1f)] float connectionTypewriterDuration = 1.2f;
    [SerializeField, Min(0.05f)] float timerMoveDuration = 0.5f;
    [SerializeField, Min(0.05f)] float scoreAppearDuration = 0.45f;
    [SerializeField, Min(0f)] float scoreAppearStagger = 0.1f;

    Tween connectionStatusTextTween;
    Sequence timerUiTween;
    Sequence scoreUiTween;
    Vector3 player0ScoreBaseScale = Vector3.one;
    Vector3 player1ScoreBaseScale = Vector3.one;
    float player0ScoreBaseAlpha = 1f;
    float player1ScoreBaseAlpha = 1f;

    [Header("スタート / リトライボタン")]
    [SerializeField] Button startRetryButton;
    [SerializeField] Image startRetryButtonImage;
    [SerializeField] Sprite startButtonSprite;
    [SerializeField] Sprite retryButtonSprite;

    [Header("スタート / リトライボタン演出")]
    [SerializeField, Min(0.05f)] float buttonAppearDuration = 0.4f;
    [SerializeField, Range(1f, 1.1f)] float buttonIdlePulseScale = 1.025f;
    [SerializeField, Min(0.1f)] float buttonIdlePulseDuration = 0.8f;
    [SerializeField, Range(1f, 1.3f)] float buttonHoverScale = 1.08f;
    [SerializeField, Min(0.05f)] float buttonHoverDuration = 0.15f;
    [SerializeField, Min(0.05f)] float buttonPressDuration = 0.1f;

    Tween startRetryButtonTween;
    Vector3 startRetryButtonBaseScale = Vector3.one;
    bool hasStartRetryButtonBaseScale;
    bool isStartRetryButtonTransitioning;
    bool areStartRetryButtonPointerEventsRegistered;
    bool isPointerOverStartRetryButton;

    int initialTimer;
    float timerAccumulator;
    bool isGameRunning;
    bool cubesReady;

    [Header("BGM")]
    [Tooltip("未設定の場合はこのGameObjectにAudioSourceを自動追加する")]
    [SerializeField] AudioSource bgmAudioSource;
    [Tooltip("スタートボタン押下時に1回再生する曲。終了後にゲーム中BGMへ切り替える")]
    [SerializeField] AudioClip gameStartBgm;
    [Tooltip("ゲーム中にループ再生する曲")]
    [SerializeField] AudioClip gamePlayingBgm;
    [Tooltip("ゲーム終了後のリトライ画面でループ再生する曲")]
    [SerializeField] AudioClip gameEndBgm;
    [SerializeField, Range(0f, 1f)] float bgmVolume = 0.5f;

    BgmPlaybackState bgmPlaybackState;

    [Header("レーザー設定")]
    [Tooltip("レーザーの射程（toioマットの座標単位）")]
    [SerializeField] float laserRange = 300f;
    [Tooltip("レーザーの中心線から左右に当たる幅")]
    [SerializeField] float laserHalfWidth = 1f;

    [Header("敵の移動設定")]
    [SerializeField, Range(10, 100)] int enemySpeed = 70;
    [SerializeField, Range(10, 100)] int enemyTurnSpeed = 55;
    [SerializeField, Min(50)] int enemyMoveDurationMs = 200;
    [Tooltip("巡回目的地を次へ切り替える距離")]
    [SerializeField, Min(5f)] float patrolTargetReachDistance = 10f;
    [Tooltip("目的地への移動中に左右へ蛇行する角度")]
    [SerializeField, Range(0f, 30f)] float patrolSwerveAngle = 12f;
    [Tooltip("蛇行の左右を切り替える周期（秒）")]
    [SerializeField, Min(1f)] float patrolSwervePeriod = 2.4f;
    [Tooltip("この距離より近い敵がいる場合は回避する")]
    [SerializeField, Min(20f)] float enemyAvoidanceDistance = 45f;
    [Tooltip("密着時に後退して離れる距離")]
    [SerializeField, Min(20f)] float enemyEscapeDistance = 34f;

    [Header("マット範囲")]
    [Tooltip("簡易プレイマットの初期値。使用マットに合わせてInspectorから変更する")]
    [SerializeField] int matMinX = 98;
    [SerializeField] int matMaxX = 402;
    [SerializeField] int matMinY = 142;
    [SerializeField] int matMaxY = 358;
    [Tooltip("キューブ中心がマット端に近づかないための余白")]
    [SerializeField, Min(12f)] float matSafetyMargin = 15f;
    [Tooltip("進行方向のこの距離先が安全範囲外なら事前に旋回する")]
    [SerializeField, Min(10f)] float edgeLookAheadDistance = 12f;
    [Tooltip("被弾後、この距離だけ座標が変わったら再移動開始とみなす")]
    [SerializeField, Min(1f)] float hitResumeDistance = 5f;

    void Awake()
    {
        initialTimer = Mathf.Max(1, timer);
        timer = initialTimer;
        point_player0 = 0;
        point_player1 = 0;
        isGameRunning = false;
        bgmPlaybackState = BgmPlaybackState.None;

        EnsureBgmAudioSource();

        UpdatePointTexts();
        PrepareScoreUi();
        PrepareConnectionStatusUi();
        ShowStartRetryButton(startButtonSprite);
    }

    async void Start()
    {
        collisionManager = GetComponent<collision>();
        if (collisionManager == null)
        {
            collisionManager = gameObject.AddComponent<collision>();
        }

        ProgressCubeManager progressCubeManager = new ProgressCubeManager(connectType);
        cm = progressCubeManager;
        Cube[] cubes = await progressCubeManager.MultiConnectWithProgress(
            RequiredCubeCount,
            UpdateConnectionStatusText);

        if (cubes == null || cubes.Length < RequiredCubeCount)
        {
            int count = cubes == null ? 0 : cubes.Length;
            Debug.LogError($"Cubeの接続に失敗しました。接続数: {count}/{RequiredCubeCount}台");
            return;
        }

        player0_cube = FindCubeByAddress(cubes, Player0Address, nameof(player0_cube));
        player1_cube = FindCubeByAddress(cubes, Player1Address, nameof(player1_cube));
        enemy0_player0_cube = FindCubeByAddress(cubes, Enemy0Player0Address, nameof(enemy0_player0_cube));
        enemy1_player0_cube = FindCubeByAddress(cubes, Enemy1Player0Address, nameof(enemy1_player0_cube));
        enemy0_player1_cube = FindCubeByAddress(cubes, Enemy0Player1Address, nameof(enemy0_player1_cube));
        enemy1_player1_cube = FindCubeByAddress(cubes, Enemy1Player1Address, nameof(enemy1_player1_cube));

        if (!AllRolesAssigned())
        {
            Debug.LogError("Cubeの役割割り当てに失敗したため、ゲームを開始しません。");
            return;
        }

        allEnemies = new[]
        {
            enemy0_player0_cube,
            enemy1_player0_cube,
            enemy0_player1_cube,
            enemy1_player1_cube
        };

        player0_cube.buttonCallback.AddListener(Player0ButtonListenerKey, OnPlayerButtonChanged);
        player1_cube.buttonCallback.AddListener(Player1ButtonListenerKey, OnPlayerButtonChanged);

        foreach (Cube enemy in allEnemies)
        {
            enemy.targetMoveCallback.AddListener(HitReactionListenerKey, OnHitRotationCompleted);
        }

        SetEnemyRoleLeds();
        cubesReady = true;
        Debug.Log("[Cube割り当て] 6台すべての役割を設定しました。");
    }

    Cube FindCubeByAddress(Cube[] cubes, string address, string roleName)
    {
        foreach (Cube cube in cubes)
        {
            if (!string.Equals(cube.addr, address, StringComparison.OrdinalIgnoreCase)) continue;

            Debug.Log($"[Cube割り当て] {roleName} = {cube.localName} / {cube.addr}");
            return cube;
        }

        Debug.LogError($"[Cube割り当て] {roleName} のCubeが見つかりません。Address={address}");
        return null;
    }

    bool AllRolesAssigned()
    {
        return player0_cube != null
            && player1_cube != null
            && enemy0_player0_cube != null
            && enemy1_player0_cube != null
            && enemy0_player1_cube != null
            && enemy1_player1_cube != null;
    }

    void Update()
    {
        UpdateBgm();
        UpdateTimer();

        if (!isGameRunning || cm == null || allEnemies == null) return;

        foreach (Cube enemy in allEnemies)
        {
            if (enemy == null) continue;

            if (activeHitReactions.TryGetValue(enemy, out HitReactionState hitState))
            {
                if (hitState.phase == HitReactionPhase.Rotating) continue;

                bool sentTranslationCommand = MoveEnemy(enemy);
                if (sentTranslationCommand && !hitState.resumeCommandSent)
                {
                    hitState.resumePosition = enemy.pos;
                    hitState.resumeCommandSent = true;
                }

                // 命令送信だけではなく、実機の座標変化を確認してから再弾を許可する。
                if (hitState.resumeCommandSent
                    && Vector2.Distance(hitState.resumePosition, enemy.pos) >= hitResumeDistance)
                {
                    activeHitReactions.Remove(enemy);
                    Debug.Log($"{enemy.localName}が再移動したため、被弾判定を再開しました。");
                }

                continue;
            }

            MoveEnemy(enemy);
        }
    }

    /// <summary>
    /// マット端と他の敵を回避しながら移動する。
    /// 平行移動を含む命令を送った場合だけtrueを返す。
    /// </summary>
    bool MoveEnemy(Cube enemy)
    {
        if (enemy == null || cm == null || !cm.IsControllable(enemy)) return false;

        if (!enemy.isGrounded)
        {
            enemy.Move(0, 0, 100, Cube.ORDER_TYPE.Strong);
            return false;
        }

        int enemyIndex = Array.IndexOf(allEnemies, enemy);
        Cube nearestEnemy = FindNearestEnemy(enemy, out float nearestDistance);
        bool mustReturnToMatCenter = MustReturnToMatCenter(enemy);

        // 密着した場合は、偶数・奇数で旋回方向を分けて完全対称の行き詰まりを防ぐ。
        // マット端では後退が落下につながるため、中央への復帰を優先する。
        if (!mustReturnToMatCenter
            && nearestEnemy != null
            && (nearestDistance <= enemyEscapeDistance || enemy.isCollisionDetected))
        {
            int curve = enemyIndex % 2 == 0 ? 25 : -25;
            int left = Mathf.Clamp(-enemySpeed + curve, -100, 100);
            int right = Mathf.Clamp(-enemySpeed - curve, -100, 100);
            enemy.Move(left, right, enemyMoveDurationMs, Cube.ORDER_TYPE.Strong);
            return true;
        }

        if (mustReturnToMatCenter)
        {
            // 落下防止だけはその場で中央方向へ向き直す。
            return MoveTowardDirection(enemy, GetMatCenter() - enemy.pos, true);
        }

        if (nearestEnemy != null && nearestDistance <= enemyAvoidanceDistance)
        {
            // 回避中も停止して方向転換せず、前進しながら大きく曲がる。
            return MoveTowardDirection(enemy, enemy.pos - nearestEnemy.pos, false);
        }

        return MoveAcrossMat(enemy, enemyIndex);
    }

    bool MoveTowardDirection(
        Cube enemy,
        Vector2 desiredDirection,
        bool allowStationaryTurn,
        float desiredAngleOffset = 0f,
        int forwardSpeedOverride = -1)
    {
        if (desiredDirection.sqrMagnitude < 0.01f) return false;

        float desiredAngle = Mathf.Atan2(desiredDirection.y, desiredDirection.x) * Mathf.Rad2Deg
            + desiredAngleOffset;
        float angleDifference = Mathf.DeltaAngle(enemy.angle, desiredAngle);

        if (allowStationaryTurn && Mathf.Abs(angleDifference) > 20f)
        {
            int turn = angleDifference > 0f ? enemyTurnSpeed : -enemyTurnSpeed;
            enemy.Move(turn, -turn, enemyMoveDurationMs);
            return false;
        }

        float steeringGain = allowStationaryTurn ? 0.8f : 1.2f;
        int steering = Mathf.RoundToInt(Mathf.Clamp(angleDifference * steeringGain,
            -enemyTurnSpeed, enemyTurnSpeed));
        int forwardSpeed = forwardSpeedOverride >= 0
            ? forwardSpeedOverride
            : allowStationaryTurn
                ? enemySpeed
                : Mathf.Max(30, Mathf.RoundToInt(enemySpeed * 0.65f));
        int moveLeft = Mathf.Clamp(forwardSpeed + steering, -100, 100);
        int moveRight = Mathf.Clamp(forwardSpeed - steering, -100, 100);
        enemy.Move(moveLeft, moveRight, enemyMoveDurationMs);
        return true;
    }

    bool MoveAcrossMat(Cube enemy, int enemyIndex)
    {
        Vector2 patrolTarget = GetPatrolTarget(enemy, enemyIndex);

        // 目的地へ直進し続けないよう、機体ごとに位相をずらして蛇行させる。
        float phaseOffset = Mathf.Max(0, enemyIndex) * 0.7f;
        float period = Mathf.Max(1f, patrolSwervePeriod);
        float swerve = Mathf.Sin((Time.time + phaseOffset) * Mathf.PI * 2f / period)
            * patrolSwerveAngle;

        return MoveTowardDirection(
            enemy,
            patrolTarget - enemy.pos,
            false,
            swerve,
            enemySpeed);
    }

    Vector2 GetPatrolTarget(Cube enemy, int enemyIndex)
    {
        if (!patrolTargetIndices.TryGetValue(enemy, out int targetIndex))
        {
            // 初期目的地を全台で分散させる。
            targetIndex = PositiveModulo(Mathf.Max(0, enemyIndex) * 3, PatrolTargetCount);
            patrolTargetIndices[enemy] = targetIndex;
        }

        Vector2 patrolTarget = GetPatrolTargetPosition(targetIndex);
        if (Vector2.Distance(enemy.pos, patrolTarget) <= patrolTargetReachDistance)
        {
            // 全台が同じ巡回方向で、異なる区間を移動するため、正面衝突しにくい。
            targetIndex = PositiveModulo(targetIndex + 1, PatrolTargetCount);
            patrolTargetIndices[enemy] = targetIndex;
            patrolTarget = GetPatrolTargetPosition(targetIndex);
        }

        return patrolTarget;
    }

    Vector2 GetPatrolTargetPosition(int targetIndex)
    {
        float inset = matSafetyMargin + edgeLookAheadDistance + 2f;
        float minX = Mathf.Min(matMinX, matMaxX) + inset;
        float maxX = Mathf.Max(matMinX, matMaxX) - inset;
        float minY = Mathf.Min(matMinY, matMaxY) + inset;
        float maxY = Mathf.Max(matMinY, matMaxY) - inset;

        // 対角線移動を含む順番にし、マット全体を横切らせる。
        switch (PositiveModulo(targetIndex, PatrolTargetCount))
        {
            case 0: return new Vector2(minX, minY);
            case 1: return new Vector2(maxX, maxY);
            case 2: return new Vector2(minX, maxY);
            default: return new Vector2(maxX, minY);
        }
    }

    int PositiveModulo(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    Cube FindNearestEnemy(Cube source, out float nearestDistance)
    {
        nearestDistance = float.MaxValue;
        Cube nearest = null;
        if (allEnemies == null) return null;

        foreach (Cube candidate in allEnemies)
        {
            if (candidate == null || candidate == source || !candidate.isGrounded) continue;

            float distance = Vector2.Distance(source.pos, candidate.pos);
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            nearest = candidate;
        }

        return nearest;
    }

    bool MustReturnToMatCenter(Cube enemy)
    {
        float safeMinX = Mathf.Min(matMinX, matMaxX) + matSafetyMargin;
        float safeMaxX = Mathf.Max(matMinX, matMaxX) - matSafetyMargin;
        float safeMinY = Mathf.Min(matMinY, matMaxY) + matSafetyMargin;
        float safeMaxY = Mathf.Max(matMinY, matMaxY) - matSafetyMargin;

        float headingRad = enemy.angle * Mathf.Deg2Rad;
        Vector2 heading = new Vector2(Mathf.Cos(headingRad), Mathf.Sin(headingRad));
        Vector2 lookAheadPosition = enemy.pos + heading * edgeLookAheadDistance;

        return !IsInside(enemy.pos, safeMinX, safeMaxX, safeMinY, safeMaxY)
            || !IsInside(lookAheadPosition, safeMinX, safeMaxX, safeMinY, safeMaxY);
    }

    bool IsInside(Vector2 position, float minX, float maxX, float minY, float maxY)
    {
        return position.x >= minX && position.x <= maxX
            && position.y >= minY && position.y <= maxY;
    }

    Vector2 GetMatCenter()
    {
        return new Vector2((matMinX + matMaxX) * 0.5f, (matMinY + matMaxY) * 0.5f);
    }

    void OnPlayerButtonChanged(Cube player)
    {
        if (!player.isPressed || !isGameRunning) return;

        if (player == player0_cube)
        {
            FireLaser(player0_cube, enemy0_player0_cube, enemy1_player0_cube, "player0");
        }
        else if (player == player1_cube)
        {
            FireLaser(player1_cube, enemy0_player1_cube, enemy1_player1_cube, "player1");
        }
    }

    void FireLaser(Cube player, Cube enemy0, Cube enemy1, string playerName)
    {
        Cube hitEnemy = null;
        float nearestDistance = float.MaxValue;

        // 担当外の敵も含め、全4体の中からレーザー上で最も近い敵を探す。
        foreach (Cube enemy in allEnemies)
        {
            if (!collisionManager.CheckLaserCollision(player, enemy, laserRange, laserHalfWidth)) continue;

            float distance = (enemy.pos - player.pos).sqrMagnitude;
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            hitEnemy = enemy;
        }

        if (hitEnemy == null)
        {
            Debug.Log($"{playerName}のレーザーは外れた");
            return;
        }

        // 被弾中の敵はレーザーを遮るが、再移動するまで得点対象にしない。
        if (activeHitReactions.ContainsKey(hitEnemy))
        {
            Debug.Log($"{playerName}のレーザーは被弾中の {hitEnemy.localName} に当たった（得点なし）");
            return;
        }

        bool isCorrectTarget = hitEnemy == enemy0 || hitEnemy == enemy1;
        if (isCorrectTarget)
        {
            Debug.Log($"{playerName}が攻撃すべき敵 {hitEnemy.localName} に当たった（青LED）");
            AddPoint(player, 100);
        }
        else
        {
            Debug.Log($"{playerName}が攻撃すべきでない敵 {hitEnemy.localName} に当たった（赤LED）");
            AddPoint(player, -100);
        }

        PlayHitReaction(hitEnemy).Forget();
    }

    void AddPoint(Cube player, int amount)
    {
        if (player == player0_cube)
        {
            point_player0 += amount;
            Debug.Log($"player0: {amount:+#;-#;0} / 合計 {point_player0} point");
        }
        else if (player == player1_cube)
        {
            point_player1 += amount;
            Debug.Log($"player1: {amount:+#;-#;0} / 合計 {point_player1} point");
        }

        UpdatePointTexts();
    }

    void UpdatePointTexts()
    {
        if (point_player0_text != null)
        {
            point_player0_text.text = point_player0.ToString();
        }
        if (point_player1_text != null)
        {
            point_player1_text.text = point_player1.ToString();
        }
    }

    void PrepareScoreUi()
    {
        if (point_player0_text != null)
        {
            player0ScoreBaseScale = point_player0_text.rectTransform.localScale;
            player0ScoreBaseAlpha = point_player0_text.color.a;
            point_player0_text.gameObject.SetActive(false);
        }

        if (point_player1_text != null)
        {
            player1ScoreBaseScale = point_player1_text.rectTransform.localScale;
            player1ScoreBaseAlpha = point_player1_text.color.a;
            point_player1_text.gameObject.SetActive(false);
        }
    }

    void AnimateScoreUi()
    {
        if (scoreUiTween != null)
        {
            scoreUiTween.Kill();
            scoreUiTween = null;
        }

        scoreUiTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(this);

        InsertScoreAppearTween(
            scoreUiTween,
            point_player0_text,
            player0ScoreBaseScale,
            player0ScoreBaseAlpha,
            0f);
        InsertScoreAppearTween(
            scoreUiTween,
            point_player1_text,
            player1ScoreBaseScale,
            player1ScoreBaseAlpha,
            scoreAppearStagger);
    }

    void InsertScoreAppearTween(
        Sequence sequence,
        TMP_Text scoreText,
        Vector3 baseScale,
        float baseAlpha,
        float delay)
    {
        if (scoreText == null) return;

        scoreText.gameObject.SetActive(true);
        scoreText.rectTransform.localScale = baseScale * 0.7f;
        Color color = scoreText.color;
        color.a = 0f;
        scoreText.color = color;

        sequence.Insert(
            delay,
            scoreText.rectTransform
                .DOScale(baseScale, scoreAppearDuration)
                .SetEase(Ease.OutBack));
        sequence.Insert(
            delay,
            scoreText
                .DOFade(baseAlpha, scoreAppearDuration * 0.75f)
                .SetEase(Ease.OutQuad));
    }

    void PrepareConnectionStatusUi()
    {
        if (timer_text == null) return;

        RectTransform timerRect = timer_text.rectTransform;
        Vector2 position = timerRect.anchoredPosition;
        position.y = connectionStatusY;
        timerRect.anchoredPosition = position;
        timer_text.enableAutoSizing = false;
        timer_text.fontSize = connectionStatusFontSize;

        PlayConnectionStatusTypewriter(0);
    }

    string GetConnectionStatusMessage(int connectedCount)
    {
        int count = Mathf.Clamp(connectedCount, 0, RequiredCubeCount);
        return $"{count}/{RequiredCubeCount} 個toio が接続されてるよ！";
    }

    void PlayConnectionStatusTypewriter(int connectedCount)
    {
        if (timer_text == null) return;

        if (connectionStatusTextTween != null)
        {
            connectionStatusTextTween.Kill();
            connectionStatusTextTween = null;
        }

        string fullText = GetConnectionStatusMessage(connectedCount);
        int visibleCharacterCount = 0;
        timer_text.text = string.Empty;
        connectionStatusTextTween = DOTween.To(
                () => visibleCharacterCount,
                value =>
                {
                    visibleCharacterCount = value;
                    timer_text.text = fullText.Substring(0, value);
                },
                fullText.Length,
                connectionTypewriterDuration)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .SetTarget(timer_text);
    }

    void UpdateConnectionStatusText(int connectedCount)
    {
        if (timer_text == null || isGameRunning) return;

        if (connectionStatusTextTween != null)
        {
            connectionStatusTextTween.Kill();
            connectionStatusTextTween = null;
        }

        timer_text.text = GetConnectionStatusMessage(connectedCount);
    }

    void AnimateTimerUiToGamePosition()
    {
        if (timer_text == null) return;

        if (connectionStatusTextTween != null)
        {
            connectionStatusTextTween.Kill();
            connectionStatusTextTween = null;
        }
        if (timerUiTween != null)
        {
            timerUiTween.Kill();
            timerUiTween = null;
        }

        timer_text.text = timer.ToString();
        RectTransform timerRect = timer_text.rectTransform;
        timerUiTween = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(timer_text);
        timerUiTween.Join(
            timerRect
                .DOAnchorPosY(timerY, timerMoveDuration)
                .SetEase(Ease.OutCubic));
        timerUiTween.Join(
            DOTween.To(
                    () => timer_text.fontSize,
                    value => timer_text.fontSize = value,
                    timerFontSize,
                    timerMoveDuration)
                .SetEase(Ease.OutCubic));
    }

    void UpdateTimerText()
    {
        if (timer_text != null)
        {
            timer_text.text = timer.ToString();
        }
    }

    void UpdateTimer()
    {
        if (!isGameRunning) return;

        timerAccumulator += Time.deltaTime;
        while (timerAccumulator >= 1f && timer > 0)
        {
            timerAccumulator -= 1f;
            timer--;
            UpdateTimerText();
        }

        if (timer <= 0)
        {
            FinishGame();
        }
    }

    /// <summary>
    /// Unity UI ButtonのOnClickから呼び出す。
    /// 初回はスタート、タイマー終了後はリトライとして動作する。
    /// </summary>
    public void OnStartRetryButtonPressed()
    {
        if (isStartRetryButtonTransitioning) return;

        if (!cubesReady)
        {
            Debug.LogWarning("Cubeの接続が完了するまで待ってください。");
            return;
        }

        PlayStartRetryButtonPressAnimation();
    }

    void StartGame()
    {
        isStartRetryButtonTransitioning = false;
        isPointerOverStartRetryButton = false;

        point_player0 = 0;
        point_player1 = 0;
        timer = initialTimer;
        timerAccumulator = 0f;
        activeHitReactions.Clear();
        isGameRunning = true;
        PlayGameStartBgm();

        UpdatePointTexts();
        AnimateScoreUi();
        AnimateTimerUiToGamePosition();

        if (startRetryButton != null)
        {
            startRetryButton.transform.localScale = startRetryButtonBaseScale;
            startRetryButton.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("Start Retry ButtonがInspectorで設定されていません。");
        }

        Debug.Log("ゲームスタート");
    }

    void PlayStartRetryButtonPressAnimation()
    {
        if (startRetryButton == null)
        {
            StartGame();
            return;
        }

        CaptureStartRetryButtonBaseScale();
        isStartRetryButtonTransitioning = true;
        startRetryButton.interactable = false;
        KillStartRetryButtonTween();

        Transform buttonTransform = startRetryButton.transform;
        buttonTransform.localScale = startRetryButtonBaseScale;

        Sequence pressSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(buttonTransform);
        pressSequence.Append(
            buttonTransform.DOScale(startRetryButtonBaseScale * 0.9f, buttonPressDuration)
                .SetEase(Ease.InQuad));
        pressSequence.Append(
            buttonTransform.DOScale(startRetryButtonBaseScale * 1.05f, buttonPressDuration)
                .SetEase(Ease.OutQuad));
        pressSequence.Append(
            buttonTransform.DOScale(startRetryButtonBaseScale, buttonPressDuration)
                .SetEase(Ease.OutBack));
        pressSequence.OnComplete(StartGame);
        startRetryButtonTween = pressSequence;
    }

    void FinishGame()
    {
        isGameRunning = false;
        activeHitReactions.Clear();
        StopAllEnemies();
        PlayGameEndBgm();
        timer = 0;
        UpdateTimerText();
        ShowStartRetryButton(retryButtonSprite);

        Debug.Log($"ゲーム終了 / player0: {point_player0} point / player1: {point_player1} point");
    }

    void EnsureBgmAudioSource()
    {
        if (bgmAudioSource == null)
        {
            bgmAudioSource = GetComponent<AudioSource>();
        }
        if (bgmAudioSource == null)
        {
            bgmAudioSource = gameObject.AddComponent<AudioSource>();
        }

        bgmAudioSource.playOnAwake = false;
        bgmAudioSource.spatialBlend = 0f;
        bgmAudioSource.volume = bgmVolume;
    }

    void UpdateBgm()
    {
        if (!isGameRunning || bgmPlaybackState != BgmPlaybackState.GameStart) return;
        if (bgmAudioSource != null && bgmAudioSource.isPlaying) return;

        PlayGamePlayingBgm();
    }

    void PlayGameStartBgm()
    {
        bgmPlaybackState = BgmPlaybackState.GameStart;
        if (gameStartBgm == null)
        {
            PlayGamePlayingBgm();
            return;
        }

        PlayBgmClip(gameStartBgm, false);
    }

    void PlayGamePlayingBgm()
    {
        bgmPlaybackState = BgmPlaybackState.GamePlaying;
        PlayBgmClip(gamePlayingBgm, true);
    }

    void PlayGameEndBgm()
    {
        bgmPlaybackState = BgmPlaybackState.GameEnd;
        PlayBgmClip(gameEndBgm, true);
    }

    void PlayBgmClip(AudioClip clip, bool loop)
    {
        EnsureBgmAudioSource();
        bgmAudioSource.Stop();

        if (clip == null) return;

        bgmAudioSource.clip = clip;
        bgmAudioSource.loop = loop;
        bgmAudioSource.volume = bgmVolume;
        bgmAudioSource.Play();
    }

    void ShowStartRetryButton(Sprite buttonSprite)
    {
        if (startRetryButton != null)
        {
            startRetryButton.gameObject.SetActive(true);
            startRetryButton.interactable = true;
            RegisterStartRetryButtonPointerEvents();
        }

        isStartRetryButtonTransitioning = false;

        if (startRetryButtonImage == null && startRetryButton != null)
        {
            // ボタン本体に非表示のImageがある場合を考慮し、子のイラストを優先する。
            Image[] buttonImages = startRetryButton.GetComponentsInChildren<Image>(true);
            foreach (Image image in buttonImages)
            {
                if (image.gameObject == startRetryButton.gameObject) continue;

                startRetryButtonImage = image;
                break;
            }

            if (startRetryButtonImage == null)
            {
                startRetryButtonImage = startRetryButton.targetGraphic as Image;
            }
            if (startRetryButtonImage == null)
            {
                startRetryButtonImage = startRetryButton.GetComponent<Image>();
            }
        }

        if (startRetryButtonImage != null)
        {
            if (buttonSprite != null)
            {
                startRetryButtonImage.sprite = buttonSprite;
            }
            startRetryButtonImage.enabled = true;

            if (startRetryButton != null)
            {
                startRetryButton.targetGraphic = startRetryButtonImage;
            }
        }

        PlayStartRetryButtonAppearAnimation();
    }

    void PlayStartRetryButtonAppearAnimation()
    {
        if (startRetryButton == null) return;

        CaptureStartRetryButtonBaseScale();
        KillStartRetryButtonTween();

        Transform buttonTransform = startRetryButton.transform;
        buttonTransform.localScale = startRetryButtonBaseScale * 0.75f;

        float targetAlpha = 1f;
        if (startRetryButtonImage != null)
        {
            Color imageColor = startRetryButtonImage.color;
            targetAlpha = imageColor.a > 0f ? imageColor.a : 1f;
            imageColor.a = 0f;
            startRetryButtonImage.color = imageColor;
        }

        Sequence appearSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(buttonTransform);
        appearSequence.Append(
            buttonTransform.DOScale(startRetryButtonBaseScale, buttonAppearDuration)
                .SetEase(Ease.OutBack));

        if (startRetryButtonImage != null)
        {
            appearSequence.Join(
                startRetryButtonImage.DOFade(targetAlpha, buttonAppearDuration * 0.75f)
                    .SetEase(Ease.OutQuad));
        }

        appearSequence.OnComplete(StartStartRetryButtonIdlePulse);
        startRetryButtonTween = appearSequence;
    }

    void RegisterStartRetryButtonPointerEvents()
    {
        if (areStartRetryButtonPointerEventsRegistered || startRetryButton == null) return;

        EventTrigger eventTrigger = startRetryButton.GetComponent<EventTrigger>();
        if (eventTrigger == null)
        {
            eventTrigger = startRetryButton.gameObject.AddComponent<EventTrigger>();
        }
        if (eventTrigger.triggers == null)
        {
            eventTrigger.triggers = new List<EventTrigger.Entry>();
        }

        EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        pointerEnterEntry.callback.AddListener(OnStartRetryButtonPointerEnter);
        eventTrigger.triggers.Add(pointerEnterEntry);

        EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };
        pointerExitEntry.callback.AddListener(OnStartRetryButtonPointerExit);
        eventTrigger.triggers.Add(pointerExitEntry);

        areStartRetryButtonPointerEventsRegistered = true;
    }

    void OnStartRetryButtonPointerEnter(BaseEventData eventData)
    {
        if (isStartRetryButtonTransitioning || startRetryButton == null) return;
        if (!startRetryButton.gameObject.activeInHierarchy) return;
        if (!startRetryButton.interactable) return;

        isPointerOverStartRetryButton = true;
        AnimateStartRetryButtonScale(startRetryButtonBaseScale * buttonHoverScale);
    }

    void OnStartRetryButtonPointerExit(BaseEventData eventData)
    {
        if (isStartRetryButtonTransitioning || startRetryButton == null) return;
        if (!startRetryButton.gameObject.activeInHierarchy) return;

        isPointerOverStartRetryButton = false;
        AnimateStartRetryButtonScale(startRetryButtonBaseScale);
        startRetryButtonTween.OnComplete(StartStartRetryButtonIdlePulse);
    }

    void AnimateStartRetryButtonScale(Vector3 targetScale)
    {
        KillStartRetryButtonTween();

        if (startRetryButtonImage != null)
        {
            Color imageColor = startRetryButtonImage.color;
            imageColor.a = 1f;
            startRetryButtonImage.color = imageColor;
        }

        Transform buttonTransform = startRetryButton.transform;
        startRetryButtonTween = buttonTransform
            .DOScale(targetScale, buttonHoverDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetTarget(buttonTransform);
    }

    void StartStartRetryButtonIdlePulse()
    {
        if (startRetryButton == null || !startRetryButton.gameObject.activeInHierarchy) return;
        if (isStartRetryButtonTransitioning || isPointerOverStartRetryButton) return;

        Transform buttonTransform = startRetryButton.transform;
        buttonTransform.localScale = startRetryButtonBaseScale;
        startRetryButtonTween = buttonTransform
            .DOScale(startRetryButtonBaseScale * buttonIdlePulseScale, buttonIdlePulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .SetTarget(buttonTransform);
    }

    void CaptureStartRetryButtonBaseScale()
    {
        if (hasStartRetryButtonBaseScale || startRetryButton == null) return;

        startRetryButtonBaseScale = startRetryButton.transform.localScale;
        hasStartRetryButtonBaseScale = true;
    }

    void KillStartRetryButtonTween()
    {
        if (startRetryButtonTween == null) return;

        startRetryButtonTween.Kill();
        startRetryButtonTween = null;
    }

    async UniTask PlayHitReaction(Cube enemy)
    {
        if (enemy == null) return;

        // FireLaser側で得点を防ぐが、非同期の競合にも備える。
        if (activeHitReactions.ContainsKey(enemy))
        {
            return;
        }

        int configId = GetNextHitReactionConfigId();
        activeHitReactions[enemy] = new HitReactionState
        {
            configId = configId,
            phase = HitReactionPhase.Rotating,
            resumePosition = enemy.pos,
            resumeCommandSent = false
        };

        enemy.Move(0, 0, 100, Cube.ORDER_TYPE.Strong);
        enemy.TargetMove(
            targetX: -1,
            targetY: -1,
            targetAngle: 360,
            configID: configId,
            timeOut: 3,
            targetMoveType: Cube.TargetMoveType.RotatingMove,
            maxSpd: 80,
            targetSpeedType: Cube.TargetSpeedType.UniformSpeed,
            targetRotationType: Cube.TargetRotationType.RelativeClockwise,
            order: Cube.ORDER_TYPE.Strong
        );
        // 被弾後もデバッグ用の担当色を維持する。
        TurnOnEnemyRoleLed(enemy);
        enemy.PlayPresetSound(2, 255, Cube.ORDER_TYPE.Strong);

        await UniTask.Delay(3500);

        if (activeHitReactions.TryGetValue(enemy, out HitReactionState hitState)
            && hitState.configId == configId
            && hitState.phase == HitReactionPhase.Rotating)
        {
            enemy.Move(0, 0, 100, Cube.ORDER_TYPE.Strong);
            BeginWaitingForMovement(enemy, hitState);
        }
    }

    void SetEnemyRoleLeds()
    {
        TurnOnEnemyRoleLed(enemy0_player0_cube);
        TurnOnEnemyRoleLed(enemy1_player0_cube);
        TurnOnEnemyRoleLed(enemy0_player1_cube);
        TurnOnEnemyRoleLed(enemy1_player1_cube);
    }

    void TurnOnEnemyRoleLed(Cube enemy)
    {
        if (enemy == null) return;

        // durationMs=0で、次のLED命令まで点灯を維持する。
        if (enemy == enemy0_player0_cube || enemy == enemy1_player0_cube)
        {
            enemy.TurnLedOn(0, 0, 255, 0, Cube.ORDER_TYPE.Strong);
        }
        else if (enemy == enemy0_player1_cube || enemy == enemy1_player1_cube)
        {
            enemy.TurnLedOn(255, 0, 0, 0, Cube.ORDER_TYPE.Strong);
        }
    }

    int GetNextHitReactionConfigId()
    {
        int configId = nextHitReactionConfigId;
        nextHitReactionConfigId++;
        if (nextHitReactionConfigId > 199) nextHitReactionConfigId = 100;
        return configId;
    }

    void OnHitRotationCompleted(Cube enemy, int configId, Cube.TargetMoveRespondType response)
    {
        if (!activeHitReactions.TryGetValue(enemy, out HitReactionState hitState)) return;
        if (hitState.configId != configId) return;

        BeginWaitingForMovement(enemy, hitState);
    }

    void BeginWaitingForMovement(Cube enemy, HitReactionState hitState)
    {
        if (hitState.phase == HitReactionPhase.WaitingForMovement) return;

        hitState.phase = HitReactionPhase.WaitingForMovement;
        hitState.resumePosition = enemy.pos;
        hitState.resumeCommandSent = false;
    }

    void StopAllEnemies()
    {
        if (allEnemies == null) return;

        foreach (Cube enemy in allEnemies)
        {
            if (enemy == null) continue;
            enemy.Move(0, 0, 100, Cube.ORDER_TYPE.Strong);
        }
    }

    void OnDestroy()
    {
        KillStartRetryButtonTween();
        if (connectionStatusTextTween != null) connectionStatusTextTween.Kill();
        if (timerUiTween != null) timerUiTween.Kill();
        if (scoreUiTween != null) scoreUiTween.Kill();

        if (player0_cube != null)
        {
            player0_cube.buttonCallback.RemoveListener(Player0ButtonListenerKey);
        }
        if (player1_cube != null)
        {
            player1_cube.buttonCallback.RemoveListener(Player1ButtonListenerKey);
        }
        if (allEnemies == null) return;

        foreach (Cube enemy in allEnemies)
        {
            if (enemy == null) continue;
            enemy.targetMoveCallback.RemoveListener(HitReactionListenerKey);
        }
    }
}

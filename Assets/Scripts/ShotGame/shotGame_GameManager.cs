using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using toio;
using Cysharp.Threading.Tasks;
using TMPro;

public class shotGame_GameManager : MonoBehaviour
{
    const int RequiredCubeCount = 6;
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
    readonly Dictionary<Cube, int> activeHitReactions = new Dictionary<Cube, int>();
    int nextHitReactionConfigId = 100;

    [Header("ポイント")]
    [SerializeField] int point_player0;
    [SerializeField] int point_player1;
    [SerializeField] TMP_Text point_player0_text;
    [SerializeField] TMP_Text point_player1_text;

    [Header("タイマー")]
    [Tooltip("ゲームの残り時間（秒）")]
    [SerializeField, Min(1)] int timer = 60;
    [SerializeField] TMP_Text timer_text;

    [Header("スタート / リトライボタン")]
    [SerializeField] Button startRetryButton;
    [SerializeField] TMP_Text startRetryButtonText;

    int initialTimer;
    float timerAccumulator;
    bool isGameRunning;
    bool cubesReady;

    [Header("レーザー設定")]
    [Tooltip("レーザーの射程（toioマットの座標単位）")]
    [SerializeField] float laserRange = 300f;
    [Tooltip("レーザーの中心線から左右に当たる幅")]
    [SerializeField] float laserHalfWidth = 1f;

    void Awake()
    {
        initialTimer = Mathf.Max(1, timer);
        timer = initialTimer;
        point_player0 = 0;
        point_player1 = 0;
        isGameRunning = false;

        UpdatePointTexts();
        UpdateTimerText();
        ShowStartRetryButton("スタート");
    }

    async void Start()
    {
        collisionManager = GetComponent<collision>();
        if (collisionManager == null)
        {
            collisionManager = gameObject.AddComponent<collision>();
        }

        cm = new CubeManager(connectType);
        Cube[] cubes = await cm.MultiConnect(RequiredCubeCount);

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
        UpdateTimer();

        if (!isGameRunning || cm == null || allEnemies == null) return;

        foreach (Cube enemy in allEnemies)
        {
            if (enemy == null || activeHitReactions.ContainsKey(enemy)) continue;
            if (cm.IsControllable(enemy))
            {
                enemy.Move(100, 70, 200);
            }
        }
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

        PlayHitReaction(hitEnemy, isCorrectTarget).Forget();
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
        if (!cubesReady)
        {
            Debug.LogWarning("Cubeの接続が完了するまで待ってください。");
            return;
        }

        point_player0 = 0;
        point_player1 = 0;
        timer = initialTimer;
        timerAccumulator = 0f;
        isGameRunning = true;

        UpdatePointTexts();
        UpdateTimerText();

        if (startRetryButton != null)
        {
            startRetryButton.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("Start Retry ButtonがInspectorで設定されていません。");
        }

        Debug.Log("ゲームスタート");
    }

    void FinishGame()
    {
        isGameRunning = false;
        timer = 0;
        UpdateTimerText();
        ShowStartRetryButton("リトライ");

        Debug.Log($"ゲーム終了 / player0: {point_player0} point / player1: {point_player1} point");
    }

    void ShowStartRetryButton(string label)
    {
        if (startRetryButton != null)
        {
            startRetryButton.gameObject.SetActive(true);
        }
        if (startRetryButtonText != null)
        {
            startRetryButtonText.text = label;
        }
    }

    async UniTask PlayHitReaction(Cube enemy, bool isCorrectTarget)
    {
        if (enemy == null) return;

        // 既に被弾リアクション中なら、LED色の更新だけ行う。
        if (activeHitReactions.ContainsKey(enemy))
        {
            TurnOnHitLed(enemy, isCorrectTarget);
            return;
        }

        int configId = GetNextHitReactionConfigId();
        activeHitReactions[enemy] = configId;

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
        TurnOnHitLed(enemy, isCorrectTarget);
        enemy.PlayPresetSound(2, 255, Cube.ORDER_TYPE.Strong);

        await UniTask.Delay(3500);

        if (activeHitReactions.TryGetValue(enemy, out int activeConfigId)
            && activeConfigId == configId)
        {
            enemy.Move(0, 0, 100, Cube.ORDER_TYPE.Strong);
            activeHitReactions.Remove(enemy);
        }
    }

    void TurnOnHitLed(Cube enemy, bool isCorrectTarget)
    {
        // 正しい敵なら青、担当外の敵なら赤に点灯する。
        if (isCorrectTarget)
        {
            enemy.TurnLedOn(0, 0, 255, 1500, Cube.ORDER_TYPE.Strong);
        }
        else
        {
            enemy.TurnLedOn(255, 0, 0, 1500, Cube.ORDER_TYPE.Strong);
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
        if (!activeHitReactions.TryGetValue(enemy, out int activeConfigId)) return;
        if (activeConfigId != configId) return;

        activeHitReactions.Remove(enemy);
    }

    void OnDestroy()
    {
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

using UnityEngine;
using toio;
using Cysharp.Threading.Tasks;

public class shotGame_GameManager : MonoBehaviour
{
    const string PlayerButtonListenerKey = "shotGame_PlayerButton";
    const string HitReactionListenerKey = "shotGame_HitReaction";

    [Header("接続方式")]
    public ConnectType connectType;

    [Header("Cube")]
    CubeManager cm;
    Cube player_cube;
    Cube enemy_cube;
    collision collisionManager;
    bool isHitReacting;
    int hitReactionSequence;
    int activeHitReactionConfigId;

    [Header("レーザー設定")]
    [Tooltip("レーザーの射程（toioマットの座標単位）")]
    [SerializeField] float laserRange = 300f;
    [Tooltip("レーザーの中心線から左右に当たる幅")]
    [SerializeField] float laserHalfWidth = 25f;

    async void Start()
    {
        collisionManager = GetComponent<collision>();
        if (collisionManager == null)
        {
            collisionManager = gameObject.AddComponent<collision>();
        }

        cm = new CubeManager(connectType);
        Cube[] cubes = await cm.MultiConnect(2);

        if (cubes == null || cubes.Length < 2)
        {
            Debug.LogError("Cubeの接続に失敗しました。2台のCubeが必要です。");
            return;
        }

        player_cube = cubes[0];
        enemy_cube = cubes[1];
        player_cube.buttonCallback.AddListener(PlayerButtonListenerKey, OnPlayerButtonChanged);
        enemy_cube.targetMoveCallback.AddListener(HitReactionListenerKey, OnHitRotationCompleted);
    }

    void Update()
    {
        if (player_cube == null || enemy_cube == null) return;

        if (!isHitReacting && cm.IsControllable(enemy_cube))
        {
            enemy_cube.Move(50, 35, 200);
        }
    }

    void OnPlayerButtonChanged(Cube cube)
    {
        // buttonCallbackは「押した時」と「離した時」の両方で呼ばれる。
        if (!cube.isPressed) return;

        FireLaser();
    }

    void FireLaser()
    {
        bool isHit = collisionManager.CheckLaserCollision(
            player_cube,
            enemy_cube,
            laserRange,
            laserHalfWidth
        );

        if (isHit)
        {
            Debug.Log("レーザーが敵に当たった");
            PlayHitReaction().Forget();
        }
        else
        {
            Debug.Log("レーザーは外れた");
        }
    }

    async UniTask PlayHitReaction()
    {
        if (isHitReacting || enemy_cube == null) return;

        isHitReacting = true;
        int currentSequence = ++hitReactionSequence;
        activeHitReactionConfigId = 100 + currentSequence % 100;

        // その場で一度停止してから、現在の向きを基準に1回転する。
        enemy_cube.Move(0, 0, 100, Cube.ORDER_TYPE.Strong);
        enemy_cube.TurnLedOn(255, 0, 0, 1500, Cube.ORDER_TYPE.Strong);
        enemy_cube.PlayPresetSound(2, 255, Cube.ORDER_TYPE.Strong);

        // 停止命令が送信されるまで待つ。
        await UniTask.Delay(150);

        if (enemy_cube == null || !enemy_cube.isConnected)
        {
            isHitReacting = false;
            return;
        }

        enemy_cube.TargetMove(
            targetX: -1,
            targetY: -1,
            targetAngle: 360,
            configID: activeHitReactionConfigId,
            timeOut: 3,
            targetMoveType: Cube.TargetMoveType.RotatingMove,
            maxSpd: 80,
            targetSpeedType: Cube.TargetSpeedType.UniformSpeed,
            targetRotationType: Cube.TargetRotationType.RelativeClockwise,
            order: Cube.ORDER_TYPE.Strong
        );

        // 完了通知が来なかった場合のセーフティ。
        await UniTask.Delay(3500);
        if (isHitReacting && currentSequence == hitReactionSequence)
        {
            enemy_cube.Move(0, 0, 100, Cube.ORDER_TYPE.Strong);
            isHitReacting = false;
        }
    }

    void OnHitRotationCompleted(Cube cube, int configId, Cube.TargetMoveRespondType response)
    {
        if (cube != enemy_cube || configId != activeHitReactionConfigId) return;

        // 360度の相対回転が完了したら、通常移動を再開する。
        isHitReacting = false;
    }

    void OnDestroy()
    {
        if (player_cube != null)
        {
            player_cube.buttonCallback.RemoveListener(PlayerButtonListenerKey);
        }
        if (enemy_cube != null)
        {
            enemy_cube.targetMoveCallback.RemoveListener(HitReactionListenerKey);
        }
    }
}

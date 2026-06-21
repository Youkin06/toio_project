using UnityEngine;
using toio;
using Cysharp.Threading.Tasks;

public class oniGokko_GameManager : MonoBehaviour
{
    public static oniGokko_GameManager instance;

    [Header("接続方式")]
    public ConnectType connectType;

    public Cube cube_player;
    public Cube cube_Oni;

    CubeManager cm;
    collision collisionManager;
    bool previousHit;
    public bool isHit;
    public int hitCount = 0;

    void Awake()
    {
        instance = this;
        collisionManager = GetComponent<collision>();
        if(collisionManager == null)
        {
            collisionManager = FindObjectOfType<collision>();
        }
        if(collisionManager == null)
        {
            collisionManager = gameObject.AddComponent<collision>();
        }
    }

    async void Start()
    {
        cm = new CubeManager(connectType);
        Cube[] cubes = await cm.MultiConnect(2);

        if (cubes == null || cubes.Length < 2)
        {
            Debug.LogError("Cubeの接続に失敗しました。2台のCubeが必要です。");
            return;
        }

        cube_player = cubes[0];
        cube_Oni = cubes[1];
    }

    void Update()
    {
        if(collisionManager == null) return;

        isHit = collisionManager.CheckCollision(cube_player, cube_Oni);

        if(isHit && !previousHit)
        {
            Debug.Log("当たった");
            hitCount++;
        }

        previousHit = isHit;
    }
}

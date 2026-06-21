using UnityEngine;
using toio;

public class shotGame_GameManager : MonoBehaviour
{
    [Header("接続方式")]
    public ConnectType connectType;
    [Header("Cube")]
    CubeManager cm;
    Cube player_cube;
    Cube enemy_cube;
    [Header("押された判定")]
    bool once;

    async void Start()
    {
        cm = new CubeManager(connectType);
        Cube[] cubes = await cm.MultiConnect(2);

        if (cubes == null || cubes.Length == 0)
        {
            Debug.LogError("Cubeの接続に失敗しました。");
            return;
        }

        player_cube = cubes[0];
        enemy_cube = cubes[1];
    }

    void Update()
    {
        if(player_cube == null) return;
        if(enemy_cube == null) return;

        if (player_cube.isPressed)
        {
            if (!once)
            {
                once = true;
                Debug.Log("押された");
            }
        }
        else
        {
            once = false;
        }
        
        if(cm.IsControllable(enemy_cube))
        {
            enemy_cube.Move(100,70, 200);
        }

    }
}

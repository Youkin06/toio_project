using UnityEngine;
using toio;

/// <summary>
/// 6台のtoioを接続し、実機のボタン操作で各Cubeの識別情報を確認する。
/// 使用中は、他のCubeManagerを使うスクリプトを無効にすること。
/// </summary>
public class SixCubeIdentifier : MonoBehaviour
{
    const int RequiredCubeCount = 6;

    [Header("接続方式")]
    [SerializeField] ConnectType connectType = ConnectType.Real;

    CubeManager cubeManager;
    Cube[] connectedCubes;

    async void Start()
    {
        Debug.Log("[Cube確認] 6台のtoioを検索します。すべての電源を入れてください。");

        cubeManager = new CubeManager(connectType);
        connectedCubes = await cubeManager.MultiConnect(RequiredCubeCount);

        int connectedCount = connectedCubes == null ? 0 : connectedCubes.Length;
        Debug.Log($"[Cube確認] 接続結果: {connectedCount}/{RequiredCubeCount}台");

        if (connectedCount < RequiredCubeCount)
        {
            Debug.LogWarning(
                $"[Cube確認] {RequiredCubeCount - connectedCount}台不足しています。" +
                "電源と、他のアプリに接続されていないかを確認してください。"
            );
        }

        for (int i = 0; i < connectedCount; i++)
        {
            Cube cube = connectedCubes[i];
            int cubeNumber = i + 1;
            string listenerKey = GetListenerKey(cubeNumber);

            cube.buttonCallback.AddListener(
                listenerKey,
                changedCube => OnCubeButtonChanged(changedCube, cubeNumber)
            );

            Debug.Log(
                $"[Cube確認] {cubeNumber}番: " +
                $"Name={cube.localName}, Address={cube.addr}, Version={cube.version}"
            );
        }

        if (connectedCount > 0)
        {
            Debug.Log("[Cube確認] 識別したいtoioのボタンを押してください。");
        }
    }

    void OnCubeButtonChanged(Cube cube, int cubeNumber)
    {
        // ボタンを離した通知は表示しない。
        if (!cube.isPressed) return;

        Debug.Log(
            $"[Cube確認] ボタンが押されたCube = {cubeNumber}番 / " +
            $"Name={cube.localName} / Address={cube.addr} / " +
            $"Position=({cube.x}, {cube.y}) / Angle={cube.angle}"
        );
    }

    string GetListenerKey(int cubeNumber)
    {
        return $"SixCubeIdentifier_{cubeNumber}";
    }

    void OnDestroy()
    {
        if (connectedCubes == null) return;

        for (int i = 0; i < connectedCubes.Length; i++)
        {
            if (connectedCubes[i] == null) continue;
            connectedCubes[i].buttonCallback.RemoveListener(GetListenerKey(i + 1));
        }
    }
}

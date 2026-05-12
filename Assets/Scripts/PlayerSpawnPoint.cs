using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 挂在 level2 的出生点空物体上
/// 场景加载后自动把玩家移动到此位置
/// </summary>
public class PlayerSpawnPoint : MonoBehaviour {

    [Tooltip("勾选后场景加载时自动把 XR Origin 移动到这里")]
    public bool spawnOnLoad = true;

    void Start () {
        if (!spawnOnLoad) return;

        // 找到 XR Origin
        var traveller = FindObjectOfType<VRPortalTraveller> ();
        if (traveller != null) {
            traveller.transform.position = transform.position;
            traveller.transform.rotation = transform.rotation;
            Debug.Log ($"[SpawnPoint] 玩家已传送到出生点: {transform.position}");
        } else {
            Debug.LogWarning ("[SpawnPoint] 找不到 VRPortalTraveller，请确认 XR Origin 在场景里！");
        }

        // 淡入（从黑色变透明）
        var fader = ScreenFader.Instance;
        if (fader != null) {
            StartCoroutine (fader.Fade (1f, 0f, 0.5f));
        }
    }
}

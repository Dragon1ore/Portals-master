using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 挂在传送门上，触发场景切换
/// 需要在 File > Build Settings 里把 level 和 level2 都加进去
/// </summary>
public class ScenePortal : MonoBehaviour {

    [Header ("场景设置")]
    [Tooltip("要传送到的场景名字，必须和 Build Settings 里的名字一致")]
    public string targetSceneName = "level2";

    [Header ("淡入淡出设置")]
    [Tooltip("黑屏淡入淡出时间（秒）")]
    public float fadeDuration = 0.5f;

    [Header ("传送设置")]
    [Tooltip("玩家进入传送门多少秒后切换场景（防止误触）")]
    public float teleportDelay = 0.2f;

    bool isTeleporting = false;

    void OnTriggerEnter (Collider other) {
        // 检查是否是玩家（XR Origin）
        if (isTeleporting) return;

        var traveller = other.GetComponent<VRPortalTraveller> ();
        if (traveller != null) {
            StartCoroutine (TeleportToScene ());
        }
    }

    IEnumerator TeleportToScene () {
        isTeleporting = true;

        // 等一点时间防止误触
        yield return new WaitForSeconds (teleportDelay);

        // 淡出（变黑）
        yield return StartCoroutine (Fade (0f, 1f));

        // 加载新场景
        SceneManager.LoadScene (targetSceneName);
    }

    IEnumerator Fade (float fromAlpha, float toAlpha) {
        // 找到或创建淡入淡出遮罩
        var fader = ScreenFader.Instance;
        if (fader != null) {
            yield return StartCoroutine (fader.Fade (fromAlpha, toAlpha, fadeDuration));
        } else {
            yield return new WaitForSeconds (fadeDuration);
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 场景切换时的黑屏淡入淡出效果
/// 挂在一个永久存在的 GameObject 上（DontDestroyOnLoad）
/// 需要在场景里创建一个 Canvas > Image 作为遮罩
/// </summary>
public class ScreenFader : MonoBehaviour {

    public static ScreenFader Instance { get; private set; }

    [Tooltip("全屏黑色遮罩 Image，挂在 World Space Canvas 上")]
    public Image fadeImage;

    void Awake () {
        if (Instance != null && Instance != this) {
            Destroy (gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad (gameObject);

        // 初始透明
        if (fadeImage != null) {
            var c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
        }
    }

    public IEnumerator Fade (float fromAlpha, float toAlpha, float duration) {
        if (fadeImage == null) yield break;

        float elapsed = 0f;
        Color c = fadeImage.color;
        c.a = fromAlpha;
        fadeImage.color = c;
        fadeImage.gameObject.SetActive (true);

        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp (fromAlpha, toAlpha, elapsed / duration);
            fadeImage.color = c;
            yield return null;
        }

        c.a = toAlpha;
        fadeImage.color = c;

        if (toAlpha <= 0f) {
            fadeImage.gameObject.SetActive (false);
        }
    }
}

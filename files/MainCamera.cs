using UnityEngine;

/// <summary>
/// VR-adapted MainCamera.cs 适配 XR Device Simulator / XR Interaction Toolkit
/// 
/// 挂载位置：XR Origin > Camera Offset > Main Camera
/// （就是 XR Rig 层级里带 Camera 组件的那个 GameObject）
/// 
/// OnPreCull 会在该摄像机渲染前自动调用，时机正确。
/// </summary>
public class MainCamera : MonoBehaviour {

    Portal[] portals;

    void Awake () {
        portals = FindObjectsOfType<Portal> ();
    }

    void OnPreCull () {
        for (int i = 0; i < portals.Length; i++) {
            portals[i].PrePortalRender ();
        }
        for (int i = 0; i < portals.Length; i++) {
            portals[i].Render ();
        }
        for (int i = 0; i < portals.Length; i++) {
            portals[i].PostPortalRender ();
        }
    }
}

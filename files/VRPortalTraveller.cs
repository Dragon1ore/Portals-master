using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VR Portal Traveller 适配 Unity XR Interaction Toolkit + XR Device Simulator
/// 
/// 挂载位置：XR Origin (XR Rig) 的根 GameObject
/// 
/// 层级结构示例：
/// XR Origin  ← 挂此脚本 + CapsuleCollider + VRPortalTraveller
///   └── Camera Offset
///         └── Main Camera  ← 这是 playerCam
/// </summary>
public class VRPortalTraveller : PortalTraveller {

    [Header ("XR Settings")]
    [Tooltip("XR Origin 根物体，留空则自动用此 GameObject 的 transform")]
    public Transform xrOrigin;

    [Tooltip("XR Rig 下的 Main Camera (Camera Offset > Main Camera)，留空则自动查找")]
    public Transform xrCamera;

    void Awake () {
        if (xrOrigin == null)
            xrOrigin = transform;

        if (xrCamera == null) {
            var cam = GetComponentInChildren<Camera> ();
            if (cam != null)
                xrCamera = cam.transform;
            else
                xrCamera = transform;
        }
    }

    /// <summary>
    /// 传送整个 XR Origin，正确处理 Camera Offset 的偏移。
    /// </summary>
    public override void Teleport (Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot) {
        // 计算摄像机相对于 XR Origin 根的水平偏移
        Vector3 camLocalOffset = xrOrigin.InverseTransformPoint(xrCamera.position);
        camLocalOffset.y = 0; // 只处理水平偏移，不影响高度

        // 计算传送门旋转差
        Quaternion rotDelta = rot * Quaternion.Inverse(xrOrigin.rotation);

        // 旋转偏移
        Vector3 rotatedOffset = rotDelta * camLocalOffset;

        // 移动 XR Origin
        xrOrigin.position = pos - rotatedOffset;
        xrOrigin.rotation = rot;

        // 同步此 transform（供 Portal.cs 侧面判断使用）
        transform.position = xrOrigin.position;
        transform.rotation = xrOrigin.rotation;
    }

    // XR Device Simulator 通常无身体模型，跳过 graphics clone
    public override void EnterPortalThreshold () {
        if (graphicsObject != null)
            base.EnterPortalThreshold ();
    }

    public override void ExitPortalThreshold () {
        if (graphicsObject != null)
            base.ExitPortalThreshold ();
    }

    /// <summary>
    /// 返回头部世界坐标，供外部判断玩家在传送门哪侧。
    /// </summary>
    public Vector3 HeadPosition => xrCamera != null ? xrCamera.position : transform.position;
}

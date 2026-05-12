using UnityEngine;

public class VRPortalTraveller : PortalTraveller
{

    [Header("XR Settings")]
    [Tooltip("XR Origin 根物体，留空则自动用此 GameObject 的 transform")]
    public Transform xrOrigin;

    [Tooltip("XR Rig 下的 Main Camera (Camera Offset > Main Camera)，留空则自动查找")]
    public Transform xrCamera;

    [Header("Portal Settings")]
    [Tooltip("勾选后传送时强制扶正玩家视角（适合地板洞/天花板传送门）")]
    public bool uprightOnTeleport = true;

    void Awake()
    {
        if (xrOrigin == null)
            xrOrigin = transform;

        if (xrCamera == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null)
                xrCamera = cam.transform;
            else
                xrCamera = transform;
        }
    }

    public override void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
    {
        Vector3 camLocalOffset = xrOrigin.InverseTransformPoint(xrCamera.position);
        camLocalOffset.y = 0;
        Quaternion rotDelta = rot * Quaternion.Inverse(xrOrigin.rotation);
        Vector3 rotatedOffset = rotDelta * camLocalOffset;

        xrOrigin.position = pos - rotatedOffset;

        if (uprightOnTeleport)
        {
            // 只保留 Y 轴旋转（水平朝向），去掉 X/Z 倾斜
            float yRotation = rot.eulerAngles.y;
            xrOrigin.rotation = Quaternion.Euler(0, yRotation, 0);
        }
        else
        {
            xrOrigin.rotation = rot;
        }

        transform.position = xrOrigin.position;
        transform.rotation = xrOrigin.rotation;
    }

    public override void EnterPortalThreshold()
    {
        if (graphicsObject != null)
            base.EnterPortalThreshold();
    }

    public override void ExitPortalThreshold()
    {
        if (graphicsObject != null)
            base.ExitPortalThreshold();
    }

    public Vector3 HeadPosition => xrCamera != null ? xrCamera.position : transform.position;
}
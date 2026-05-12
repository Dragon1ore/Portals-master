using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VR-adapted Portal.cs for Meta Quest (Oculus SDK) + Unity 2022.3.
/// 
/// Key changes from original:
/// 1. playerCam now references CenterEyeAnchor camera instead of Camera.main
/// 2. Teleport uses VRPortalTraveller.Teleport which moves the entire OVRCameraRig
/// 3. RenderTexture uses fixed resolution (VR resolution) instead of Screen.width/height
///    because in VR, Screen dimensions don't match actual eye resolution
/// 4. VR near clip plane logic uses fixed FOV fallback since VR uses asymmetric projection
/// </summary>
public class Portal : MonoBehaviour
{
    [Header("Main Settings")]
    public Portal linkedPortal;
    public MeshRenderer screen;
    public int recursionLimit = 5;

    [Header("Advanced Settings")]
    public float nearClipOffset = 0.05f;
    public float nearClipLimit = 0.2f;

    [Header("VR Settings")]
    [Tooltip("Set to match your Quest rendering resolution. Quest 2: 1832x1920, Quest 3: 2064x2208")]
    public int vrRenderWidth = 1832;
    public int vrRenderHeight = 1920;

    [Tooltip("Leave empty — will be found automatically from OVRCameraRig > TrackingSpace > CenterEyeAnchor")]
    public Camera centerEyeCamera;

    // Private variables
    RenderTexture viewTexture;
    Camera portalCam;
    Camera playerCam;
    Material firstRecursionMat;
    List<PortalTraveller> trackedTravellers;
    MeshFilter screenMeshFilter;

    void Awake()
    {
        // VR: Use CenterEyeAnchor camera instead of Camera.main
        // Camera.main in OVR setups often returns the wrong camera
        if (centerEyeCamera != null)
        {
            playerCam = centerEyeCamera;
        }
        else
        {
            // Auto-find: look for CenterEyeAnchor camera in scene
            playerCam = FindCenterEyeCamera();
            if (playerCam == null)
            {
                Debug.LogWarning("[Portal] Could not find CenterEyeAnchor camera. Falling back to Camera.main.");
                playerCam = Camera.main;
            }
        }

        portalCam = GetComponentInChildren<Camera>();
        portalCam.enabled = false;
        trackedTravellers = new List<PortalTraveller>();
        screenMeshFilter = screen.GetComponent<MeshFilter>();
        screen.material.SetInt("displayMask", 1);
    }

    Camera FindCenterEyeCamera()
    {
        // XR Device Simulator: 查找 "Main Camera"（在 XR Origin > Camera Offset > Main Camera）
        // 优先找名字叫 "Main Camera" 且 tag 是 MainCamera 的
        var cameras = FindObjectsOfType<Camera>();
        foreach (var cam in cameras)
        {
            if (cam.CompareTag("MainCamera"))
            {
                return cam;
            }
        }
        // 备用：找名为 CenterEyeAnchor（OVR）或 Main Camera（XRI）
        foreach (var cam in cameras)
        {
            if (cam.gameObject.name == "CenterEyeAnchor" || cam.gameObject.name == "Main Camera")
            {
                return cam;
            }
        }
        return null;
    }

    void LateUpdate()
    {
        HandleTravellers();
    }

    void HandleTravellers()
    {
        for (int i = 0; i < trackedTravellers.Count; i++)
        {
            PortalTraveller traveller = trackedTravellers[i];
            Transform travellerT = traveller.transform;
            var m = linkedPortal.transform.localToWorldMatrix * transform.worldToLocalMatrix * travellerT.localToWorldMatrix;

            Vector3 offsetFromPortal = travellerT.position - transform.position;
            int portalSide = System.Math.Sign(Vector3.Dot(offsetFromPortal, transform.forward));
            int portalSideOld = System.Math.Sign(Vector3.Dot(traveller.previousOffsetFromPortal, transform.forward));

            if (portalSide != portalSideOld)
            {
                var positionOld = travellerT.position;
                var rotOld = travellerT.rotation;
                traveller.Teleport(transform, linkedPortal.transform, m.GetColumn(3), m.rotation);

                // Only update clone if there are graphics (VR player may not have a body)
                if (traveller.graphicsClone != null)
                {
                    traveller.graphicsClone.transform.SetPositionAndRotation(positionOld, rotOld);
                }

                linkedPortal.OnTravellerEnterPortal(traveller);
                trackedTravellers.RemoveAt(i);
                i--;
            }
            else
            {
                if (traveller.graphicsClone != null)
                {
                    traveller.graphicsClone.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
                }
                traveller.previousOffsetFromPortal = offsetFromPortal;
            }
        }
    }

    public void PrePortalRender()
    {
        foreach (var traveller in trackedTravellers)
        {
            UpdateSliceParams(traveller);
        }
    }

    public void Render()
    {
        if (!CameraUtility.VisibleFromCamera(linkedPortal.screen, playerCam))
        {
            return;
        }

        CreateViewTexture();

        var localToWorldMatrix = playerCam.transform.localToWorldMatrix;
        var renderPositions = new Vector3[recursionLimit];
        var renderRotations = new Quaternion[recursionLimit];

        int startIndex = 0;
        portalCam.projectionMatrix = playerCam.projectionMatrix;

        for (int i = 0; i < recursionLimit; i++)
        {
            if (i > 0)
            {
                if (!CameraUtility.BoundsOverlap(screenMeshFilter, linkedPortal.screenMeshFilter, portalCam))
                {
                    break;
                }
            }
            localToWorldMatrix = transform.localToWorldMatrix * linkedPortal.transform.worldToLocalMatrix * localToWorldMatrix;
            int renderOrderIndex = recursionLimit - i - 1;
            renderPositions[renderOrderIndex] = localToWorldMatrix.GetColumn(3);
            renderRotations[renderOrderIndex] = localToWorldMatrix.rotation;

            portalCam.transform.SetPositionAndRotation(renderPositions[renderOrderIndex], renderRotations[renderOrderIndex]);
            startIndex = renderOrderIndex;
        }

        screen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        linkedPortal.screen.material.SetInt("displayMask", 0);

        for (int i = startIndex; i < recursionLimit; i++)
        {
            portalCam.transform.SetPositionAndRotation(renderPositions[i], renderRotations[i]);
            SetNearClipPlane();
            HandleClipping();
            portalCam.Render();

            if (i == startIndex)
            {
                linkedPortal.screen.material.SetInt("displayMask", 1);
            }
        }

        screen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    void HandleClipping()
    {
        const float hideDst = -1000;
        const float showDst = 1000;
        float screenThickness = linkedPortal.ProtectScreenFromClipping(portalCam.transform.position);

        foreach (var traveller in trackedTravellers)
        {
            if (traveller.originalMaterials == null) continue;

            if (SameSideOfPortal(traveller.transform.position, portalCamPos))
            {
                traveller.SetSliceOffsetDst(hideDst, false);
            }
            else
            {
                traveller.SetSliceOffsetDst(showDst, false);
            }

            if (traveller.cloneMaterials != null)
            {
                int cloneSideOfLinkedPortal = -SideOfPortal(traveller.transform.position);
                bool camSameSideAsClone = linkedPortal.SideOfPortal(portalCamPos) == cloneSideOfLinkedPortal;
                if (camSameSideAsClone)
                {
                    traveller.SetSliceOffsetDst(screenThickness, true);
                }
                else
                {
                    traveller.SetSliceOffsetDst(-screenThickness, true);
                }
            }
        }

        foreach (var linkedTraveller in linkedPortal.trackedTravellers)
        {
            if (linkedTraveller.originalMaterials == null) continue;

            bool cloneOnSameSideAsCam = linkedPortal.SideOfPortal(linkedTraveller.graphicsObject != null ? linkedTraveller.graphicsObject.transform.position : linkedTraveller.transform.position) != SideOfPortal(portalCamPos);
            if (cloneOnSameSideAsCam)
            {
                linkedTraveller.SetSliceOffsetDst(hideDst, true);
            }
            else
            {
                linkedTraveller.SetSliceOffsetDst(showDst, true);
            }

            bool camSameSideAsTraveller = linkedPortal.SameSideOfPortal(linkedTraveller.transform.position, portalCamPos);
            if (camSameSideAsTraveller)
            {
                linkedTraveller.SetSliceOffsetDst(screenThickness, false);
            }
            else
            {
                linkedTraveller.SetSliceOffsetDst(-screenThickness, false);
            }
        }
    }

    public void PostPortalRender()
    {
        foreach (var traveller in trackedTravellers)
        {
            UpdateSliceParams(traveller);
        }
        ProtectScreenFromClipping(playerCam.transform.position);
    }

    void CreateViewTexture()
    {
        // VR FIX: Use fixed VR resolution instead of Screen.width/height
        // In VR, Screen.width returns the combined stereo width which is wrong for portal rendering
        if (viewTexture == null || viewTexture.width != vrRenderWidth || viewTexture.height != vrRenderHeight)
        {
            if (viewTexture != null)
            {
                viewTexture.Release();
            }
            viewTexture = new RenderTexture(vrRenderWidth, vrRenderHeight, 0);
            portalCam.targetTexture = viewTexture;
            linkedPortal.screen.material.SetTexture("_MainTex", viewTexture);
        }
    }

    float ProtectScreenFromClipping(Vector3 viewPoint)
    {
        // VR FIX: Use a safe fallback FOV since VR cameras use asymmetric projection matrices
        float fov = playerCam.fieldOfView > 0 ? playerCam.fieldOfView : 90f;
        float halfHeight = playerCam.nearClipPlane * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float halfWidth = halfHeight * playerCam.aspect;
        float dstToNearClipPlaneCorner = new Vector3(halfWidth, halfHeight, playerCam.nearClipPlane).magnitude;
        float screenThickness = dstToNearClipPlaneCorner;

        Transform screenT = screen.transform;
        bool camFacingSameDirAsPortal = Vector3.Dot(transform.forward, transform.position - viewPoint) > 0;
        screenT.localScale = new Vector3(screenT.localScale.x, screenT.localScale.y, screenThickness);
        screenT.localPosition = Vector3.forward * screenThickness * ((camFacingSameDirAsPortal) ? 0.5f : -0.5f);
        return screenThickness;
    }

    void UpdateSliceParams(PortalTraveller traveller)
    {
        if (traveller.originalMaterials == null) return;

        int side = SideOfPortal(traveller.transform.position);
        Vector3 sliceNormal = transform.forward * -side;
        Vector3 cloneSliceNormal = linkedPortal.transform.forward * side;

        Vector3 slicePos = transform.position;
        Vector3 cloneSlicePos = linkedPortal.transform.position;

        float sliceOffsetDst = 0;
        float cloneSliceOffsetDst = 0;
        float screenThickness = screen.transform.localScale.z;

        bool playerSameSideAsTraveller = SameSideOfPortal(playerCam.transform.position, traveller.transform.position);
        if (!playerSameSideAsTraveller)
        {
            sliceOffsetDst = -screenThickness;
        }
        bool playerSameSideAsCloneAppearing = side != linkedPortal.SideOfPortal(playerCam.transform.position);
        if (!playerSameSideAsCloneAppearing)
        {
            cloneSliceOffsetDst = -screenThickness;
        }

        for (int i = 0; i < traveller.originalMaterials.Length; i++)
        {
            traveller.originalMaterials[i].SetVector("sliceCentre", slicePos);
            traveller.originalMaterials[i].SetVector("sliceNormal", sliceNormal);
            traveller.originalMaterials[i].SetFloat("sliceOffsetDst", sliceOffsetDst);

            if (traveller.cloneMaterials != null && i < traveller.cloneMaterials.Length)
            {
                traveller.cloneMaterials[i].SetVector("sliceCentre", cloneSlicePos);
                traveller.cloneMaterials[i].SetVector("sliceNormal", cloneSliceNormal);
                traveller.cloneMaterials[i].SetFloat("sliceOffsetDst", cloneSliceOffsetDst);
            }
        }
    }

    void SetNearClipPlane()
    {
        Transform clipPlane = transform;
        int dot = System.Math.Sign(Vector3.Dot(clipPlane.forward, transform.position - portalCam.transform.position));

        Vector3 camSpacePos = portalCam.worldToCameraMatrix.MultiplyPoint(clipPlane.position);
        Vector3 camSpaceNormal = portalCam.worldToCameraMatrix.MultiplyVector(clipPlane.forward) * dot;
        float camSpaceDst = -Vector3.Dot(camSpacePos, camSpaceNormal) + nearClipOffset;

        if (Mathf.Abs(camSpaceDst) > nearClipLimit)
        {
            Vector4 clipPlaneCameraSpace = new Vector4(camSpaceNormal.x, camSpaceNormal.y, camSpaceNormal.z, camSpaceDst);
            portalCam.projectionMatrix = playerCam.CalculateObliqueMatrix(clipPlaneCameraSpace);
        }
        else
        {
            portalCam.projectionMatrix = playerCam.projectionMatrix;
        }
    }

    void OnTravellerEnterPortal(PortalTraveller traveller)
    {
        if (!trackedTravellers.Contains(traveller))
        {
            traveller.EnterPortalThreshold();
            traveller.previousOffsetFromPortal = traveller.transform.position - transform.position;
            trackedTravellers.Add(traveller);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        var traveller = other.GetComponent<PortalTraveller>();
        if (traveller)
        {
            OnTravellerEnterPortal(traveller);
        }
    }

    void OnTriggerExit(Collider other)
    {
        var traveller = other.GetComponent<PortalTraveller>();
        if (traveller && trackedTravellers.Contains(traveller))
        {
            traveller.ExitPortalThreshold();
            trackedTravellers.Remove(traveller);
        }
    }

    int SideOfPortal(Vector3 pos)
    {
        return System.Math.Sign(Vector3.Dot(pos - transform.position, transform.forward));
    }

    bool SameSideOfPortal(Vector3 posA, Vector3 posB)
    {
        return SideOfPortal(posA) == SideOfPortal(posB);
    }

    Vector3 portalCamPos
    {
        get { return portalCam.transform.position; }
    }

    void OnValidate()
    {
        if (linkedPortal != null)
        {
            linkedPortal.linkedPortal = this;
        }
    }
}

using UnityEngine;

/// <summary>
/// 临时调试脚本，挂在 Forest Portal 上，用来测试碰撞检测是否正常
/// 测试完成后可以删除
/// </summary>
public class PortalDebug : MonoBehaviour {

    void OnTriggerEnter (Collider other) {
        Debug.Log ($"[Portal] OnTriggerEnter: {other.gameObject.name} (Layer: {LayerMask.LayerToName(other.gameObject.layer)})");
    }

    void OnTriggerStay (Collider other) {
        Debug.Log ($"[Portal] OnTriggerStay: {other.gameObject.name}");
    }

    void OnTriggerExit (Collider other) {
        Debug.Log ($"[Portal] OnTriggerExit: {other.gameObject.name}");
    }

    void OnCollisionEnter (Collision other) {
        Debug.Log ($"[Portal] OnCollisionEnter: {other.gameObject.name}");
    }
}

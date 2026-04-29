using UnityEngine;

public class RotateTester : MonoBehaviour
{
    [SerializeField] private Transform _lookTarget;
    private void Update()
    {
        // Y軸回転のみ目標に向ける
        transform.LookAt(new Vector3(_lookTarget.position.x, transform.position.y, _lookTarget.position.z));
    }
}

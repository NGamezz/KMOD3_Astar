using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1;
    [SerializeField] private float yPos = 15;
    [SerializeField] private float zoomSpeed = 2;
    [SerializeField] private float dampTime = 1;
    private Vector3 refVelocity = Vector3.zero;
    private Vector3 targetPos;

    private void Start()
    {
        targetPos = new Vector3(0, yPos, 0);
    }

    private void Update()
    {
        float vert = Input.GetAxis("Vertical");
        float hor = Input.GetAxis("Horizontal");

        if (vert != 0 || hor != 0)
        {
            targetPos += 50.0f * moveSpeed * Time.deltaTime * (Vector3.forward * vert + hor * Vector3.right).normalized;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            yPos += -Mathf.Sign(scroll) * zoomSpeed * Time.deltaTime * 50.0f;
            yPos = Mathf.Clamp(yPos, 1, 100);
            targetPos = new Vector3(targetPos.x, yPos, targetPos.z);
        }

        Vector3 smoothedPosition = Vector3.SmoothDamp(transform.position, targetPos, ref refVelocity, dampTime);
        transform.position = smoothedPosition;
    }
}

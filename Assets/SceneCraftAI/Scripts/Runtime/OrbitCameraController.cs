using UnityEngine;
using UnityEngine.EventSystems;

namespace SceneCraftAI.Runtime
{
    public sealed class OrbitCameraController : MonoBehaviour
    {
        [SerializeField] private Vector3 target = new Vector3(0f, 0.75f, 0f);
        [SerializeField] private float distance = 9.5f;
        [SerializeField] private float yaw = 35f;
        [SerializeField] private float pitch = 31f;
        [SerializeField] private float rotateSpeed = 3.2f;
        [SerializeField] private float zoomSpeed = 1.1f;
        [SerializeField] private float panSpeed = 0.012f;

        private void LateUpdate()
        {
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overUi && Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * rotateSpeed;
                pitch -= Input.GetAxis("Mouse Y") * rotateSpeed;
                pitch = Mathf.Clamp(pitch, 12f, 72f);
            }

            if (!overUi && Input.GetMouseButton(2))
            {
                PanTarget(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            }

            if (!overUi)
            {
                distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * zoomSpeed, 4.5f, 36f);
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = target - rotation * Vector3.forward * distance;
            transform.rotation = rotation;
        }

        private void PanTarget(float mouseX, float mouseY)
        {
            Quaternion viewRotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 horizontal = viewRotation * Vector3.right;
            Vector3 vertical = Vector3.ProjectOnPlane(viewRotation * Vector3.up, Vector3.up);
            if (vertical.sqrMagnitude > 0.0001f) vertical.Normalize();
            float distanceScale = Mathf.Max(4.5f, distance) * panSpeed;
            target -= (horizontal * mouseX + vertical * mouseY) * distanceScale;
            target.y = Mathf.Max(0.25f, target.y);
        }

        public void FrameBounds(Bounds bounds)
        {
            target = new Vector3(bounds.center.x, Mathf.Max(0.75f, bounds.size.y * 0.28f), bounds.center.z);
            distance = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 1.18f + 4f, 7.5f, 32f);
        }
    }
}

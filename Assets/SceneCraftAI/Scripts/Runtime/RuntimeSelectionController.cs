using System;
using SceneCraftAI.Domain;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SceneCraftAI.Runtime
{
    public sealed class RuntimeSelectionController : MonoBehaviour
    {
        private Camera targetCamera;
        private SceneRuntimeController runtimeController;
        private SceneObjectView selected;
        private bool dragging;
        private Vector3 dragOffset;
        private Vector3 dragTarget;

        public event Action<SceneObjectView> SelectionChanged;
        public SceneObjectView Selected { get { return selected; } }

        public void Initialize(Camera cameraToUse, SceneRuntimeController controller)
        {
            targetCamera = cameraToUse;
            runtimeController = controller;
        }

        private void Update()
        {
            if (targetCamera == null) return;
            if (runtimeController != null && runtimeController.IsBusy)
            {
                if (dragging) runtimeController.CancelObjectMove(selected);
                dragging = false;
                return;
            }

            if (Input.GetMouseButtonDown(0)) BeginPointerInteraction();
            if (dragging && Input.GetMouseButton(0)) UpdateDrag();
            if (dragging && Input.GetMouseButtonUp(0)) EndDrag();
        }

        private void BeginPointerInteraction()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            SceneObjectView next = null;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                next = hit.collider.GetComponentInParent<SceneObjectView>();
            }

            Select(next);
            if (next == null || runtimeController == null) return;

            Vector3 point;
            if (!TryGetDragPoint(next, ray, out point)) return;
            dragging = true;
            dragOffset = next.transform.position - point;
            dragTarget = next.transform.position;
            runtimeController.BeginObjectMove(next);
        }

        private void UpdateDrag()
        {
            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            Vector3 point;
            if (!TryGetDragPoint(selected, ray, out point)) return;
            dragTarget = point + dragOffset;
            if (selected.Spec.category == "wall_art")
            {
                RoomSpec room = runtimeController.GetRoom(selected.Spec.roomId);
                dragTarget.x = Mathf.Clamp(dragTarget.x, room.center.x - room.width * 0.5f + selected.Spec.size.x * 0.5f, room.center.x + room.width * 0.5f - selected.Spec.size.x * 0.5f);
                dragTarget.y = Mathf.Clamp(dragTarget.y, selected.Spec.size.y * 0.5f, room.height - selected.Spec.size.y * 0.5f);
                dragTarget.z = selected.Spec.position.z;
            }
            else
            {
                dragTarget.y = selected.Spec.position.y;
            }

            string reason;
            runtimeController.PreviewObjectMove(selected, dragTarget, out reason);
        }

        private void EndDrag()
        {
            dragging = false;
            if (selected != null) runtimeController.CommitObjectMove(selected, dragTarget);
        }

        private bool TryGetDragPoint(SceneObjectView view, Ray ray, out Vector3 point)
        {
            Plane plane;
            if (view.Spec.category == "wall_art")
            {
                plane = new Plane(Vector3.forward, new Vector3(0f, 0f, view.Spec.position.z));
            }
            else
            {
                plane = new Plane(Vector3.up, new Vector3(0f, view.Spec.position.y, 0f));
            }

            float distance;
            if (plane.Raycast(ray, out distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        public void Select(SceneObjectView next)
        {
            if (selected == next) return;
            if (selected != null) selected.SetSelected(false);
            selected = next;
            if (selected != null) selected.SetSelected(true);
            if (SelectionChanged != null) SelectionChanged(selected);
        }

        public void Clear()
        {
            if (dragging && selected != null && runtimeController != null) runtimeController.CancelObjectMove(selected);
            dragging = false;
            Select(null);
        }
    }
}

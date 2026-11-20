using UnityEngine;

namespace VR.CrowbarPuzzle
{
    [DisallowMultipleComponent]
    public sealed class ProjectionAnchorSet : MonoBehaviour
    {
        [SerializeField] private Transform center;
        [SerializeField] private Transform horizontal;
        [SerializeField] private Transform vertical;

        public Transform Center => center;
        public Transform Horizontal => horizontal;
        public Transform Vertical => vertical;
        public bool IsConfigured => center != null && horizontal != null && vertical != null;

        public void Configure(Transform centerAnchor, Transform horizontalAnchor, Transform verticalAnchor)
        {
            center = centerAnchor;
            horizontal = horizontalAnchor;
            vertical = verticalAnchor;
        }

        private void Reset()
        {
            center = transform.Find("Center");
            horizontal = transform.Find("Horizontal");
            vertical = transform.Find("Vertical");
        }
    }
}

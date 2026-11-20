using System;
using UnityEngine;
using UnityEngine.Events;

namespace VR.CrowbarPuzzle
{
    [DisallowMultipleComponent]
    public sealed class CrowbarProjectionPuzzle : MonoBehaviour
    {
        [Serializable]
        public sealed class ProjectionJoint
        {
            [SerializeField] private string label = "Joint";
            [SerializeField] private ProjectionAnchorSet first;
            [SerializeField] private ProjectionAnchorSet second;
            [SerializeField, Range(0f, 1f)] private float score;

            public string Label => label;
            public ProjectionAnchorSet First => first;
            public ProjectionAnchorSet Second => second;
            public float Score => score;

            public void Configure(string jointLabel, ProjectionAnchorSet firstSet, ProjectionAnchorSet secondSet)
            {
                label = jointLabel;
                first = firstSet;
                second = secondSet;
            }

            public void SetScore(float value)
            {
                score = Mathf.Clamp01(value);
            }
        }

        [Serializable]
        public sealed class FloatEvent : UnityEvent<float>
        {
        }

        [Header("Viewpoint")]
        [SerializeField] private Camera hmdCamera;
        [SerializeField] private bool requireAnchorsInsideViewport = true;

        [Header("Projected joints")]
        [SerializeField] private ProjectionJoint firstJoint = new ProjectionJoint();
        [SerializeField] private ProjectionJoint secondJoint = new ProjectionJoint();

        [Header("Scoring")]
        [Tooltip("Average viewport-space distance that produces a score of zero.")]
        [SerializeField, Min(0.001f)] private float zeroScoreViewportDistance = 0.12f;
        [SerializeField, Range(0f, 1f)] private float completionThreshold = 0.97f;
        [SerializeField, Min(0f)] private float requiredHoldSeconds = 1.5f;

        [Header("Completion")]
        [SerializeField] private GameObject[] fragmentObjects = Array.Empty<GameObject>();
        [SerializeField] private GameObject completedCrowbar;
        [SerializeField] private bool showDebugOverlay = true;
        [SerializeField] private FloatEvent progressChanged = new FloatEvent();
        [SerializeField] private UnityEvent completed = new UnityEvent();

        private float holdSeconds;
        private float totalScore;
        private bool isCompleted;

        public float FirstJointScore => firstJoint.Score;
        public float SecondJointScore => secondJoint.Score;
        public float TotalScore => totalScore;
        public float HoldProgress => requiredHoldSeconds <= 0f ? 1f : Mathf.Clamp01(holdSeconds / requiredHoldSeconds);
        public bool IsCompleted => isCompleted;

        public void Configure(
            Camera viewpointCamera,
            ProjectionAnchorSet firstA,
            ProjectionAnchorSet firstB,
            ProjectionAnchorSet secondA,
            ProjectionAnchorSet secondB,
            GameObject[] fragments,
            GameObject completedObject)
        {
            hmdCamera = viewpointCamera;
            firstJoint.Configure("Piece 1 - Piece 2", firstA, firstB);
            secondJoint.Configure("Piece 2 - Piece 3", secondA, secondB);
            fragmentObjects = fragments ?? Array.Empty<GameObject>();
            completedCrowbar = completedObject;
        }

        private void Awake()
        {
            ResolveCamera();
            ResetPuzzle();
        }

        private void Update()
        {
            if (isCompleted || !ResolveCamera())
            {
                return;
            }

            firstJoint.SetScore(EvaluateJoint(hmdCamera, firstJoint.First, firstJoint.Second));
            secondJoint.SetScore(EvaluateJoint(hmdCamera, secondJoint.First, secondJoint.Second));

            totalScore = Mathf.Min(firstJoint.Score, secondJoint.Score);
            progressChanged.Invoke(totalScore);

            if (firstJoint.Score >= completionThreshold && secondJoint.Score >= completionThreshold)
            {
                holdSeconds += Time.deltaTime;
                if (holdSeconds >= requiredHoldSeconds)
                {
                    CompletePuzzle();
                }
            }
            else
            {
                holdSeconds = 0f;
            }
        }

        public void ResetPuzzle()
        {
            isCompleted = false;
            holdSeconds = 0f;
            totalScore = 0f;
            firstJoint.SetScore(0f);
            secondJoint.SetScore(0f);

            foreach (GameObject fragment in fragmentObjects)
            {
                if (fragment != null)
                {
                    fragment.SetActive(true);
                }
            }

            if (completedCrowbar != null)
            {
                completedCrowbar.SetActive(false);
            }
        }

        [ContextMenu("Complete Puzzle")]
        public void CompletePuzzle()
        {
            if (isCompleted)
            {
                return;
            }

            isCompleted = true;
            holdSeconds = requiredHoldSeconds;
            totalScore = 1f;

            foreach (GameObject fragment in fragmentObjects)
            {
                if (fragment != null)
                {
                    fragment.SetActive(false);
                }
            }

            if (completedCrowbar != null)
            {
                completedCrowbar.SetActive(true);
            }

            progressChanged.Invoke(1f);
            completed.Invoke();
            Debug.Log("Crowbar projection puzzle completed.", this);
        }

        private bool ResolveCamera()
        {
            if (hmdCamera == null)
            {
                hmdCamera = Camera.main;
            }

            return hmdCamera != null;
        }

        private float EvaluateJoint(Camera cameraToUse, ProjectionAnchorSet first, ProjectionAnchorSet second)
        {
            if (first == null || second == null || !first.IsConfigured || !second.IsConfigured)
            {
                return 0f;
            }

            Transform[] firstAnchors = { first.Center, first.Horizontal, first.Vertical };
            Transform[] secondAnchors = { second.Center, second.Horizontal, second.Vertical };
            float distanceSum = 0f;

            for (int index = 0; index < firstAnchors.Length; index++)
            {
                Vector3 firstPoint = cameraToUse.WorldToViewportPoint(
                    firstAnchors[index].position,
                    Camera.MonoOrStereoscopicEye.Mono);
                Vector3 secondPoint = cameraToUse.WorldToViewportPoint(
                    secondAnchors[index].position,
                    Camera.MonoOrStereoscopicEye.Mono);

                if (!IsProjectionValid(cameraToUse, firstPoint) || !IsProjectionValid(cameraToUse, secondPoint))
                {
                    return 0f;
                }

                distanceSum += Vector2.Distance(firstPoint, secondPoint);
            }

            float averageDistance = distanceSum / firstAnchors.Length;
            return 1f - Mathf.Clamp01(averageDistance / zeroScoreViewportDistance);
        }

        private bool IsProjectionValid(Camera cameraToUse, Vector3 viewportPoint)
        {
            if (viewportPoint.z <= cameraToUse.nearClipPlane)
            {
                return false;
            }

            if (!requireAnchorsInsideViewport)
            {
                return true;
            }

            return viewportPoint.x >= 0f && viewportPoint.x <= 1f
                && viewportPoint.y >= 0f && viewportPoint.y <= 1f;
        }

        private void OnGUI()
        {
            if (!showDebugOverlay)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(20f, 20f, 330f, 165f), GUI.skin.box);
            GUILayout.Label("Crowbar projection puzzle");
            GUILayout.Label($"Joint 1: {firstJoint.Score:P0}");
            GUILayout.Label($"Joint 2: {secondJoint.Score:P0}");
            GUILayout.Label($"Total (lower joint): {totalScore:P0}");
            GUILayout.Label(isCompleted
                ? "Completed"
                : $"Hold: {HoldProgress:P0} / threshold {completionThreshold:P0}");
            GUILayout.EndArea();
        }

        private void OnDrawGizmosSelected()
        {
            DrawJointGizmos(firstJoint, Color.cyan);
            DrawJointGizmos(secondJoint, Color.yellow);
        }

        private static void DrawJointGizmos(ProjectionJoint joint, Color color)
        {
            if (joint == null || joint.First == null || joint.Second == null
                || !joint.First.IsConfigured || !joint.Second.IsConfigured)
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.DrawLine(joint.First.Center.position, joint.Second.Center.position);
            Gizmos.DrawLine(joint.First.Horizontal.position, joint.Second.Horizontal.position);
            Gizmos.DrawLine(joint.First.Vertical.position, joint.Second.Vertical.position);
        }
    }
}

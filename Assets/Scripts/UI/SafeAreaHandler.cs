using UnityEngine;

namespace Story
{
    /// <summary>
    /// Adjusts RectTransform anchors to match the device screen's safe area.
    /// Handles notches, camera punch-holes, rounded corners, dynamic islands, and Android navigation bars.
    /// Works seamlessly across device orientations and aspect ratios (16:9, 18:9, 20:9, 21:9, 4:3).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeAreaHandler : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea = new Rect(0, 0, 0, 0);
        private Vector2Int _lastScreenSize = new Vector2Int(0, 0);
        private ScreenOrientation _lastOrientation = (ScreenOrientation)(-1);
        private bool _isApplying = false;

        [Header("Axis Constraints")]
        [Tooltip("Apply safe area inset to horizontal sides (left and right)")]
        public bool applyHorizontal = true;

        [Tooltip("Apply safe area inset to vertical sides (top and bottom)")]
        public bool applyVertical = true;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void Start()
        {
            ApplySafeArea();
        }

        private void OnEnable()
        {
            ApplySafeArea();
        }

        private void Update()
        {
            if (_lastSafeArea != Screen.safeArea ||
                _lastScreenSize.x != Screen.width ||
                _lastScreenSize.y != Screen.height ||
                _lastOrientation != Screen.orientation)
            {
                ApplySafeArea();
            }
        }

        /// <summary>
        /// Recomputes and applies the safe area anchor boundaries.
        /// </summary>
        public void ApplySafeArea()
        {
            if (_isApplying) return;
            _isApplying = true;

            try
            {
                if (_rectTransform == null)
                {
                    _rectTransform = GetComponent<RectTransform>();
                }

                if (_rectTransform == null) return;

                Rect safeArea = Screen.safeArea;
                _lastSafeArea = safeArea;
                _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
                _lastOrientation = Screen.orientation;

                if (Screen.width <= 0 || Screen.height <= 0) return;

                Vector2 anchorMin = safeArea.position;
                Vector2 anchorMax = safeArea.position + safeArea.size;

                anchorMin.x /= Screen.width;
                anchorMin.y /= Screen.height;
                anchorMax.x /= Screen.width;
                anchorMax.y /= Screen.height;

                // Clamp in range [0, 1]
                anchorMin.x = Mathf.Clamp01(anchorMin.x);
                anchorMin.y = Mathf.Clamp01(anchorMin.y);
                anchorMax.x = Mathf.Clamp01(anchorMax.x);
                anchorMax.y = Mathf.Clamp01(anchorMax.y);

                if (!applyHorizontal)
                {
                    anchorMin.x = 0f;
                    anchorMax.x = 1f;
                }

                if (!applyVertical)
                {
                    anchorMin.y = 0f;
                    anchorMax.y = 1f;
                }

                _rectTransform.anchorMin = anchorMin;
                _rectTransform.anchorMax = anchorMax;
                _rectTransform.offsetMin = Vector2.zero;
                _rectTransform.offsetMax = Vector2.zero;
            }
            finally
            {
                _isApplying = false;
            }
        }
    }
}

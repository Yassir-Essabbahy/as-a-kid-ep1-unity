using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AsAKid.UI
{
    /// <summary>
    /// Centered interaction circle reticle that smoothly scales up and brightens
    /// when aiming at an interactable target. On touch devices (or mouse click),
    /// tapping the enlarged reticle directly triggers the active interaction.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public class CenterInteractionReticle : MonoBehaviour, IPointerDownHandler
    {
        [Header("Scale Animation")]
        [SerializeField] private float normalScale = 1f;
        [SerializeField] private float interactableScale = 1.85f;
        [SerializeField] private float scaleLerpSpeed = 14f;

        [Header("Visual Feedback")]
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.72f);
        [SerializeField] private Color interactableColor = new Color(1f, 1f, 1f, 1f);

        [Header("Touch Hit Padding (Negative Expands Outward)")]
        [SerializeField] private float extraTouchRadius = 36f;

        private Image _image;
        private RectTransform _rectTransform;
        private bool _isInteractable;
        private Action _onInteractTriggered;
        private float _currentScale = 1f;
        private Color _currentColor;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rectTransform = GetComponent<RectTransform>();
            _currentScale = normalScale;
            _currentColor = normalColor;

            if (_image != null)
            {
                _image.raycastTarget = false;
                _image.raycastPadding = new Vector4(-extraTouchRadius, -extraTouchRadius, -extraTouchRadius, -extraTouchRadius);
                _image.color = normalColor;
            }

            if (_rectTransform != null)
            {
                _rectTransform.localScale = Vector3.one * normalScale;
            }
        }

        private void OnDisable()
        {
            _isInteractable = false;
            _onInteractTriggered = null;
            _currentScale = normalScale;
            _currentColor = normalColor;

            if (_image != null)
            {
                _image.raycastTarget = false;
                _image.color = normalColor;
            }

            if (_rectTransform != null)
            {
                _rectTransform.localScale = Vector3.one * normalScale;
            }
        }

        private void Update()
        {
            float targetScale = _isInteractable ? interactableScale : normalScale;
            Color targetColor = _isInteractable ? interactableColor : normalColor;
            float t = Mathf.Clamp01(Time.unscaledDeltaTime * scaleLerpSpeed);

            _currentScale = Mathf.Lerp(_currentScale, targetScale, t);
            _currentColor = Color.Lerp(_currentColor, targetColor, t);

            if (_rectTransform != null)
            {
                _rectTransform.localScale = new Vector3(_currentScale, _currentScale, 1f);
            }

            if (_image != null)
            {
                _image.color = _currentColor;
            }
        }

        /// <summary>
        /// Updates whether the player is currently aiming at an interactable target.
        /// </summary>
        public void SetInteractable(bool interactable, Action onInteractTriggered = null)
        {
            _isInteractable = interactable;
            _onInteractTriggered = interactable ? onInteractTriggered : null;

            if (_image != null)
            {
                _image.raycastTarget = interactable;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_isInteractable)
                return;

            _onInteractTriggered?.Invoke();
        }
    }
}

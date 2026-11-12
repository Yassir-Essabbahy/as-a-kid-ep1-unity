using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shared visual-only typewriter reveal helper used by all dialogue,
/// monologue, and message UI systems across the entire playthrough.
/// Plays zero typing sound effects and handles LTR + Arabic RTL cleanly.
/// </summary>
public static class DialogueTypewriterUtility
{
    public const float DefaultCharactersPerSecond = 25f;

    /// <summary>
    /// Returns true if the pointer or touch position for this frame falls inside the given button's RectTransform.
    /// Used so raw screen-tap / click polling does not double-fire alongside Unity Button.onClick.
    /// </summary>
    public static bool IsPointerOverButton(Button button)
    {
        if (button == null || !button.gameObject.activeInHierarchy)
            return false;

        var rt = button.transform as RectTransform;
        if (rt == null)
            return false;

        var canvas = button.GetComponentInParent<Canvas>();
        Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera
            : null;

        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(rt, Input.GetTouch(i).position, uiCam))
                    return true;
            }
        }

        if (Input.mousePresent && RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, uiCam))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks keyboard, mouse, and touch advance/skip inputs for this frame.
    /// </summary>
    public static bool IsAdvanceOrSkipInputPressed(Button ignorePointerOverButton = null)
    {
        if (Story.MobilePauseMenu.Instance != null && Story.MobilePauseMenu.Instance.isPaused)
            return false;

        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.E) ||
            ModularFirstPersonController.MobileTouchControls.IsInteractTriggeredThisFrame)
        {
            return true;
        }

        bool pointerDown = Input.GetMouseButtonDown(0) ||
                           (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);

        if (pointerDown && !IsPointerOverButton(ignorePointerOverButton) && !IsPointerOverPauseButton())
        {
            return true;
        }

        return false;
    }

    private static readonly List<RaycastResult> s_UiRaycastResults = new List<RaycastResult>(16);

    /// <summary>
    /// Returns true if the given screen position hits any raycastable UI element.
    /// Reliable across both InputSystemUIInputModule and legacy input (avoids fingerId vs touchId mismatches).
    /// </summary>
    public static bool IsScreenPointOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;

        var ped = new PointerEventData(EventSystem.current) { position = screenPosition };
        s_UiRaycastResults.Clear();
        EventSystem.current.RaycastAll(ped, s_UiRaycastResults);
        return s_UiRaycastResults.Count > 0;
    }

    public static bool IsPointerOverPauseButton()
    {
        var pauseMenu = Story.MobilePauseMenu.Instance;
        if (pauseMenu == null || pauseMenu.pauseButtonObj == null)
            return false;

        var pauseBtn = pauseMenu.pauseButtonObj.GetComponent<Button>();
        return IsPointerOverButton(pauseBtn);
    }

    /// <summary>
    /// Prepares TMP_Text for either LTR or Arabic RTL display without double-reversing RTLTextMeshPro.
    /// </summary>
    public static string PrepareDisplayText(TMP_Text targetText, string rawLine, bool preserveAlignment = false)
    {
        if (string.IsNullOrEmpty(rawLine))
            rawLine = "";

        bool isArabic = LocalizationManager.ContainsArabic(rawLine);
        bool usesRtlComponent = targetText is RTLTMPro.RTLTextMeshPro;

        string displayLine = usesRtlComponent
            ? rawLine
            : (isArabic ? LocalizationManager.FixRTL(rawLine) : rawLine);

        if (targetText != null)
        {
            if (!usesRtlComponent)
            {
                targetText.isRightToLeftText = isArabic;
            }

            if (!preserveAlignment)
            {
                targetText.alignment = isArabic ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
            }
        }

        return displayLine;
    }

    /// <summary>
    /// Visually reveals characters one by one without playing any typing sound.
    /// Guards against same-frame skip input so advancing between lines never skips the next line's typing animation.
    /// </summary>
    public static IEnumerator TypeTextRoutine(
        TMP_Text targetText,
        string rawLine,
        float charactersPerSecond = DefaultCharactersPerSecond,
        bool allowSkip = true,
        Func<bool> externalSkipCheck = null,
        Button ignorePointerOverButton = null,
        bool preserveAlignment = false,
        Action<float> onElapsedSeconds = null,
        Action<bool> onWasSkipped = null,
        Action<int, int> onVisibleCharactersChanged = null)
    {
        if (targetText == null)
        {
            onElapsedSeconds?.Invoke(0f);
            onWasSkipped?.Invoke(false);
            yield break;
        }

        string displayLine = PrepareDisplayText(targetText, rawLine, preserveAlignment);

        if (charactersPerSecond <= 0f || !Application.isPlaying)
        {
            targetText.text = displayLine;
            targetText.maxVisibleCharacters = int.MaxValue;
            targetText.ForceMeshUpdate();
            int instantTotal = targetText.textInfo != null ? targetText.textInfo.characterCount : displayLine.Length;
            onVisibleCharactersChanged?.Invoke(instantTotal, instantTotal);
            onElapsedSeconds?.Invoke(0f);
            onWasSkipped?.Invoke(false);
            yield break;
        }

        // Hide all characters BEFORE assigning text / yielding so the full line never flashes for 1 frame.
        targetText.maxVisibleCharacters = 0;
        targetText.text = displayLine;
        targetText.ForceMeshUpdate();

        int totalChars = (targetText.textInfo != null)
            ? targetText.textInfo.characterCount
            : displayLine.Length;

        onVisibleCharactersChanged?.Invoke(0, totalChars);

        if (totalChars <= 0)
        {
            targetText.maxVisibleCharacters = int.MaxValue;
            onVisibleCharactersChanged?.Invoke(0, 0);
            onElapsedSeconds?.Invoke(0f);
            onWasSkipped?.Invoke(false);
            yield break;
        }

        float elapsed = 0f;
        int visibleCount = 0;
        int startFrame = Time.frameCount;
        bool wasSkipped = false;

        while (visibleCount < totalChars)
        {
            if (allowSkip && Time.frameCount > startFrame)
            {
                bool extSkip = externalSkipCheck != null && externalSkipCheck();
                if (extSkip || IsAdvanceOrSkipInputPressed(ignorePointerOverButton))
                {
                    wasSkipped = true;
                    break;
                }
            }

            elapsed += Time.deltaTime;
            int targetVisible = Mathf.Min(totalChars, Mathf.FloorToInt(elapsed * charactersPerSecond) + 1);
            if (targetVisible > visibleCount)
            {
                visibleCount = targetVisible;
                targetText.maxVisibleCharacters = visibleCount;
                onVisibleCharactersChanged?.Invoke(visibleCount, totalChars);
            }

            yield return null;
        }

        targetText.maxVisibleCharacters = int.MaxValue;
        onVisibleCharactersChanged?.Invoke(totalChars, totalChars);
        onElapsedSeconds?.Invoke(elapsed);
        onWasSkipped?.Invoke(wasSkipped);
    }
}

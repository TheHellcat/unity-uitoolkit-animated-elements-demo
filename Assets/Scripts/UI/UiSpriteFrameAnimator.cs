using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class UiSpriteFrameAnimator : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument = null;
    [SerializeField] private bool autoStartOnLoad = true;

    private Dictionary<int, IEnumerator> _activeCoroutines = new Dictionary<int, IEnumerator>();

    void Start()
    {
        if (autoStartOnLoad)
        {
            StartAnimHooks();
        }
    }

    public void StartAnimHooks()
    {
        IEnumerator newCoroutine;
        List<VisualElement> uiElements;

        // start a coroutine, handling the actual background swapping for the animation,
        // for each UI element marked as "wants to have animation performed"
        uiElements = uiDocument.rootVisualElement.Query(null, "ui-background-frame-anim").ToList();
        foreach (VisualElement element in uiElements)
        {
            newCoroutine = UpdateElement(element);
            _activeCoroutines.Add(_activeCoroutines.Count, newCoroutine);
            StartCoroutine(newCoroutine);
        }
    }

    public void StopAnimHooks()
    {
        foreach (KeyValuePair<int, IEnumerator> coroutine in _activeCoroutines)
        {
            StopCoroutine(coroutine.Value);
        }

        _activeCoroutines.Clear();
    }

    public void Restart()
    {
        // simply start over
        StopAnimHooks();
        StartAnimHooks();
    }

    private void Update()
    {
        // check if the UI still exists
        if ((uiDocument.rootVisualElement == null) && (_activeCoroutines.Count > 0))
        {
            // root element is gone, i.e. no UI anymore, purge our coroutines
            StopAnimHooks();
        }

        // check if a new UI was created and we haven't any hooks on it
        if ((uiDocument.rootVisualElement != null) && (_activeCoroutines.Count == 0))
        {
            // there's a UI, but we're handling no animations - (re)init our coroutines
            Restart();
        }
    }

    /// <summary>
    /// The actual method that "hooks" onto a VisualElement and swaps out the "background-image" property
    /// to achieve an animated element.
    /// Everything is controlled/configured from the UI, using custom USS properties.
    /// </summary>
    /// <param name="element">The element to "hook" on and animate.</param>
    /// <returns></returns>
    IEnumerator UpdateElement(VisualElement element)
    {
        CustomStyleProperty<string> propertyString;
        CustomStyleProperty<int> propertyInt;
        Sprite sprite;
        float frameDuration;
        bool ok;
        string stringValue;
        int intValue;
        int i;
        string s;
        Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();
        int currentFrame = 0;

        yield return null;

        // Yop, a fine prime example of an "endless loop",
        // it will/can be terminated using "StopCoroutine", when needed, as there's plenty of yield returns.
        while (true)
        {
            // load and cache all sprites for animation frames, if we haven't already
            if (sprites.Count == 0)
            {
                propertyString = new CustomStyleProperty<string>("--background-frame-anim-spritepath-template");
                ok = element.customStyle.TryGetValue(propertyString, out stringValue);
                propertyInt = new CustomStyleProperty<int>("--background-frame-anim-spritepath-filenamedigits");
                ok &= element.customStyle.TryGetValue(propertyInt, out intValue);
                if (ok)
                {
                    ok = true;
                    i = 0;
                    while (ok)
                    {
                        // check if another sprite exists, and load it
                        s = GetPaddedStringNumber(i, intValue);
                        s = stringValue.Replace("{n}", s);
                        sprite = (Sprite) AssetDatabase.LoadAssetAtPath(s, typeof(Sprite));
                        ok = sprite != null;
                        if (ok)
                        {
                            // found another sprite, save it
                            sprites.Add(i, sprite);
                            i++;
                        }
                    }
                }
                else
                {
                    // required properties not found, skip this pass
                    yield return null;
                    continue;
                }
            }

            // check if animation is enabled via USS definitions
            propertyInt = new CustomStyleProperty<int>("--background-frame-anim-enabled");
            ok = element.customStyle.TryGetValue(propertyInt, out intValue);
            if (!ok || (intValue != 1))
            {
                // it's either not set at all, or not set to "enabled",
                // so restore "frame 1" (if we we have one loaded/cached)....
                if (sprites.Count >= 1)
                {
                    currentFrame = 0;
                    ChangeElementBackground(element, sprites[currentFrame]);
                }

                // ...and skip this pass
                yield return null;
                continue;
            }

            // see how long to "hold" this frame, based on set desired FPS from the USS parameters
            // default to 60fps
            frameDuration = 1.0f / 60.0f;
            propertyInt = new CustomStyleProperty<int>("--background-frame-anim-fps");
            ok = element.customStyle.TryGetValue(propertyInt, out intValue);
            if (ok)
            {
                frameDuration = 1.0f / (float) intValue;
            }

            // advance frame counter ("where we are" in the current animation)....
            currentFrame++;
            if (currentFrame >= sprites.Count)
            {
                currentFrame = 0;
            }

            // ....and set the proper sprite for this animation frame
            ChangeElementBackground(element, sprites[currentFrame]);

            yield return new WaitForSeconds(frameDuration);
        }
    }

    /// <summary>
    /// Helper method to change (only) the background image sprite of an UI elements style
    /// </summary>
    /// <param name="element">VisualElement to change the background image of.</param>
    /// <param name="sprite">The sprite to set as new background.</param>
    private void ChangeElementBackground(VisualElement element, Sprite sprite)
    {
        StyleBackground elementBackgroundImage;
        Background elementBackgroundImageValue;

        elementBackgroundImage = element.style.backgroundImage;
        elementBackgroundImageValue = elementBackgroundImage.value;
        elementBackgroundImageValue.sprite = sprite;

        elementBackgroundImage.value = elementBackgroundImageValue;
        element.style.backgroundImage = elementBackgroundImage;
    }

    /// <summary>
    /// Simple helper method to more conviently get a properly padded "number string" for asset path generation.
    /// </summary>
    /// <param name="i">The value to convert to the "number as string".</param>
    /// <param name="targetLength">Desired (minimum) length, final string will be prepended with "0"s up to this size.</param>
    /// <returns>Input int value as string, prepended/padded with "0" digits to desired length.</returns>
    private string GetPaddedStringNumber(int i, int targetLength)
    {
        string s;

        s = i.ToString();
        s = s.PadLeft(targetLength, '0');

        return s;
    }
}

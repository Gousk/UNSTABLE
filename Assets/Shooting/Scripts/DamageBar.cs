using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[DisallowMultipleComponent]
public class DamageBar : MonoBehaviour
{
    [Header("Target Binding")]
    public Target target;

    [Header("Build Mode")]
    public bool autoBuildUI = true;
    public RectTransform uiRoot;

    [Header("References (when autoBuildUI = false)")]
    public Canvas canvas;
    public CanvasGroup canvasGroup;
    public Image backImage;
    public Image fillImage;
    public Image chipImage;
    public Image frameImage;
    public Image flashOverlay;
    public Text hpText;

    [Header("Placement")]
    public Vector3 worldOffset = new Vector3(0, 2.0f, 0);
    public bool billboard = true;
    public Camera facingCamera;

    [Header("Scale By Distance")]
    public bool distanceScale = true;
    public float minDistance = 5f;
    public float maxDistance = 40f;
    public float minScale = 0.8f;
    public float maxScale = 1.6f;

    [Header("Visibility")]
    public bool showOnDamage = true;
    public bool showWhileNotFull = true;
    public float idleHideDelay = 1.25f;
    public float fadeIn = 0.12f;
    public float fadeOut = 0.25f;
    public bool visibleOnStart = false;

    [Header("Fill Anim")]
    public float fillLerpSpeed = 10f;

    [Header("Classic Bar Colors")]
    public Color backColor = new Color(0f, 0f, 0f, 0.45f);
    public Color fillColor = new Color(0.1f, 1f, 0.2f, 1f);
    public Color chipColor = new Color(1f, 0.5f, 0.1f, 0.85f);
    public Color frameColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("Gradient (overrides fillColor if set)")]
    public bool useGradient = false;
    public Gradient fillGradient;
    public bool useCriticalColor = true;
    [Range(0f, 1f)] public float criticalThreshold = 0.2f;
    public Color criticalColor = new Color(1f, 0.2f, 0.15f);

    [Header("Damage Chip")]
    public bool useDamageChip = true;
    public float chipLerpSpeed = 4f;
    public float chipDelay = 0.1f;

    [Header("Text")]
    public bool showText = false;
    public enum TextMode { Percent, CurrentOverMax }
    public TextMode textMode = TextMode.Percent;
    public Color textColor = Color.white;
    public int textFontSize = 18;

    [Header("Per-hit Feedback")]
    public bool hitScalePunch = true;
    public Vector3 hitScaleAmount = new Vector3(0.08f, 0.18f, 0f);
    public float hitPunchTime = 0.06f;
    public float hitReturnSpeed = 10f;
    public bool hitColorFlash = true;
    public Color hitFlashColor = Color.white;
    public float hitFlashDuration = 0.12f;
    [Range(0f, 1f)] public float hitFlashIntensity = 0.75f;

    [Header("Layout")]
    public Vector2 barSize = new Vector2(1.75f, 0.175f);

    [Header("Sprites")]
    [Tooltip("If empty, a 1x1 white sprite will be created at runtime.")]
    public Sprite defaultSprite;

    [Header("Init")]
    public bool deferBuildOneFrame = true;

    // runtime
    RectTransform _rootRT;
    Vector3 _baseLocalScale;
    float _targetFill = 1f, _currentFill = 1f;
    float _chipFill = 1f;
    float _lastDamageTime = -999f;
    float _chipStartTime = -1f;
    Coroutine _fadeCo, _flashCo, _punchCo;

    void Awake()
    {
        if (!target) target = GetComponentInParent<Target>();
        if (!facingCamera) facingCamera = Camera.main;
        EnsureDefaultSprite();

        if (deferBuildOneFrame && isActiveAndEnabled)
            StartCoroutine(InitRoutine());
        else
            InitImmediate();
    }

    IEnumerator InitRoutine()
    {
        yield return null;
        if (!this || !gameObject) yield break;
        InitImmediate();
    }

    void EnsureDefaultSprite()
    {
        if (defaultSprite) return;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        defaultSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
    }

    void InitImmediate()
    {
        if (!this || !gameObject) return;
        if (autoBuildUI) SafeAutoBuild();

        _rootRT = uiRoot;
        if (_rootRT) _baseLocalScale = _rootRT.localScale;

        EnsureFilled(fillImage);
        EnsureFilled(chipImage);

        if (backImage) backImage.color = backColor;
        if (fillImage) fillImage.color = useGradient ? fillGradient.Evaluate(1f) : fillColor;
        if (chipImage) chipImage.color = chipColor;
        if (frameImage) frameImage.color = frameColor;

        if (hpText)
        {
            hpText.color = textColor;
            hpText.fontSize = textFontSize;
        }

        SetFillInstant(Health01(), alsoChip: true);

        if (visibleOnStart || (showWhileNotFull && Health01() < 1f))
            SetCanvasAlpha(1f);
        else
            SetCanvasAlpha(0f);

        if (target != null)
        {
            target.onDamaged.AddListener(OnDamaged);
            target.onDeath.AddListener(OnDeath);
        }
    }

    void OnDestroy()
    {
        if (target != null)
        {
            target.onDamaged.RemoveListener(OnDamaged);
            target.onDeath.RemoveListener(OnDeath);
        }
    }

    void LateUpdate()
    {
        if (!target || !_rootRT) return;

        Vector3 pos = target.transform.position + worldOffset;
        _rootRT.position = pos;

        if (billboard && facingCamera)
        {
            Vector3 fwd = (pos - facingCamera.transform.position).normalized;
            if (fwd.sqrMagnitude > 0.0001f)
                _rootRT.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        if (distanceScale && facingCamera)
        {
            float dist = Vector3.Distance(facingCamera.transform.position, pos);
            float t = Mathf.InverseLerp(minDistance, maxDistance, dist);
            float s = Mathf.Lerp(minScale, maxScale, Mathf.SmoothStep(0f, 1f, t));
            _rootRT.localScale = _baseLocalScale * s;
        }

        float hp01 = Health01();
        _targetFill = hp01;

        _currentFill = Mathf.MoveTowards(_currentFill, _targetFill, fillLerpSpeed * Time.deltaTime);
        if (fillImage) fillImage.fillAmount = _currentFill;

        if (useDamageChip && chipImage)
        {
            if (_targetFill < _chipFill)
                if (_chipStartTime < 0f) _chipStartTime = Time.time + chipDelay;

            if (_chipStartTime > 0f && Time.time >= _chipStartTime)
            {
                _chipFill = Mathf.MoveTowards(_chipFill, _targetFill, chipLerpSpeed * Time.deltaTime);
                if (Mathf.Approximately(_chipFill, _targetFill)) _chipStartTime = -1f;
            }
            chipImage.fillAmount = _chipFill;
        }

        if (fillImage)
        {
            if (useGradient)
                fillImage.color = Color.Lerp(fillImage.color, fillGradient.Evaluate(hp01), 10f * Time.deltaTime);
            else
                fillImage.color = fillColor;

            if (useCriticalColor && hp01 <= criticalThreshold)
                fillImage.color = Color.Lerp(fillImage.color, criticalColor, 10f * Time.deltaTime);
        }

        if (showText && hpText)
        {
            if (textMode == TextMode.Percent)
                hpText.text = Mathf.RoundToInt(hp01 * 100f) + "%";
            else
                hpText.text = Mathf.RoundToInt(target.CurrentHealth) + " / " + Mathf.RoundToInt(target.maxHealth);
        }

        if (showOnDamage && canvasGroup)
        {
            bool visible = (Time.time - _lastDamageTime) <= idleHideDelay || (showWhileNotFull && hp01 < 1f);
            float targetA = visible ? 1f : 0f;
            float speed = visible ? (fadeIn > 0 ? 1f / fadeIn : 999f) : (fadeOut > 0 ? 1f / fadeOut : 999f);
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetA, speed * Time.deltaTime);
        }
    }

    void OnDamaged()
    {
        _lastDamageTime = Time.time;
        if (showOnDamage) FadeTo(1f, fadeIn);
        if (useDamageChip) _chipStartTime = -1f;

        if (hitScalePunch && _rootRT)
        {
            if (_punchCo != null) StopCoroutine(_punchCo);
            _punchCo = StartCoroutine(ScalePunch());
        }

        if (hitColorFlash && flashOverlay)
        {
            if (_flashCo != null) StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(FlashOverlay());
        }
    }

    void OnDeath() => FadeTo(0f, fadeOut);

    float Health01()
    {
        if (!target || target.maxHealth <= 0f) return 1f;
        return Mathf.Clamp01(target.CurrentHealth / target.maxHealth);
    }

    void SetFillInstant(float v, bool alsoChip)
    {
        _currentFill = _targetFill = Mathf.Clamp01(v);
        if (fillImage) fillImage.fillAmount = _currentFill;
        if (alsoChip)
        {
            _chipFill = _currentFill;
            if (chipImage) chipImage.fillAmount = _chipFill;
            _chipStartTime = -1f;
        }
    }

    void SetCanvasAlpha(float a)
    {
        if (!canvasGroup && uiRoot)
        {
            canvasGroup = uiRoot.GetComponent<CanvasGroup>();
            if (!canvasGroup) canvasGroup = uiRoot.gameObject.AddComponent<CanvasGroup>();
        }
        if (canvasGroup) canvasGroup.alpha = Mathf.Clamp01(a);
    }

    void FadeTo(float alpha, float duration)
    {
        if (!canvasGroup) { SetCanvasAlpha(alpha); return; }
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeRoutine(alpha, duration));
    }

    IEnumerator FadeRoutine(float a, float d)
    {
        if (!canvasGroup) yield break;
        if (d <= 0f) { canvasGroup.alpha = a; yield break; }
        float start = canvasGroup.alpha, t = 0f;
        while (t < d)
        {
            if (!canvasGroup) yield break;
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / d);
            canvasGroup.alpha = Mathf.Lerp(start, a, u);
            yield return null;
        }
        if (canvasGroup) canvasGroup.alpha = a;
    }

    IEnumerator FlashOverlay()
    {
        if (!flashOverlay) yield break;
        float t = 0f, d = Mathf.Max(0.01f, hitFlashDuration);
        var baseC = flashOverlay.color;
        while (t < d)
        {
            if (!flashOverlay) yield break;
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / d);
            float w = Mathf.Sin(u * Mathf.PI) * hitFlashIntensity;
            flashOverlay.color = new Color(hitFlashColor.r, hitFlashColor.g, hitFlashColor.b, w);
            yield return null;
        }
        if (flashOverlay) flashOverlay.color = new Color(baseC.r, baseC.g, baseC.b, 0f);
    }

    IEnumerator ScalePunch()
    {
        if (!_rootRT) yield break;
        Vector3 start = _rootRT.localScale;
        Vector3 tgt = start + hitScaleAmount;
        float t = 0f;
        float punchT = Mathf.Max(0.01f, hitPunchTime);

        while (t < 1f)
        {
            if (!_rootRT) yield break;
            t += Time.deltaTime / punchT;
            _rootRT.localScale = Vector3.Lerp(start, tgt, t);
            yield return null;
        }

        while (_rootRT && (_rootRT.localScale - start).sqrMagnitude > 0.000001f)
        {
            _rootRT.localScale = Vector3.Lerp(_rootRT.localScale, start, Time.deltaTime * hitReturnSpeed);
            yield return null;
        }
        if (_rootRT) _rootRT.localScale = start;
    }

    void EnsureFilled(Image img)
    {
        if (!img) return;
        if (img.sprite == null) img.sprite = defaultSprite;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    void SafeAutoBuild()
    {
        if (!uiRoot)
        {
            var holder = new GameObject("DamageBar_UI", typeof(RectTransform));
            holder.transform.SetParent(transform, false);
            uiRoot = holder.GetComponent<RectTransform>();
        }

        _rootRT = uiRoot;

        if (!canvas)
        {
            canvas = uiRoot.GetComponent<Canvas>();
            if (!canvas) canvas = uiRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = facingCamera ? facingCamera : Camera.main;
            canvas.sortingOrder = 10;
        }

        var scaler = uiRoot.GetComponent<CanvasScaler>();
        if (!scaler) scaler = uiRoot.gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        if (!canvasGroup)
        {
            canvasGroup = uiRoot.GetComponent<CanvasGroup>();
            if (!canvasGroup) canvasGroup = uiRoot.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = visibleOnStart ? 1f : 0f;
        }

        uiRoot.sizeDelta = barSize;

        backImage = CreateImage("Back", uiRoot, backColor);
        chipImage = CreateImage("Chip", uiRoot, chipColor);
        fillImage = CreateImage("Fill", uiRoot, fillColor);
        frameImage = CreateImage("Frame", uiRoot, frameColor);
        flashOverlay = CreateImage("Flash", uiRoot, new Color(1, 1, 1, 0));

        EnsureFilled(fillImage);
        EnsureFilled(chipImage);

        chipImage.gameObject.SetActive(useDamageChip);

        if (showText && !hpText)
        {
            var go = new GameObject("HPText", typeof(RectTransform));
            go.transform.SetParent(uiRoot, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            hpText = go.AddComponent<Text>();
            hpText.alignment = TextAnchor.MiddleCenter;
            hpText.color = textColor;
            hpText.fontSize = textFontSize;
        }
    }

    Image CreateImage(string name, RectTransform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.sprite = defaultSprite;
        return img;
    }
}

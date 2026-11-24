using UnityEngine;
using TMPro;

public class FloatingTextBlock : MonoBehaviour
{
    [Header("Float")]
    public float floatAmplitude = 4f;
    public float floatSpeed = 0.6f;

    [Header("Rotate")]
    public float rotateAmplitude = 1.2f;
    public float rotateSpeed = 0.45f;

    [Header("Alpha Breath")]
    public bool useAlphaBreath = true;
    [Range(0f, 1f)] public float minAlpha = 0.92f;
    [Range(0f, 1f)] public float maxAlpha = 1f;
    public float alphaSpeed = 0.5f;

    RectTransform rectTransform;
    TMP_Text tmpText;
    CanvasGroup canvasGroup;

    Vector2 baseAnchoredPos;
    float baseRotationZ;
    float phaseOffset;
    float originalTextAlpha = 1f;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        tmpText = GetComponentInChildren<TMP_Text>();
        canvasGroup = GetComponent<CanvasGroup>();

        baseAnchoredPos = rectTransform.anchoredPosition;
        baseRotationZ = rectTransform.localEulerAngles.z;
        phaseOffset = Random.Range(0f, 100f);

        if (tmpText != null)
            originalTextAlpha = tmpText.color.a;
    }

    void OnEnable()
    {
        baseAnchoredPos = rectTransform.anchoredPosition;
        baseRotationZ = rectTransform.localEulerAngles.z;

        if (tmpText != null)
            originalTextAlpha = tmpText.color.a;
    }

    void Update()
    {
        // 正在拖拽时，DragItem 会把 CanvasGroup.blocksRaycasts 设为 false
        // 这时不要再覆盖位置/旋转
        if (canvasGroup != null && canvasGroup.blocksRaycasts == false)
            return;

        // 放进 slot 后也不要继续漂浮
        if (transform.parent != null && transform.parent.name.StartsWith("Slot"))
            return;

        float t = Time.unscaledTime + phaseOffset;

        float yOffset = Mathf.Sin(t * floatSpeed * Mathf.PI * 2f) * floatAmplitude;
        rectTransform.anchoredPosition = new Vector2(baseAnchoredPos.x, baseAnchoredPos.y + yOffset);

        float rot = Mathf.Sin(t * rotateSpeed * Mathf.PI * 2f) * rotateAmplitude;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, baseRotationZ + rot);

        if (tmpText != null)
        {
            Color c = tmpText.color;

            if (useAlphaBreath)
            {
                float a = 0.5f + 0.5f * Mathf.Sin(t * alphaSpeed * Mathf.PI * 2f);
                c.a = Mathf.Lerp(minAlpha, maxAlpha, a) * originalTextAlpha;
            }
            else
            {
                c.a = originalTextAlpha;
            }

            tmpText.color = c;
        }
    }

    public void RefreshBasePose()
    {
        baseAnchoredPos = rectTransform.anchoredPosition;
        baseRotationZ = rectTransform.localEulerAngles.z;
    }
}
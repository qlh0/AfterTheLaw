using UnityEngine;

using UnityEngine.UI;

public class DisplayFadeIn : MonoBehaviour

{

    public Image fadeImage;

    public float fadeDuration = 1.5f;

    float timer = 0f;

    bool isFading = true;

    void Start()

    {

        if (fadeImage == null)

            fadeImage = GetComponent<Image>();

        if (fadeImage != null)

        {

            Color c = fadeImage.color;

            c.a = 1f;

            fadeImage.color = c;

        }

    }

    void Update()

    {

        if (!isFading) return;

        if (fadeImage == null) return;

        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / fadeDuration);

        Color c = fadeImage.color;

        c.a = 1f - t;

        fadeImage.color = c;

        if (t >= 1f)

        {

            isFading = false;

            gameObject.SetActive(false);

        }

    }

}
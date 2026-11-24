using TMPro;
using UnityEngine;

public class AgentVisual_Number : MonoBehaviour
{
    public int agentId;
    public int generation;

    TextMeshPro text3D;

    void Awake()
    {
        text3D = GetComponent<TextMeshPro>();

        if (text3D == null)
            text3D = GetComponentInChildren<TextMeshPro>();

        if (text3D != null)
            AddBody();
    }

    void AddBody()
    {
        // The animated number may be a child of the root. Keep the body anchored
        // to the simulated position while the original effect animates the number.
        AgentVisualEffect effect = GetComponentInParent<AgentVisualEffect>();
        Transform root = effect != null ? effect.transform : transform;
        var body = new GameObject("AgentBody", typeof(SpriteRenderer));
        body.layer = root.gameObject.layer;
        body.transform.SetParent(root, false);

        Renderer label = text3D.GetComponent<Renderer>();
        SpriteRenderer renderer = body.GetComponent<SpriteRenderer>();
        renderer.sprite = GetBodySprite();
        renderer.color = text3D.color;
        renderer.sortingLayerID = label.sortingLayerID;
        renderer.sortingOrder = label.sortingOrder + 1;
        label.sortingOrder = renderer.sortingOrder + 1;

        var shadowObject = new GameObject("AgentShadow", typeof(SpriteRenderer));
        shadowObject.layer = body.layer;
        shadowObject.transform.SetParent(root, false);
        Transform shadowTransform = shadowObject.transform;
        // Match HomeShadow: the shadow extends to the right and up from the feet.
        shadowTransform.localRotation = Quaternion.Euler(0f, 0f, -67f);
        shadowTransform.localScale = new Vector3(0.85f, 0.68f, 1f);
        float bodyHalfHeight = renderer.sprite.bounds.extents.y;
        Vector3 feet = Vector3.down * bodyHalfHeight;
        Vector3 shadowHalfLength = Vector3.up * (bodyHalfHeight * shadowTransform.localScale.y);
        shadowTransform.localPosition = feet + shadowTransform.localRotation * shadowHalfLength;

        SpriteRenderer shadow = shadowObject.GetComponent<SpriteRenderer>();
        shadow.sprite = renderer.sprite;
        shadow.color = new Color(0.44f, 0.45f, 0.42f, 0.5f);
        shadow.sortingLayerID = renderer.sortingLayerID;
        shadow.sortingOrder = renderer.sortingOrder - 1;
    }

    static Sprite bodySprite;

    static Sprite GetBodySprite()
    {
        if (bodySprite != null)
            return bodySprite;

        string[] silhouette =
        {
            "........####......",
            ".......######.....",
            ".......######.....",
            ".......######.....",
            "........####......",
            "........###.......",
            ".......#####......",
            ".....#########....",
            "....###########...",
            "....###########...",
            "...###.#####.###..",
            "...###.#####.###..",
            "...##.#######.##..",
            "....#.#######.#...",
            "......#######.....",
            "......#######.....",
            "......###.###.....",
            "......###.###.....",
            ".....###..###.....",
            ".....###..###.....",
            ".....###...###....",
            "....####...###....",
            "....####...###....",
            "...........####...",
        };

        int width = silhouette[0].Length;
        int height = silhouette.Length;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (silhouette[y][x] == '#')
                    pixels[(height - y - 1) * width + x] = new Color32(255, 255, 255, 255);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        bodySprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 30f, 0, SpriteMeshType.FullRect);
        bodySprite.name = "Agent Silhouette";
        return bodySprite;
    }

    public void SetAgentData(int id, int gen)
    {
        agentId = id;
        generation = gen;

        if (text3D != null)
            text3D.text = $"G{generation}-{agentId}";
    }

    public void SetId(int id)
    {
        SetAgentData(id, 1);
    }

    public TextMeshPro GetText()
    {
        return text3D;
    }
}
using TMPro;
using UnityEngine;

/// <summary>
/// Se puede poner en cualquier objeto del greybox. Documenta qué asset final lo reemplazará
/// y muestra el nombre con un texto TextMeshPro encima, tanto en el editor como en el juego.
/// Si el objeto tiene SpriteRenderer, además le aplica el color fijo del tipo (PlaceholderPalette).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class PlaceholderInfo : MonoBehaviour
{
    const string LabelName = "[PlaceholderLabel]";

    [Header("Información del placeholder")]
    [Tooltip("Nombre del área u objeto (se muestra encima del cubo).")]
    public string areaName = "Placeholder";

    [Tooltip("Descripción del asset final que reemplazará a este placeholder.")]
    [TextArea(2, 5)] public string finalAssetDescription;

    [Tooltip("Tamaño sugerido del asset final, en píxeles (ancho x alto).")]
    public Vector2Int suggestedSizePx = new Vector2Int(32, 32);

    [Tooltip("Integrante responsable de crear el asset final.")]
    public PlaceholderOwner owner = PlaceholderOwner.Integrante1;

    [Header("Apariencia")]
    [Tooltip("Tipo de placeholder. Define el color según la leyenda fija.")]
    public PlaceholderKind kind = PlaceholderKind.Interactive;
    public bool applyKindColor = true;
    public bool showLabel = true;
    [Min(0.5f)] public float labelFontSize = 4f;
    public float labelOffset = 0.15f;

    TextMeshPro label;

    void OnEnable() => ScheduleRefresh();

    void OnValidate()
    {
#if UNITY_EDITOR
        // OnValidate no permite crear objetos: siempre se difiere (también en Play).
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Refresh(); };
#endif
    }

    void OnDisable()
    {
        if (label != null) label.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (label != null)
        {
            if (Application.isPlaying) Destroy(label.gameObject);
            else DestroyImmediate(label.gameObject);
        }
    }

    void ScheduleRefresh()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // En el editor no se pueden crear objetos dentro de OnValidate/OnEnable: se difiere.
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) Refresh(); };
            return;
        }
#endif
        Refresh();
    }

    void Refresh()
    {
        if (applyKindColor && TryGetComponent(out SpriteRenderer sr))
            sr.color = PlaceholderPalette.GetColor(kind);

        EnsureLabel();
        UpdateLabel();
    }

    void EnsureLabel()
    {
        if (label == null)
        {
            var existing = transform.Find(LabelName);
            if (existing != null) label = existing.GetComponent<TextMeshPro>();
        }

        if (label == null)
        {
            var go = new GameObject(LabelName);
            // El texto se crea al vuelo (no se guarda en la escena ni en el prefab).
            go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            go.transform.SetParent(transform, false);
            label = go.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Bottom;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            label.rectTransform.sizeDelta = new Vector2(20f, 2f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.sortingOrder = 1000;
        }
    }

    void UpdateLabel()
    {
        if (label == null) return;

        bool visible = showLabel && enabled && !string.IsNullOrEmpty(areaName);
        if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
        if (!visible) return;

        if (label.text != areaName) label.text = areaName;
        label.fontSize = labelFontSize;

        Vector3 pos = transform.position;
        float top = pos.y + 0.5f;
        if (TryGetComponent(out SpriteRenderer sr)) top = sr.bounds.max.y;
        else if (TryGetComponent(out Collider2D col)) top = col.bounds.max.y;

        var t = label.transform;
        t.position = new Vector3(pos.x, top + labelOffset, pos.z);
        t.rotation = Quaternion.identity;
        var ls = transform.lossyScale;
        t.localScale = new Vector3(1f / Mathf.Max(Mathf.Abs(ls.x), 0.0001f), 1f / Mathf.Max(Mathf.Abs(ls.y), 0.0001f), 1f);
    }

    void LateUpdate() => UpdateLabel();
}

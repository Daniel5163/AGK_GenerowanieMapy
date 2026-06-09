using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RiverScroll : MonoBehaviour
{
    public float scrollSpeed;
    private Material mat;

    void Start()
    {
        var renderer = GetComponent<MeshRenderer>();
        if (renderer != null) mat = renderer.material;
    }

    void Update()
    {
        if (mat != null)
        {
            float offset = Time.time * scrollSpeed;
            mat.SetTextureOffset("_MainTex", new Vector2(0, -offset));
        }
    }
}

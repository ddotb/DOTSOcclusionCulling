using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CullableObject : MonoBehaviour
{
    [SerializeField] private Renderer m_Renderer;
    [SerializeField] private Collider m_Collider;

    private RealtimeCulling m_Culling;

    private int m_Identifier;
    private int m_Index;

    public Renderer Renderer { get => m_Renderer; }
    public Collider Collider { get => m_Collider; }
    public int Identifier { get => m_Identifier; }

    public void Initialise(RealtimeCulling culling, int index)
    {
        m_Culling = culling;
        m_Index = index;

        m_Identifier = m_Collider.GetInstanceID();

        m_Culling.Register(this);
    }

    public void SetRenderState(bool enabled)
    {
        m_Renderer.forceRenderingOff = !enabled;
    }
}

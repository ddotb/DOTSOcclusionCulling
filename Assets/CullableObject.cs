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

    public int Identifier { get => m_Identifier; }

    public void Initialise(RealtimeCulling culling, int index)
    {
        m_Culling = culling;
        m_Index = index;

        m_Identifier = m_Collider.GetInstanceID();
    }

    private void LateUpdate()
    {
        if (!m_Culling.Active)
        {
            return;
        }

        if (m_Culling.ResultsFlags != null && m_Culling.ResultsFlags.Length > 0)
        {
            m_Renderer.forceRenderingOff = !m_Culling.ResultsFlags[m_Index];
        }
    }
}

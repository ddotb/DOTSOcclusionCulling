using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class NativeBSPTree : MonoBehaviour
{
    [SerializeField] private float3 m_WorldExtents;

    private BSPLevel m_LeftLevel;
    private BSPLevel m_RightLevel;

    public void Populate(NativeArray<BSPObject> data, int maxDepth)
    {        
        m_LeftLevel = new BSPLevel();
        m_RightLevel = new BSPLevel();

        m_LeftLevel.Initialise(0, maxDepth, false);
        m_RightLevel.Initialise(0, maxDepth, false);

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i].Position.x <= 0)
            {
                m_LeftLevel.AddObject(data[i]);
            }
            else
            {
                m_RightLevel.AddObject(data[i]);
            }
        }
    }

    private void OnDestroy()
    {
        m_LeftLevel.Destroy();
        m_RightLevel.Destroy();
    }

    [ContextMenu("Log Tree")]
    public void LogTree()
    {
        Debug.Log("Left Level  ---------------");
        m_LeftLevel.LogContents();
        Debug.Log("Right Level ---------------");
        m_RightLevel.LogContents();

        Debug.Break();
    }
}

public class BSPLevel
{
    private BSPLevel m_LeftLevel;
    private BSPLevel m_RightLevel;

    private NativeList<BSPObject> m_Objects;

    private float3 m_Position;
    private float3 m_Extents;

    private bool m_ToggleDimension = false;
    private bool m_IsLeaf = false;
    private int m_CurrentDepth = 0;
    private int m_MaxDepth = 0;

    public bool IsInFrustumRough(float3 cameraPosition, float3 cameraForwards, float cameraFOV)
    {
        return math.dot(m_Position - cameraPosition, cameraForwards) > 1 - (cameraFOV / 180);
    }

    public void Initialise(float3 position, int currentDepth, int maxDepth, bool toggleDimension)
    {
        m_CurrentDepth = currentDepth;
        m_MaxDepth = maxDepth;
        m_ToggleDimension = toggleDimension;

        m_Position = position;

        if (m_CurrentDepth >= m_MaxDepth)
        {
            m_IsLeaf = true;
        }
        else
        {
            //Increment depth
            currentDepth++;

            m_LeftLevel = new BSPLevel();
            m_LeftLevel.Initialise(currentDepth, maxDepth, !toggleDimension);

            m_RightLevel = new BSPLevel();
            m_RightLevel.Initialise(currentDepth, maxDepth, !toggleDimension);
        }

        m_Objects = new NativeList<BSPObject>(Allocator.Persistent);
    }

    public void AddObject(BSPObject newObject)
    {
        if (m_IsLeaf)
        {
            m_Objects.Add(newObject);
        }
        else
        {
            //Use Z coordinate
            if (m_ToggleDimension)
            {
                if (newObject.Position.z <= m_Position.z)
                {
                    m_LeftLevel.AddObject(newObject);
                }
                else
                {
                    m_RightLevel.AddObject(newObject);
                }
            }
            else
            {
                //Use X coordinate
                if (newObject.Position.x <= m_Position.x)
                {
                    m_LeftLevel.AddObject(newObject);
                }
                else
                {
                    m_RightLevel.AddObject(newObject);
                }
            }
        }
    }

    public void Destroy()
    {
        if (m_LeftLevel != null)
        {
            m_LeftLevel.Destroy();
        }

        if (m_RightLevel != null)
        {
            m_RightLevel.Destroy();
        }

        m_Objects.Dispose();
    }

    public void LogContents()
    {
        Debug.Log("Current Depth = " + m_CurrentDepth);

        if (m_IsLeaf)
        {
            for (int i = 0; i < m_Objects.Length; i++)
            {
                Debug.Log(m_Objects[i].ID);
            }
        }
        else
        {
            Debug.Log("Left Level  ---------------");
            m_LeftLevel.LogContents();
            Debug.Log("Right Level ---------------");
            m_RightLevel.LogContents();
        }
    }
}

public struct BSPObject
{
    public int ID;
    public float3 Position;
}

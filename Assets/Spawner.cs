using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [SerializeField] private RealtimeCulling m_Culling;
    [SerializeField] private GameObject m_SpawnPrefab;

    [SerializeField, Range(1, 10)] private float m_Separation;

    [SerializeField, Range(1, 25)] private int m_Height;
    [SerializeField, Range(1, 25)] private int m_Width;
    [SerializeField, Range(1, 25)] private int m_Depth;

    /// <summary>
    /// Start as a Coroutine so that it can be split across multiple frames
    /// </summary>
    /// <returns></returns>
    private IEnumerator Start()
    {
        int index = 0;
        for (int i = 0; i < m_Depth; i++)
        {
            for (int j = 0; j < m_Width; j++)
            {
                for (int k = 0; k < m_Height; k++)
                {
                    Vector3 position = new Vector3(k * m_Separation, j * m_Separation, i * m_Separation);

                    GameObject newObject = Instantiate(m_SpawnPrefab, position, Quaternion.identity);
                    CullableObject cullable = newObject.GetComponent<CullableObject>();
                    Renderer renderer = newObject.GetComponent<Renderer>();
                    Collider collider = newObject.GetComponent<Collider>();

                    cullable.Initialise(m_Culling, index);

                    index++;
                }

                //Done for this frame
                yield return null;
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Spawner : MonoBehaviour
{
    [SerializeField] private RealtimeCulling m_Culling;
    [SerializeField] private GameObject m_SpawnPrefab;

    [SerializeField, Range(1, 100)] private float m_Separation;
    [SerializeField, Range(1, 10)] private float m_ScaleVariation;
    [SerializeField, Range(0, 10)] private float m_PositionVariation;

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
                    Vector3 position = new Vector3(j * m_Separation, k * m_Separation, i * m_Separation);

                    GameObject newObject = Instantiate(m_SpawnPrefab, position, Quaternion.identity);
                    newObject.transform.localScale *= Random.Range(1, m_ScaleVariation);
                    newObject.transform.position += new Vector3(m_Width * Random.value, m_Height * Random.value, m_Depth * Random.value) * m_PositionVariation;

                    CullableObject cullable = newObject.GetComponent<CullableObject>();
                    Renderer renderer = newObject.GetComponent<Renderer>();
                    Collider collider = newObject.GetComponent<Collider>();

                    cullable.Initialise(m_Culling, index);

                    index++;
                }
            }

            //Done for this frame
            yield return null;
        }
    }
}

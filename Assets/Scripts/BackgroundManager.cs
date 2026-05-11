using UnityEngine;

/// <summary>Spawns <see cref="BackgroundObject"/> instances from a prefab.</summary>
public class BackgroundManager : MonoBehaviour
{
    public GameObject backgroundObjectPrefab;
    public int numberOfObjects = 10;

    void Start()
    {
        for (int i = 0; i < numberOfObjects; i++)
        {
            GameObject obj = Instantiate(backgroundObjectPrefab, transform);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localScale = Vector3.one;
            obj.transform.localRotation = Quaternion.identity;
        }
    }
}

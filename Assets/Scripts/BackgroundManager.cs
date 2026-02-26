using UnityEngine;

public class BackgroundManager : MonoBehaviour
{
    public GameObject backgroundObjectPrefab;
    public int numberOfObjects = 10;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        for (int i = 0; i < numberOfObjects; i++)
        {
            GameObject obj = Instantiate(backgroundObjectPrefab, transform);
            obj.transform.localPosition = new Vector3(0, 0, 0);
            obj.transform.localScale = new Vector3(1, 1, 1);
            obj.transform.localRotation = Quaternion.identity;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

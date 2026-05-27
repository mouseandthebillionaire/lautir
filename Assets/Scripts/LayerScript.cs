using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class LayerScript : MonoBehaviour
{
    [Range(0f, 1f)]
    public float sliderValue = 1f;
    
    public Image image;
    public Vector3 targetLocalPosition = Vector3.zero;
    public Vector3 targetLocalScale = new Vector3(12f, 12f, 1f);

    public string imageFolderPath;
    
    private Vector2 randomSpeedDegPerSec = new Vector2(2f, 12f);

    private bool isRotating = false;
    private Vector3 randomStartLocalPosition;
    private Vector3 randomStartLocalScale;
    private Quaternion randomStartLocalRotation;
    private float baseRotateSpeedDegPerSec;
    private float spinAngleDeg;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        image = GetComponent<Image>();


        
        // Layer  Index = object's name
        string layerIndex = this.gameObject.name;
        
        // Load the image from the resources folder
        var spritePath = imageFolderPath + layerIndex;
        var sprite = Resources.Load<Sprite>(spritePath);
        image.sprite = sprite;

        // Set Random Size
        float randomSize = Random.Range(10f, 25f);
        randomStartLocalScale = new Vector3(randomSize, randomSize, 1f);
        image.transform.localScale = randomStartLocalScale;

        // Set Random Position
        float randomX = Random.Range(-500f, 500f);
        float randomY = Random.Range(-400f, 400f);
        randomStartLocalPosition = new Vector3(randomX, randomY, 0f);
        image.transform.localPosition = randomStartLocalPosition;

        // Set Random Rotation
        float randomRotation = Random.Range(0f, 360f);
        randomStartLocalRotation = Quaternion.Euler(0f, 0f, randomRotation);
        image.transform.localRotation = randomStartLocalRotation;


        float speed = Random.Range(randomSpeedDegPerSec.x, randomSpeedDegPerSec.y);
        int direction = Random.value < 0.5f ? -1 : 1; // -1 = clockwise, 1 = counterclockwise (Unity's +Z is CCW)
        
        isRotating = true;
        baseRotateSpeedDegPerSec = direction * speed;
        spinAngleDeg = 0f;
        StartCoroutine(Rotate());

        ApplySlider();
    }

    private IEnumerator Rotate()
    {
        while (isRotating)
        {
            // As sliderValue→1, rotation speed→0 and stops.
            spinAngleDeg += baseRotateSpeedDegPerSec * (1f - sliderValue) * Time.deltaTime;
            yield return null;
        }
    }

    void Update(){
        if(Input.GetKeyDown(KeyCode.N)){
            Normal();
        }

        ApplySlider();
    }
    
    public void Normal(){
        // set rotation to 0
        image.transform.rotation = Quaternion.Euler(0, 0, 0);
        // set size to 100%
        image.transform.localScale = new Vector3(12, 12, 1);
        // set position to 0, 0
        image.transform.localPosition = new Vector3(0, 0, 0);

        isRotating = false;

    }

    // 0 => original randomized pose, 1 => target pose
    public void ApplySlider(){
        if (image == null) return;

        image.transform.localPosition = Vector3.Lerp(randomStartLocalPosition, targetLocalPosition, sliderValue);
        image.transform.localScale = Vector3.Lerp(randomStartLocalScale, targetLocalScale, sliderValue);

        // Keep a slow spin when sliderValue is near 0, but converge to identity as it approaches 1.
        var spinning = randomStartLocalRotation * Quaternion.Euler(0f, 0f, spinAngleDeg);
        image.transform.localRotation = Quaternion.Slerp(spinning, Quaternion.identity, sliderValue);
    }
}

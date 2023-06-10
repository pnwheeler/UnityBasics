using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SliderScript : MonoBehaviour
{
    public Slider slider;
    [SerializeField]
    TextMeshProUGUI display;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        display.SetText("[{0:0} x {1:0}]", slider.value, slider.value);
    }
}

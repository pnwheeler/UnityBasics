using UnityEngine;

public class Graph : MonoBehaviour
{
    // Create reference to prefab Transform
    [SerializeField]
    Transform pointPrefab;

    // Create field for number of objects
    [SerializeField, Range(10, 200)]
    int resolution = 10;

    // Field to select function
    [SerializeField]
    FunctionLibrary.FunctionName function;

    // Field to select transition behavior
    public enum TransitionMode { Cycle, Random }
    [SerializeField]
    TransitionMode transitionMode;

    // Enable function switching
    [SerializeField, Min(0f)]
    float functionDuration = 1f, transitionDuration = 1f;

    // Array of transforms for every point prefab
    Transform[] points;
    // Store duration of function execution
    float duration;
    // State of graph : transitioning or not
    bool transitioning;
    // transition function
    FunctionLibrary.FunctionName transitionFunction;

    // Initialization: Awake() runs before Start()
    private void Awake()
    {
        float step = 2f / resolution;                       // Viewport of [-1, 1]
        var scale = Vector3.one * step;                     // Scale
        points = new Transform[resolution * resolution];    // Array of transforms
        
        // Create & position prefab points along the x axis
        for (int i = 0; i < points.Length; i++)
        {
            Transform point = points[i] = Instantiate(pointPrefab);
            point.localScale = scale;
            point.SetParent(transform, false);
        }
    }
    // Update: Applies function from Function Libaray
    private void Update()
    {
        duration += Time.deltaTime;
        if (transitioning) {
            if (duration >= transitionDuration) {
                duration -= transitionDuration;
                transitioning = false;
            }
        } else if (duration >= functionDuration) {
            duration -= functionDuration;
            transitioning = true;
            transitionFunction = function;
            PickNextFunction();
        }
        if (transitioning) {
            UpdateFunctionTransition();
        } else {
            UpdateFunction();
        }
    }
    private void PickNextFunction()
    {
        function = transitionMode == TransitionMode.Cycle ?
            FunctionLibrary.GetNextFunctionName(function) :
            FunctionLibrary.GetRandomFunctionNameOtherThan(function);
    }
    private void UpdateFunction()
    {
        // Get the function
        FunctionLibrary.Function f = FunctionLibrary.GetFunction(function);
        float time = Time.time;
        float step = 2f / resolution;
        float v = 0.5f * step - 1f;
        for (int i = 0, x = 0, z = 0; i < points.Length; i++, x++)
        {
            if (x == resolution) { x = 0; z++; v = (z + 0.5f) * step - 1f; }
            float u = (x + 0.5f) * step - 1f;
            points[i].localPosition = f(u, v, time);
        }
    }
    private void UpdateFunctionTransition()
    {
        // Set initial and target functions
        FunctionLibrary.Function
            from = FunctionLibrary.GetFunction(transitionFunction),
			to = FunctionLibrary.GetFunction(function);
        float progress = duration / transitionDuration;
        float time = Time.time;
        float step = 2f / resolution;
        float v = 0.5f * step - 1f;
        // Apply tweening
        for (int i = 0, x = 0, z = 0; i < points.Length; i++, x++){
            if (x == resolution) { x = 0; z++; v = (z + 0.5f) * step - 1f; }
            float u = (x + 0.5f) * step - 1f;
            points[i].localPosition = FunctionLibrary.Morph(
                u, v, time, from, to, progress
            );
        }
    }
}

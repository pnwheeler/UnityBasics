using UnityEngine;
using UnityEngine.UI;

public class GPUGraph : MonoBehaviour
{
    // Specify compute shader
    [SerializeField]
    ComputeShader computeShader;
    // Data container for GPU memory
    ComputeBuffer positionsBuffer;
    // ComputeShader: Identifiers stored as static fields
    static readonly int
        positionsId = Shader.PropertyToID("_Positions"),
        resolutionId = Shader.PropertyToID("_Resolution"),
        stepId = Shader.PropertyToID("_Step"),
        timeId = Shader.PropertyToID("_Time"),
        transitionProgressId = Shader.PropertyToID("_TransitionProgress");

    // Specify material
    [SerializeField]
    Material material;

    // Specify mesh
    [SerializeField]
    Mesh mesh;

    // Specify resolution
    const int maxResolution = 1000;
    [SerializeField, Range(10, maxResolution)]
    int resolution = 10;

    // Slider to control resolution
    [SerializeField]
    Slider Slider;
    
    // Field to select function
    [SerializeField]
    FunctionLibrary.FunctionName function;

    // Function reference
    FunctionLibrary.FunctionName transitionFunction;

    //Field to adjust duration of function and specify transition tweening period
    [SerializeField, Min(0f)]
    float functionDuration = 1f, transitionDuration = 1f;

    // Field to select transition behavior
    public enum TransitionMode { Cycle, Random }
    [SerializeField]
    TransitionMode transitionMode;

    [SerializeField]
    Camera Camera;
    // Function state: transitioning or not
    bool transitioning;
    // Elapsed time
    float duration;

    public GameObject target;



    // OnEnable(): invoked each time the component is enabled, right after "Awake()"
    private void OnEnable() {
        // Constructor takes count (i.e. total points) and stride(i.e. size of single entry: 3 floats/position, 4 bytes/float) 
        positionsBuffer = new ComputeBuffer(maxResolution * maxResolution, 3 * 4); // Claim max res space to enable on the fly resolution change
        resolution = (int)Slider.minValue;
    }
    // Update(): Applies function updates
    private void Update() {
        resolution = (int)Slider.value;
        duration += Time.deltaTime;
        //Camera.transform.RotateAround(target.transform.position, Vector3.up, 2f * Time.deltaTime);
        if (transitioning) {
            Camera.transform.RotateAround(target.transform.position, Vector3.up, Time.deltaTime * 72f);
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
        UpdateFunctionOnGPU();
    }
    // OnDisable(): invoked when disabled, and also if Graph is destroyed and right before hot reload
    private void OnDisable() {
        // Release memory 
        positionsBuffer.Release();
        // Destroy reference
        positionsBuffer = null;
    }
    // GPU update
    void UpdateFunctionOnGPU() {
        float step = 2f / resolution;
        // Set properties in compute shader
        computeShader.SetInt(resolutionId, resolution);
        computeShader.SetFloat(stepId, step);
        computeShader.SetFloat(timeId, Time.time);
        if (transitioning) {
            computeShader.SetFloat(
                transitionProgressId,
                Mathf.SmoothStep(0f, 1f, duration / transitionDuration)
            );
        }

        var kernelIndex =
            (int)function +
            (int)(transitioning ? transitionFunction : function) *
            FunctionLibrary.FunctionCount;
        computeShader.SetBuffer(kernelIndex, positionsId, positionsBuffer);
        int groups = Mathf.CeilToInt(resolution / 8f);
        computeShader.Dispatch(kernelIndex, groups, groups, 1);
        material.SetBuffer(positionsId, positionsBuffer);
        material.SetFloat(stepId, step);
        var bounds = new Bounds(Vector3.zero, Vector3.one * (2f + 2f / resolution));
        // Procedural draw call -- uses current res*res instead of buffer element count
        Graphics.DrawMeshInstancedProcedural(
            mesh, 0, material, bounds, resolution * resolution
        );
    }
    // Chooses next function
    private void PickNextFunction() {
        function = transitionMode == TransitionMode.Cycle ?
            FunctionLibrary.GetNextFunctionName(function) :
            FunctionLibrary.GetRandomFunctionNameOtherThan(function);
    }

}

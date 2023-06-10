using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;
using quaternion = Unity.Mathematics.quaternion;
using Random = UnityEngine.Random;

public class Fractal : MonoBehaviour {
    // Burst Compiler Settings
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    // Define [I]nterface [J]ob that runs inside [For] loops
    struct UpdateFractalLevelJob : IJobFor {
        public float scale;
        public float deltaTime;

        // Read only -- ensure data remains constant to avoid race conditions
        [ReadOnly]  
        public NativeArray<FractalPart> parents;
        public NativeArray<FractalPart> parts;

        // Partial access -- write only
        [WriteOnly]
        public NativeArray<float3x4> matrices;
        public void Execute(int i) {
            FractalPart parent = parents[i / 5];
            FractalPart part = parts[i];
            part.spinAngle += part.spinVelocity * deltaTime;
            
            // Placement: Sag
            float3 upAxis = mul(mul(parent.worldRotation, part.rotation), up());
            float3 sagAxis = cross(up(), upAxis);
            float sagMagnitude = length(sagAxis);
            quaternion baseRotation;
            if (sagMagnitude > 0f) {
                sagAxis /= sagMagnitude;
                quaternion sagRotation =
                    quaternion.AxisAngle(sagAxis, part.maxSagAngle * sagMagnitude); ;
                baseRotation = mul(sagRotation, parent.worldRotation);
            } else {
                baseRotation = parent.worldRotation;
            }
            part.worldRotation = mul(baseRotation,
                mul(part.rotation, quaternion.RotateY(part.spinAngle))
            );

            // Placement: Position
            part.worldPosition =
                 parent.worldPosition +
                 mul(part.worldRotation, float3(0f, 1.5f * scale, 0f));
            parts[i] = part;

            float3x3 r = float3x3(part.worldRotation) * scale;
            matrices[i] = float3x4(r.c0, r.c1, r.c2, part.worldPosition);
        }
    }
    struct FractalPart {
        public float3 worldPosition;                        // World position
        public quaternion rotation, worldRotation;          // Local and world rotations
        public float maxSagAngle, spinAngle, spinVelocity;  // Fresh quaternions each update to avoid precision errors
    }
   
    // NativeArray to hold per-level FractalPart(s)
    NativeArray<FractalPart>[] parts;
    // NativeArray to hold corresponding transform matricies
    NativeArray<float3x4>[] matrices;

    // Register shader properties
    static readonly int
        colorAId = Shader.PropertyToID("_ColorA"),
        colorBId = Shader.PropertyToID("_ColorB"),
        matricesId = Shader.PropertyToID("_Matrices"),
        sequenceNumbersId = Shader.PropertyToID("_SequenceNumbers");

    // ======== Field Configurations =========
    // Depth
    [SerializeField, Range(3, 8)]
    int depth = 4;
    // Meshes
    [SerializeField]
    Mesh mesh, leafMesh;
    // Material
    [SerializeField]
    Material material;
    // Gradients
    [SerializeField]
    Gradient gradientA, gradientB;
    // Leaf color
    [SerializeField]
    Color leafColorA, leafColorB;
    // Sag angle
    [SerializeField, Range(0f, 90f)]
    float maxSagAngleA = 15f, maxSagAngleB = 25f;
    // Spin velocity
    [SerializeField, Range(0f, 90f)]
    float spinSpeedA = 20f, spinSpeedB = 25f;
    // Reverse Spin
    [SerializeField, Range(0f, 1f)]
    float reverseSpinChance = 0.5f;

    // Define set of rotations
    static quaternion[] rotations = {
        quaternion.identity,
        quaternion.RotateZ(-0.5f * PI), quaternion.RotateZ(0.5f * PI),
        quaternion.RotateX(0.5f * PI), quaternion.RotateX(-0.5f * PI)
    };

    // Send matrices to GPU -- CPU responsible for filling per-level buffers
    ComputeBuffer[] matricesBuffers;
    
    // Link each buffer to specific draw command
    static MaterialPropertyBlock propertyBlock;

    // Returns new FractalPart at index
    FractalPart CreatePart(int childIndex) => new FractalPart {
        maxSagAngle = radians(Random.Range(maxSagAngleA, maxSagAngleB)),
        rotation = rotations[childIndex],
        spinVelocity =
            (Random.value < reverseSpinChance ? -1f : 1f) *
            radians(Random.Range(spinSpeedA, spinSpeedB))
    };

    Vector4[] sequenceNumbers;
    // Initialization (Awake()->OnEnable() to support hot reloads)
    private void OnEnable() {
        // Allocate container space
        parts = new NativeArray<FractalPart>[depth];
        matrices = new NativeArray<float3x4>[depth];
        matricesBuffers = new ComputeBuffer[depth];
        sequenceNumbers = new Vector4[depth];
        int stride = 12 * 4;
        for (int i = 0, length = 1; i < parts.Length; i++, length *= 5) {
            parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent);
            matrices[i] = new NativeArray<float3x4>(length, Allocator.Persistent);
            matricesBuffers[i] = new ComputeBuffer(length, stride);
            sequenceNumbers[i] = new Vector4(Random.value, Random.value, Random.value, Random.value);
        }
        // Create new FractalPart(s)
        parts[0][0] = CreatePart(0);
        for (int li = 1; li < parts.Length; li++) {
            NativeArray<FractalPart> levelParts = parts[li];
            for (int fpi = 0; fpi < levelParts.Length; fpi += 5) {
                for (int ci = 0; ci < 5; ci++) {
                    levelParts[fpi + ci] = CreatePart(ci);
                }
            }
        }
        // Assign property block if current value is null (coalescing assignment shorthand)
        propertyBlock ??= new MaterialPropertyBlock();
    }

    // Release allocations and destroy references
    private void OnDisable() {
        for(int i = 0; i < matricesBuffers.Length; i++) {
            matricesBuffers[i].Release();
            parts[i].Dispose();
            matrices[i].Dispose();
        }
        matricesBuffers = null;
        parts = null;
        matrices = null;
        sequenceNumbers = null;
    }

    // Invoked in play mode when active and enabled, or when component is disabled
    private void OnValidate() {
        // Refresh memory
        if (parts != null && enabled) {    // Verify fractal is active & enabled
            OnDisable();    
            OnEnable();
        }
    }
    
    // Called per-frame
    private void Update() {
        // Initialization
        float deltaTime = Time.deltaTime;
        FractalPart rootPart = parts[0][0];
        rootPart.spinAngle += rootPart.spinVelocity * deltaTime;
        rootPart.worldRotation = mul(transform.rotation,
             mul(rootPart.rotation, quaternion.RotateY(rootPart.spinAngle))
         );
        rootPart.worldPosition = transform.position;
        parts[0][0] = rootPart;
        float objectScale = transform.lossyScale.x;
        float3x3 r = float3x3(rootPart.worldRotation) * objectScale;
        matrices[0][0] = float3x4(r.c0, r.c1, r.c2, rootPart.worldPosition);
        float scale = objectScale;

        // Configure job
        JobHandle jobHandle = default;
        for (int li = 1; li < parts.Length; li++) {
            scale *= 0.5f;
            jobHandle = new UpdateFractalLevelJob
            {
                deltaTime = deltaTime,
                scale = scale,
                parents = parts[li - 1],
                parts = parts[li],
                matrices = matrices[li]
            }.ScheduleParallel(parts[li].Length, 5, jobHandle);
        }
        jobHandle.Complete();
        
        // Send matrices to GPU
        var bounds = new Bounds(rootPart.worldPosition, 3f * objectScale * Vector3.one);
        int leafIndex = matricesBuffers.Length - 1;
        for (int i = 0; i < matricesBuffers.Length; i++) {
            ComputeBuffer buffer = matricesBuffers[i];
            buffer.SetData(matrices[i]);
            // Configure property block
            Color colorA, colorB;
            Mesh instanceMesh;
            if (i == leafIndex) {
                colorA = leafColorA;
                colorB = leafColorB;
                instanceMesh = leafMesh;
            } else {
                float gradientInterpolator = i / (matricesBuffers.Length - 2f);
                colorA = gradientA.Evaluate(gradientInterpolator);
                colorB = gradientB.Evaluate(gradientInterpolator);
                instanceMesh = mesh;
            }
            propertyBlock.SetColor(colorAId, colorA);
            propertyBlock.SetColor(colorBId, colorB);
            propertyBlock.SetBuffer(matricesId, buffer);
            propertyBlock.SetVector(sequenceNumbersId, sequenceNumbers[i]);
            Graphics.DrawMeshInstancedProcedural(
                instanceMesh, 0, material, bounds, buffer.count, propertyBlock
            );
        }
    }
}

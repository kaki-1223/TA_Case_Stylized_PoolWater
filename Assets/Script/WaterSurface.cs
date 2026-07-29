using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(Collider))]

public class WaterSurface : MonoBehaviour
{
    [Header("Interaction Height Texture")]
    [SerializeField] private int textureSize = 512;
    [SerializeField] private string interactionHeightTexName = "_InteractionHeightTex";

    [Header("Ripple Inject")]
    [SerializeField] private Material rippleInjectMaterial;
    [SerializeField] private Camera raycastCamera;
    [SerializeField] private float rippleRadius = 0.035f;
    [SerializeField] private float rippleStrength = 0.5f;

    [Header("Wave Stimulation")]
    [SerializeField] private Material WaveUpdateMaterial;
    [SerializeField] private float waveDamping = 0.985f;
    [SerializeField] private float waveSpeed = 0.25f;

    [Header("Interaction Foam Texture")]
    [SerializeField] private string interactionFoamTexName = "_InteractionFoamTex";
    [SerializeField] private Material foamInjectMaterial;
    [SerializeField] private Material foamDecayMaterial;
    [SerializeField] private float foamDecay = 0.985f;

    [Header("Mouse Foam Debug")]
    [SerializeField] private float mouseFoamRadius = 0.06f;
    [SerializeField] private float mouseFoamStrength = 1.0f;

    private RenderTexture foamRT;
    private RenderTexture foamTempRT;


    //private RenderTexture interactionHeightRT;
    private RenderTexture currentHeightRT;
    private RenderTexture previousHeightRT;
    private RenderTexture tempRT;
    private Material waterMaterial;

    public RenderTexture InterationHeightRT => currentHeightRT;

    // private MeshFilter meshFilter;
    // private Bounds localBounds;

    private Collider waterCollider;

    public float WaterY
    {
        get
        {
            if (waterCollider != null)
                return waterCollider.bounds.max.y;

            return transform.position.y;
        }
    }

    //用于水中漂浮物体
    public Bounds WaterBounds => waterCollider != null
    ? waterCollider.bounds
    : new Bounds(transform.position, Vector3.one * 10f);




    private void Awake()
    {
        Renderer renderer = GetComponent<Renderer>();

        // 使用 renderer.material 会创建当前物体自己的材质实例，
        // 避免直接改到 Project 里的材质资源。
        waterMaterial = renderer.material;

        // meshFilter = GetComponent<MeshFilter>();

        // if (meshFilter != null && meshFilter.sharedMesh != null)
        // {
        //     localBounds = meshFilter.sharedMesh.bounds;
        // }

        waterCollider = GetComponent<Collider>();



        if (raycastCamera == null)
        {
            Debug.LogError("Raycast Camera is not assigned.");
            raycastCamera = Camera.main;
        }

        CreateInterationRT();
        CreateFoamRTs();
        AssignTextureToMaterial();
    }

    private void Update()
    {
        UpdateWaveSimulation();
        UpdateFoamDecay();
        if (Input.GetMouseButtonDown(0))
        {
            TryInjectRippleFromMouse();
        }


    }


    // 新增这个方法
    private float GetPlaneAspectRatio()
    {
        if (waterCollider == null) return 1f;
        Bounds b = waterCollider.bounds;
        float width = b.size.x;
        float depth = b.size.z;
        if (depth < 0.0001f) return 1f;
        return width / depth;
    }


    private void TryInjectRippleFromMouse()
    {
        if (raycastCamera == null)
        {
            Debug.LogError("Raycast Camera is not assigned.");
            return;
        }
        Ray ray = raycastCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.gameObject != this.gameObject)
            {
                return;
            }

            Vector2 uv = hit.textureCoord;
            InjectRipple(uv, rippleRadius, rippleStrength);
            InjectFoam(uv, mouseFoamRadius, mouseFoamStrength);

        }
    }

    public void InjectRipple(Vector2 uv, float radius, float strength)
    {
        if (rippleInjectMaterial == null || currentHeightRT == null)
        {
            Debug.LogError("Ripple Inject Material is not assigned.");
            return;
        }

        //rippleInjectMaterial.SetTexture("_MainTex",interactionHeightRT);
        rippleInjectMaterial.SetVector("_Center", new Vector4(uv.x, uv.y, 0, 0));
        rippleInjectMaterial.SetFloat("_Radius", radius);
        rippleInjectMaterial.SetFloat("_Strength", strength);
        rippleInjectMaterial.SetFloat("_AspectRatio", GetPlaneAspectRatio());

        RenderTexture tempRT = RenderTexture.GetTemporary(currentHeightRT.descriptor);
        Graphics.Blit(currentHeightRT, tempRT, rippleInjectMaterial);
        Graphics.Blit(tempRT, currentHeightRT);
        RenderTexture.ReleaseTemporary(tempRT);
    }




    // public bool InjectRippleWorld(Vector3 worldPosition, float radius, float strength)
    // {
    //     if (waterCollider == null)
    //         return false;

    //     // 从水面上方往下投射，避免物体本身高度影响 UV
    //     Vector3 origin = worldPosition + transform.up * 10f;
    //     Vector3 direction = -transform.up;

    //     Ray ray = new Ray(origin, direction);

    //     if (waterCollider.Raycast(ray, out RaycastHit hit, 20f))
    //     {
    //         Vector2 uv = hit.textureCoord;
    //         InjectRipple(uv, radius, strength);
    //         return true;
    //     }

    //     return false;
    // }

    public bool InjectRippleAndFoamWorld(
       Vector3 worldPosition,
       float rippleRadius,
       float rippleStrength,
       float foamRadius,
       float foamStrength)
    {
        if (waterCollider == null)
            return false;

        Bounds b = waterCollider.bounds;

        Vector3 origin = new Vector3(
            worldPosition.x,
            b.max.y + 10f,
            worldPosition.z
        );

        Ray ray = new Ray(origin, Vector3.down);

        if (waterCollider.Raycast(ray, out RaycastHit hit, 100f))
        {
            Vector2 uv = hit.textureCoord;

            InjectRipple(uv, rippleRadius, rippleStrength);
            InjectFoam(uv, foamRadius, foamStrength);

            return true;
        }

        Debug.LogWarning("InjectRippleAndFoamWorld failed. World position: " + worldPosition);
        return false;
    }






    public void InjectFoam(Vector2 uv, float radius, float strength)
    {
        if (foamRT == null || foamTempRT == null || foamInjectMaterial == null)
            return;

        foamInjectMaterial.SetVector("_Center", new Vector4(uv.x, uv.y, 0, 0));
        foamInjectMaterial.SetFloat("_Radius", radius);
        foamInjectMaterial.SetFloat("_Strength", strength);
        foamInjectMaterial.SetFloat("_Seed", Random.value * 10000f);


        Graphics.Blit(foamRT, foamTempRT, foamInjectMaterial);
        Graphics.Blit(foamTempRT, foamRT);

        AssignTextureToMaterial();
    }

    private void UpdateFoamDecay()
    {
        if (foamRT == null || foamTempRT == null || foamDecayMaterial == null)
            return;

        foamDecayMaterial.SetFloat("_Decay", foamDecay);

        Graphics.Blit(foamRT, foamTempRT, foamDecayMaterial);

        RenderTexture temp = foamRT;
        foamRT = foamTempRT;
        foamTempRT = temp;

        AssignTextureToMaterial();
    }



    private void UpdateWaveSimulation()
    {
        if (currentHeightRT == null || previousHeightRT == null || tempRT == null)
        {
            Debug.LogError("Wave Update Material is not assigned.");
            return;
        }
        if (WaveUpdateMaterial == null)
        {
            Debug.LogError("Wave Update Material is not assigned.");
            return;
        }


        WaveUpdateMaterial.SetTexture("_PrevTex", previousHeightRT);
        WaveUpdateMaterial.SetFloat("_WaveSpeed", waveSpeed);
        WaveUpdateMaterial.SetFloat("_Damping", waveDamping);
        WaveUpdateMaterial.SetFloat("_AspectRatio", GetPlaneAspectRatio());

        Graphics.Blit(currentHeightRT, tempRT, WaveUpdateMaterial);

        RenderTexture oldPrevious = previousHeightRT;
        previousHeightRT = currentHeightRT;
        currentHeightRT = tempRT;
        tempRT = oldPrevious;

        AssignTextureToMaterial();
    }

    private void CreateInterationRT()
    {
        currentHeightRT = CreateRT("RT_Water_CurrentHeight");
        previousHeightRT = CreateRT("RT_Water_PreviousHeight");
        tempRT = CreateRT("RT_Water_TempHeight");

        ClearRT(currentHeightRT, Color.black);
        ClearRT(previousHeightRT, Color.black);
        ClearRT(tempRT, Color.black);

    }

    private RenderTexture CreateRT(string rtName)
    {
        RenderTexture rt = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.RHalf);
        rt.name = rtName;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.filterMode = FilterMode.Bilinear;
        rt.useMipMap = false;
        rt.autoGenerateMips = false;
        rt.Create();

        return rt;
    }

    private void CreateFoamRTs()
    {
        foamRT = CreateRT("RT_Water_InteractionFoam");
        foamTempRT = CreateRT("RT_Water_InteractionFoamTemp");

        ClearRT(foamRT, Color.black);
        ClearRT(foamTempRT, Color.black);
    }




    private void AssignTextureToMaterial()
    {
        if (waterMaterial == null || currentHeightRT == null)
        {
            Debug.LogError("Water Material or Current Height RenderTexture is not assigned.");
            return;
        }
        waterMaterial.SetTexture(interactionHeightTexName, currentHeightRT);

        if (foamRT != null)
            waterMaterial.SetTexture(interactionFoamTexName, foamRT);
    }

    private void ClearRT(RenderTexture rt, Color clearColor)
    {
        RenderTexture currentActiveRT = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, clearColor);
        RenderTexture.active = currentActiveRT;
    }

    private void ReleaseRT(RenderTexture rt)
    {
        if (rt == null)
            return;

        rt.Release();
        Destroy(rt);

    }

    private void OnDestroy()
    {
        ReleaseRT(currentHeightRT);
        ReleaseRT(previousHeightRT);
        ReleaseRT(tempRT);
        ReleaseRT(foamRT);
        ReleaseRT(foamTempRT);
    }
}

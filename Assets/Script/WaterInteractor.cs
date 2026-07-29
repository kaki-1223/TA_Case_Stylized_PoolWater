using UnityEngine;

public class WaterInteractor : MonoBehaviour
{
    [Header("Target Water")]
    [SerializeField] private WaterSurface waterSurface;

    [Header("Ripple Settings")]
    [SerializeField] private float rippleRadius = 0.035f;
    [SerializeField] private float strengthMultiplier = 0.15f;
    [SerializeField] private float minSpeedToRipple = 0.05f;
    [SerializeField] private float injectInterval = 0.08f;


    [Header("Enter / Exit Splash")]
    [SerializeField] private float waterLevelY = 0f;
    [SerializeField] private float enterSplashRadius = 0.08f;
    [SerializeField] private float enterSplashStrength = 1.2f;
    [Header("Foam Settings")]
    [Header("Foam Settings")]
    [SerializeField] private float moveFoamRadius = 0.04f;
    [SerializeField] private float moveFoamStrengthMultiplier = 0.08f;

    [SerializeField] private float enterFoamRadius = 0.12f;
    [SerializeField] private float enterFoamStrength = 1.0f;


    private bool wasInWater;//判断物体是否接触水面

    [SerializeField] private float contactTolerance = 0.03f;

    private Collider interactorCollider;


    private Vector3 previousPosition;
    private float timer;

    private void Start()
    {
        previousPosition = transform.position;
        wasInWater = transform.position.y <= waterLevelY;
        interactorCollider = GetComponent<Collider>();


    }

    private void Update()
    {
        if (waterSurface == null)
            return;

        Vector3 currentPosition = transform.position;
        Vector3 velocity = (currentPosition - previousPosition) / Mathf.Max(Time.deltaTime, 0.0001f);

        float speed = velocity.magnitude;

        timer += Time.deltaTime;

        if (timer >= injectInterval && speed >= minSpeedToRipple && IsTouchingWater())
        {
            float strength = speed * strengthMultiplier;

            waterSurface.InjectRippleAndFoamWorld(currentPosition, rippleRadius, strength, moveFoamRadius, moveFoamStrengthMultiplier);

            timer = 0f;
        }

        previousPosition = currentPosition;

        bool isInWater = currentPosition.y <= waterLevelY;

        if (!wasInWater && isInWater)
        {
            Debug.Log("Stone entered water at world pos: " + transform.position);
            waterSurface.InjectRippleAndFoamWorld(currentPosition, enterSplashRadius, enterSplashStrength, enterFoamRadius, enterFoamStrength);

        }

        wasInWater = isInWater;

    }

    private bool IsTouchingWater()
    {
        if (waterSurface == null)
            return false;

        //float waterY = waterSurface.transform.position.y;
        float waterY = waterSurface.WaterY;

        float bottomY = interactorCollider != null
            ? interactorCollider.bounds.min.y
            : transform.position.y;

        return bottomY <= waterY + contactTolerance;
    }

}

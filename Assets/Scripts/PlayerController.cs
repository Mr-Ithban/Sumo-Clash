using UnityEngine;

/// <summary>
/// Moves one sumo player through its Rigidbody2D.
/// Round rules decide when this component may accept movement.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerController : MonoBehaviour
{
    private Rigidbody2D body;
    private PlayerInputHandler inputHandler;
    private TrailRenderer motionTrail;
    private ParticleSystem impactParticles;
    private Color effectColour = Color.white;
    private float moveSpeed;
    private float moveAcceleration;
    private float maxMoveSpeed;
    private float roundMoveMultiplier = 1f;
    private float roundDashMultiplier = 1f;
    private float roundImpactMultiplier = 1f;
    private int roundControlMode;
    private float spinSpeed = 105f;
    private float dashDuration = 0.28f;
    private float movementDamping = 0.42f;
    private bool movementEnabled;
    private float chargeAmount;
    private float dashRemaining;
    private float chargeCooldownRemaining;
    private float timedBoostRecoveryDuration = 2f;
    private float dashStrength;
    private Vector2 dashDirection = Vector2.right;
    private bool hitDuringDash;
    private bool requireChargeRelease;
    private float defaultDamping;
    private Vector2 facingDirection = Vector2.right;

    public Rigidbody2D Body => body;
    public float ChargeAmount => chargeAmount;
    public bool IsCharging => inputHandler != null && inputHandler.ChargeHeld;
    public bool IsDashing => dashRemaining > 0f;
    public bool IsBracing => inputHandler != null && inputHandler.SlowHeld;
    public bool CanCharge => chargeCooldownRemaining <= 0f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        defaultDamping = body.linearDamping;
    }

    public void Configure(PlayerInputHandler configuredInputHandler, float configuredMoveSpeed, Color configuredEffectColour)
    {
        inputHandler = configuredInputHandler;
        moveSpeed = configuredMoveSpeed;
        moveAcceleration = configuredMoveSpeed * 3.1f;
        maxMoveSpeed = configuredMoveSpeed * 1.2f;
        effectColour = configuredEffectColour;
        ConfigureMotionTrail();
        ConfigureImpactParticles();
    }

    private void ConfigureMotionTrail()
    {
        motionTrail = gameObject.AddComponent<TrailRenderer>();
        motionTrail.time = 0.28f;
        motionTrail.minVertexDistance = 0.055f;
        motionTrail.widthMultiplier = 0.24f;
        motionTrail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.92f), new Keyframe(0.72f, 0.58f), new Keyframe(1f, 0f));
        motionTrail.sortingOrder = 4;
        motionTrail.emitting = false;
        motionTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        motionTrail.receiveShadows = false;
        Gradient trailGradient = new Gradient();
        trailGradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(effectColour, 0.42f), new GradientColorKey(effectColour, 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.52f, 0.5f), new GradientAlphaKey(0f, 1f) });
        motionTrail.colorGradient = trailGradient;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            motionTrail.material = new Material(shader);
        }
    }

    private void ConfigureImpactParticles()
    {
        impactParticles = gameObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = impactParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.36f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.09f);
        main.startColor = new ParticleSystem.MinMaxGradient(effectColour, Color.white);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 48;

        ParticleSystem.EmissionModule emission = impactParticles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = impactParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;
        ParticleSystem.ColorOverLifetimeModule fade = impactParticles.colorOverLifetime;
        fade.enabled = true;
        Gradient particleGradient = new Gradient();
        particleGradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(effectColour, 0.45f), new GradientColorKey(effectColour, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.82f, 0.45f), new GradientAlphaKey(0f, 1f) });
        fade.color = new ParticleSystem.MinMaxGradient(particleGradient);
        ParticleSystemRenderer particleRenderer = impactParticles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sortingOrder = 7;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            particleRenderer.material = new Material(shader);
        }
    }

    public void SetEffectColour(Color colour)
    {
        effectColour = colour;
        if (motionTrail != null)
        {
            Gradient trailGradient = new Gradient();
            trailGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(effectColour, 0.42f), new GradientColorKey(effectColour, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.52f, 0.5f), new GradientAlphaKey(0f, 1f) });
            motionTrail.colorGradient = trailGradient;
        }
        if (impactParticles != null)
        {
            ParticleSystem.MainModule main = impactParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, effectColour);
            ParticleSystem.ColorOverLifetimeModule fade = impactParticles.colorOverLifetime;
            Gradient particleGradient = new Gradient();
            particleGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(effectColour, 0.45f), new GradientColorKey(effectColour, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.82f, 0.45f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(particleGradient);
        }
    }

    private void EmitImpactBurst(int particleCount)
    {
        if (impactParticles != null)
        {
            impactParticles.Emit(particleCount);
        }
    }

    public void SetRoundMultipliers(float movementMultiplier, float dashMultiplier, float impactMultiplier = 1f)
    {
        roundMoveMultiplier = movementMultiplier;
        roundDashMultiplier = dashMultiplier;
        roundImpactMultiplier = impactMultiplier;
    }

    public void SetRoundControlMode(int mode, float rotationSpeed, float normalDamping = 0.42f)
    {
        roundControlMode = mode;
        spinSpeed = rotationSpeed;
        movementDamping = normalDamping;
        dashDuration = mode == 1 || mode == 3 ? 2f : 0.28f;
    }

    public void SetTimedBoostRecoveryDuration(float duration)
    {
        timedBoostRecoveryDuration = Mathf.Max(0f, duration);
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (!movementEnabled)
        {
            chargeAmount = 0f;
            dashRemaining = 0f;
            body.linearDamping = defaultDamping;
            Stop();
            if (motionTrail != null)
            {
                motionTrail.emitting = false;
                motionTrail.Clear();
            }
            impactParticles?.Clear();
        }
    }

    public void ResetTo(Vector2 position)
    {
        body.position = position;
        chargeAmount = 0f;
        dashRemaining = 0f;
        chargeCooldownRemaining = 0f;
        requireChargeRelease = false;
        facingDirection = position.x < 0f ? Vector2.right : Vector2.left;
        inputHandler?.ConsumeChargeRelease();
        motionTrail?.Clear();
        impactParticles?.Clear();
        Stop();
    }

    public void Stop()
    {
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void FixedUpdate()
    {
        if (!movementEnabled || inputHandler == null)
        {
            Stop();
            if (motionTrail != null) motionTrail.emitting = false;
            return;
        }

        chargeCooldownRemaining = Mathf.Max(0f, chargeCooldownRemaining - Time.fixedDeltaTime);

        Vector2 moveInput = inputHandler.MoveInput;
        if (!inputHandler.ChargeHeld)
        {
            requireChargeRelease = false;
        }
        if (roundControlMode == 1)
        {
            body.MoveRotation(body.rotation + spinSpeed * Time.fixedDeltaTime);
            facingDirection = body.transform.up;
        }
        else if (moveInput.sqrMagnitude > 0.01f)
        {
            facingDirection = moveInput;
            float angle = Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg - 90f;
            body.MoveRotation(angle);
        }

        bool timedBoostRound = roundControlMode == 1 || roundControlMode == 3;
        float chargeLimit = timedBoostRound ? 2f : 1.2f;
        if (inputHandler.ChargeHeld && chargeCooldownRemaining <= 0f && !requireChargeRelease)
        {
            chargeAmount = Mathf.Min(chargeLimit, chargeAmount + Time.fixedDeltaTime);
        }

        bool releasedCharge = inputHandler.ConsumeChargeRelease();
        bool boostTimedOut = timedBoostRound && chargeAmount >= chargeLimit && chargeCooldownRemaining <= 0f;
        if (boostTimedOut)
        {
            requireChargeRelease = true;
        }
        if ((releasedCharge || boostTimedOut) && chargeAmount >= 0.12f && chargeCooldownRemaining <= 0f)
        {
            float chargeStrength = Mathf.InverseLerp(0.12f, chargeLimit, chargeAmount);
            dashDirection = roundControlMode == 1 ? (Vector2)body.transform.up : facingDirection;
            body.linearDamping = timedBoostRound ? 0.65f : Mathf.Max(defaultDamping, 0.18f);
            float boostSpeed = timedBoostRound
                ? Mathf.Lerp(3.5f, 5.2f, chargeStrength)
                : Mathf.Lerp(7f, 12f, chargeStrength);
            body.linearVelocity = dashDirection * boostSpeed * roundDashMultiplier;
            dashStrength = Mathf.Lerp(3.5f, 8f, chargeStrength) * roundDashMultiplier * roundImpactMultiplier;
            dashRemaining = dashDuration;
            chargeCooldownRemaining = timedBoostRound ? dashDuration + timedBoostRecoveryDuration : 1.6f;
            chargeAmount = 0f;
            hitDuringDash = false;
            EmitImpactBurst(5);
            SumoClashAudio.Instance?.PlayDash();
        }

        if (motionTrail != null)
        {
            motionTrail.time = IsDashing ? 0.42f : 0.28f;
            motionTrail.emitting = body.linearVelocity.sqrMagnitude > 1.35f;
        }

        if (dashRemaining > 0f)
        {
            dashRemaining = Mathf.Max(0f, dashRemaining - Time.fixedDeltaTime);
            return;
        }

        bool bracing = inputHandler.SlowHeld;
        body.linearDamping = bracing ? 3.2f : Mathf.Max(defaultDamping, movementDamping);
        float accelerationMultiplier = bracing ? 0.48f : 1f;
        if (inputHandler.ChargeHeld)
        {
            accelerationMultiplier *= 0.7f;
        }

        bool movementAllowed = roundControlMode == 0 || roundControlMode == 3;
        if (movementAllowed)
        {
            body.AddForce(inputHandler.MoveInput * moveAcceleration * accelerationMultiplier * roundMoveMultiplier, ForceMode2D.Force);
        }
        else if (roundControlMode == 2 && inputHandler.ChargeHeld && moveInput.sqrMagnitude > 0.01f)
        {
            facingDirection = moveInput;
        }
        float speedLimit = bracing ? moveSpeed * 0.55f : maxMoveSpeed * roundMoveMultiplier;
        if (!movementAllowed && roundControlMode != 1)
        {
            body.linearVelocity = Vector2.zero;
        }
        else if (roundControlMode == 1)
        {
            body.linearVelocity = Vector2.zero;
        }
        else if (body.linearVelocity.sqrMagnitude > speedLimit * speedLimit)
        {
            body.linearVelocity = body.linearVelocity.normalized * speedLimit;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.relativeVelocity.sqrMagnitude > 1f)
        {
            EmitImpactBurst(Mathf.Clamp(5 + Mathf.RoundToInt(collision.relativeVelocity.magnitude * 2f), 5, 14));
            SumoClashAudio.Instance?.PlayHit();
        }
        ApplyChargeImpact(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        ApplyChargeImpact(collision);
    }

    private void ApplyChargeImpact(Collision2D collision)
    {
        if (!movementEnabled || !IsDashing || hitDuringDash)
        {
            return;
        }

        Rigidbody2D opponent = collision.rigidbody;
        if (opponent == null || opponent == body)
        {
            return;
        }

        opponent.AddForce(dashDirection * dashStrength, ForceMode2D.Impulse);
        hitDuringDash = true;
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Builds the local Sumo Clash match and its menu at runtime so the starter scene
/// does not need hand-authored scene objects.
/// </summary>
public sealed class SumoClashPrototype : MonoBehaviour
{
    private const float ArenaRadius = 3.75f;
    private const float BaseSafeRadius = 3.30f;
    private const float PlayerRadius = 0.45f;
    private const float MoveSpeed = 3.95f;
    private const float CountdownDuration = 3.4f;
    private const int ChampionshipRounds = 3;

    private static readonly Color[] PlayerOneColours =
    {
        new Color(0.20f, 0.68f, 1f), new Color(0.53f, 0.36f, 1f), new Color(0.18f, 0.85f, 0.62f)
    };
    private static readonly Color[] PlayerTwoColours =
    {
        new Color(1f, 0.34f, 0.36f), new Color(1f, 0.63f, 0.19f), new Color(0.96f, 0.38f, 0.72f)
    };

    private readonly PlayerState[] players = new PlayerState[2];
    private readonly string[] playerNames = { "PLAYER 1", "PLAYER 2" };
    private readonly Color[] playerColours = { new Color(0.20f, 0.68f, 1f), new Color(1f, 0.34f, 0.36f) };
    private readonly int[] playerLooks = new int[2];
    private readonly int[] scores = new int[2];
    private Sprite circleSprite;
    private Sprite barricadeSprite;
    private Sprite menuFighterSprite;
    private Texture2D colourWheelTexture;
    private GameState gameState;
    private float phaseRemaining;
    private int winner = -1;
    private bool isDraw;
    private bool playAgainstComputer = true;
    private int setupPlayerIndex;
    private int roundIndex;
    private int previousRoundWinner = -1;
    private float safeRadius = BaseSafeRadius;
    private Transform safeAreaVisual;
    private SpriteRenderer ringGlowRenderer;
    private SpriteRenderer ringEdgeRenderer;
    private SpriteRenderer arenaFloorRenderer;
    private SpriteRenderer safeAreaRenderer;
    private float roundElapsed;
    private readonly GameObject[] roundBarricades = new GameObject[4];
    private float cpuChargeTimer;
    private bool cpuIsCharging;
    private readonly Vector2[] menuPixels = new Vector2[52];
    private readonly Color[] menuPixelColours = new Color[52];
    private readonly float[] menuPixelSizes = new float[52];
    private int lastCountdownBeat;
    private SumoClashAudio audioManager;
    private GUIStyle standardButtonStyle;
    private GUIStyle primaryButtonStyle;
    private GUIStyle arrowButtonStyle;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle smallStyle;
    private GUIStyle playerCardTitleStyle;
    private GUIStyle colourNameStyle;
    private GUIStyle instructionsButtonStyle;
    private GUIStyle quitButtonStyle;
    private GUIStyle transparentButtonStyle;
    private GUIStyle instructionsHeadingStyle;
    private GUIStyle instructionsSectionStyle;
    private GUIStyle instructionsDetailStyle;
    private Texture2D buttonTexture;
    private Texture2D buttonHoverTexture;
    private Texture2D buttonActiveTexture;
    private Texture2D primaryTexture;

    private enum GameState
    {
        MainMenu,
        Instructions,
        PlayerSetup,
        Countdown,
        Playing,
        RoundResult,
        ChampionshipResult
    }

    private sealed class PlayerState
    {
        public PlayerController Controller;
        public PlayerInputHandler Input;
        public SpriteRenderer Renderer;
        public int PlayerNumber;
        public bool IsOut;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreatePrototype()
    {
        if (FindAnyObjectByType<SumoClashPrototype>() == null)
        {
            new GameObject("Sumo Clash").AddComponent<SumoClashPrototype>();
        }
    }

    private void Awake()
    {
        audioManager = gameObject.AddComponent<SumoClashAudio>();
        circleSprite = CreateCircleSprite();
        barricadeSprite = CreateBarricadeSprite();
        colourWheelTexture = CreateColourWheelTexture();
        menuFighterSprite = CreatePixelSumoSprite(PlayerOneColours[0], 0);
        playerNames[1] = "DOJO CPU";
        InitializeMenuPixels();
        ConfigureCamera();
        CreateArena();
        CreatePlayers();
        SetPlayerMovement(false);
        gameState = GameState.MainMenu;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (gameState == GameState.PlayerSetup)
            {
                BackFromSetup();
            }
            else if (gameState != GameState.MainMenu)
            {
                gameState = GameState.MainMenu;
                SetPlayerMovement(false);
                audioManager.PlayMenu();
            }
        }

        if (gameState == GameState.MainMenu)
        {
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            {
                StartPlayerSetup();
            }
        }
        else if (gameState == GameState.Instructions)
        {
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            {
                gameState = GameState.MainMenu;
            }
        }
        else if (gameState == GameState.PlayerSetup)
        {
            // Setup fields and navigation are handled by the menu buttons.
        }
        else if (gameState == GameState.Countdown)
        {
            phaseRemaining -= Time.deltaTime;
            int countdownBeat = Mathf.CeilToInt(phaseRemaining - 0.4f);
            if (countdownBeat > 0 && countdownBeat < lastCountdownBeat)
            {
                audioManager.PlayCountdown();
                lastCountdownBeat = countdownBeat;
            }

            if (phaseRemaining <= 0f)
            {
                gameState = GameState.Playing;
                SetPlayerMovement(true);
                audioManager.PlayStart();
            }
        }
        else if (gameState == GameState.Playing)
        {
            UpdateComputerOpponent();
        }
        else if (gameState == GameState.RoundResult)
        {
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            {
                AdvanceRound();
            }
        }
        else if (gameState == GameState.ChampionshipResult)
        {
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            {
                gameState = GameState.MainMenu;
                audioManager.PlayMenu();
            }
        }
    }

    private void FixedUpdate()
    {
        if (gameState != GameState.Playing)
        {
            return;
        }

        roundElapsed += Time.fixedDeltaTime;

        players[0].IsOut = IsFullyOutsideRing(players[0]);
        players[1].IsOut = IsFullyOutsideRing(players[1]);
        if (players[0].IsOut || players[1].IsOut)
        {
            if (players[0].IsOut && players[1].IsOut)
            {
                EndRound(-1, true);
            }
            else
            {
                EndRound(players[0].IsOut ? 1 : 0, false);
            }
        }
    }

    private static bool IsFullyOutsideRing(PlayerState player)
    {
        Rigidbody2D body = player.Controller.Body;
        Collider2D playerCollider = body.GetComponent<Collider2D>();
        float colliderRadius = PlayerRadius;
        if (playerCollider is CircleCollider2D circle)
        {
            Vector3 scale = body.transform.lossyScale;
            colliderRadius = circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }

        // safeRadius marks the changing inner play zone. Ring-outs use the
        // fixed visible dohyo edge, and require the complete collider to clear it.
        float ringOutDistance = ArenaRadius - 0.08f + colliderRadius;
        return body.position.sqrMagnitude >= ringOutDistance * ringOutDistance;
    }

    private void StartPlayerSetup()
    {
        setupPlayerIndex = 0;
        gameState = GameState.PlayerSetup;
        audioManager.PlayMenu();
    }

    private void BackFromSetup()
    {
        if (setupPlayerIndex > 0)
        {
            setupPlayerIndex--;
        }
        else
        {
            gameState = GameState.MainMenu;
        }

        audioManager.PlayMenu();
    }

    private void CompletePlayerSetup()
    {
        if (!playAgainstComputer && setupPlayerIndex == 0)
        {
            setupPlayerIndex = 1;
            audioManager.PlayMenu();
            return;
        }

        StartChampionship();
    }

    private void StartChampionship()
    {
        scores[0] = 0;
        scores[1] = 0;
        roundIndex = 0;
        previousRoundWinner = -1;
        BeginRound();
    }

    private void AdvanceRound()
    {
        if (roundIndex + 1 >= ChampionshipRounds)
        {
            gameState = GameState.ChampionshipResult;
            phaseRemaining = 0f;
            audioManager.PlayResult(scores[0] != scores[1]);
            return;
        }

        roundIndex++;
        BeginRound();
    }

    private void BeginRound()
    {
        ConfigureRound();
        players[0].IsOut = false;
        players[1].IsOut = false;
        players[0].Controller.ResetTo(new Vector2(-1.5f, 0f));
        players[1].Controller.ResetTo(new Vector2(1.5f, 0f));
        ApplyRoundBonuses();
        players[0].Renderer.color = Color.white;
        players[1].Renderer.color = Color.white;
        cpuChargeTimer = 0f;
        cpuIsCharging = false;
        phaseRemaining = CountdownDuration;
        gameState = GameState.Countdown;
        lastCountdownBeat = 3;
        SetPlayerMovement(false);
        audioManager.PlayMenu();
        audioManager.PlayCountdown();
    }

    private void ConfigureRound()
    {
        float[] safeRadii = { 3.30f, 2.82f, 3.05f };
        safeRadius = safeRadii[roundIndex];
        roundElapsed = 0f;
        if (safeAreaVisual != null)
        {
            safeAreaVisual.localScale = Vector3.one * (safeRadius * 2f);
            ApplyRoundArenaPalette();
        }

        for (int i = 0; i < roundBarricades.Length; i++)
        {
            if (roundBarricades[i] != null)
            {
                Destroy(roundBarricades[i]);
                roundBarricades[i] = null;
            }
        }

        if (roundIndex == 2)
        {
            CreateRoundThreeBarricades();
        }
    }

    private void CreateRoundThreeBarricades()
    {
        Color[] colours = { new Color(0.95f, 0.47f, 0.18f), new Color(0.22f, 0.78f, 0.93f), new Color(0.94f, 0.73f, 0.28f), new Color(0.44f, 0.55f, 0.64f) };
        Vector2[] positions = CreateSpacedBarricadePositions();
        PhysicsMaterial2D bounceMaterial = new PhysicsMaterial2D("Immovable Dohyo Barricade")
        {
            friction = 0.25f,
            bounciness = 0.06f,
            frictionCombine = PhysicsMaterialCombine2D.Maximum,
            bounceCombine = PhysicsMaterialCombine2D.Minimum
        };

        for (int i = 0; i < roundBarricades.Length; i++)
        {
            GameObject barricade = new GameObject($"Round 3 fixed barricade {i + 1}");
            barricade.transform.position = positions[i];
            barricade.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 180f));
            barricade.transform.localScale = new Vector3(0.50f, 0.36f, 1f);
            SpriteRenderer renderer = barricade.AddComponent<SpriteRenderer>();
            renderer.sprite = barricadeSprite;
            renderer.color = colours[i];
            renderer.sortingOrder = 4;
            BoxCollider2D collider = barricade.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.sharedMaterial = bounceMaterial;
            roundBarricades[i] = barricade;
        }
    }

    private Vector2[] CreateSpacedBarricadePositions()
    {
        Vector2[] positions = new Vector2[roundBarricades.Length];
        Vector2[] playerStarts = { new Vector2(-1.5f, 0f), new Vector2(1.5f, 0f) };
        const float minObstacleSpacing = 1.18f;
        const float playerStartClearance = 0.92f;
        int placed = 0;

        // Try many random spots, rejecting placements that bunch up or block a spawn.
        for (int attempt = 0; attempt < 240 && placed < positions.Length; attempt++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float radius = Random.Range(0.82f, safeRadius - 0.85f);
            Vector2 candidate = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            bool clear = true;
            for (int i = 0; i < placed; i++)
            {
                if (Vector2.Distance(candidate, positions[i]) < minObstacleSpacing)
                {
                    clear = false;
                    break;
                }
            }
            for (int i = 0; clear && i < playerStarts.Length; i++)
            {
                if (Vector2.Distance(candidate, playerStarts[i]) < playerStartClearance)
                {
                    clear = false;
                }
            }

            if (clear)
            {
                positions[placed++] = candidate;
            }
        }

        // A fixed angular search keeps the layout valid even if random sampling is unlucky.
        if (placed < positions.Length)
        {
            for (int degree = 0; degree < 360 && placed < positions.Length; degree += 15)
            {
                float angle = degree * Mathf.Deg2Rad;
                Vector2 candidate = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.8f;
                bool clear = true;
                for (int i = 0; i < placed; i++)
                {
                    if (Vector2.Distance(candidate, positions[i]) < minObstacleSpacing)
                    {
                        clear = false;
                        break;
                    }
                }
                for (int i = 0; clear && i < playerStarts.Length; i++)
                {
                    if (Vector2.Distance(candidate, playerStarts[i]) < playerStartClearance)
                    {
                        clear = false;
                    }
                }

                if (clear)
                {
                    positions[placed++] = candidate;
                }
            }
        }

        return positions;
    }

    private void ApplyRoundBonuses()
    {
        for (int i = 0; i < players.Length; i++)
        {
            players[i].Input.SetSpinningTwoPlayerControls(!playAgainstComputer && roundIndex >= 1);
            int controlMode = roundIndex >= 1 ? 1 : 3;
            float movementDamping = roundIndex == 0 ? 1.6f : 0.42f;
            players[i].Controller.SetRoundControlMode(controlMode, roundIndex >= 1 ? 55f : 0f, movementDamping);
            players[i].Controller.SetTimedBoostRecoveryDuration(!playAgainstComputer && roundIndex >= 1 ? 0f : 2f);
            float movement = 1f;
            float dash = 1f;
            float impact = 1f;
            if (roundIndex >= 1)
            {
                movement = 0.82f;
                dash = 0.70f;
            }
            if (i == previousRoundWinner)
            {
                movement = 1.08f;
                dash = 1.12f;
            }

            if (playAgainstComputer && i == 1)
            {
                movement *= roundIndex == 0 ? 0.55f : 0.84f;
                dash *= roundIndex == 0 ? 0.68f : 1f;
                impact = roundIndex == 0 ? 0.55f : 1f;
            }

            players[i].Controller.SetRoundMultipliers(movement, dash, impact);
        }
    }

    private void EndRound(int roundWinner, bool draw)
    {
        if (gameState != GameState.Playing)
        {
            return;
        }

        SetPlayerMovement(false);
        winner = roundWinner;
        isDraw = draw;
        if (roundWinner >= 0)
        {
            scores[roundWinner]++;
        }

        previousRoundWinner = draw ? -1 : roundWinner;
        gameState = GameState.RoundResult;
        audioManager.PlayResult(roundWinner >= 0 && !draw);
    }

    private void UpdateComputerOpponent()
    {
        if (!playAgainstComputer)
        {
            return;
        }

        PlayerState computer = players[1];
        Vector2 computerPosition = computer.Controller.Body.position;
        Vector2 playerPosition = players[0].Controller.Body.position;
        Vector2 towardPlayer = playerPosition - computerPosition;
        float distance = towardPlayer.magnitude;
        Vector2 movement = distance > 0.01f ? towardPlayer / distance : Vector2.zero;

        float edgeStart = safeRadius - 0.62f;
        float edgeBlend = Mathf.InverseLerp(edgeStart, safeRadius, computerPosition.magnitude);
        if (edgeBlend > 0f)
        {
            movement = (movement + -computerPosition.normalized * (1.5f + edgeBlend * 2f)).normalized;
        }

        bool shouldBrace = players[0].Controller.IsDashing || edgeBlend > 0.65f;
        // In the spinning rounds the CPU only gets one brief alignment window per
        // rotation. A narrow 14-degree window made it look passive in practice;
        // allow it to begin charging while it is turning toward the player.
        bool facingPlayer = roundIndex == 0 || Vector2.Dot(computer.Controller.Body.transform.up, towardPlayer.normalized) > 0.62f;
        float attackDistance = roundIndex == 0 ? 1.38f : 3.4f;
        bool firstRoundWarmup = roundIndex == 0 && roundElapsed < 1.8f;
        if (!cpuIsCharging && computer.Controller.CanCharge && distance < attackDistance && facingPlayer && !firstRoundWarmup)
        {
            cpuIsCharging = true;
            cpuChargeTimer = 0f;
        }

        bool chargeInput = false;
        if (cpuIsCharging)
        {
            cpuChargeTimer += Time.deltaTime;
            float holdTime = roundIndex == 0 ? 0.19f : 0.32f;
            chargeInput = cpuChargeTimer < holdTime;
            if (!chargeInput)
            {
                cpuIsCharging = false;
            }
        }

        computer.Input.SetComputerInput(movement, chargeInput, shouldBrace && !chargeInput);
    }

    private void SetPlayerMovement(bool enabled)
    {
        foreach (PlayerState player in players)
        {
            if (player?.Controller != null)
            {
                player.Controller.SetMovementEnabled(enabled);
            }
        }
    }

    private void ConfigureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.backgroundColor = Color.black;
        if (camera.GetComponent<AudioListener>() == null)
        {
            camera.gameObject.AddComponent<AudioListener>();
        }
    }

    private void CreateArena()
    {
        ringGlowRenderer = CreateCircleVisual("Ring glow", new Color(0.26f, 0.95f, 1f, 0.24f), ArenaRadius, 0).GetComponent<SpriteRenderer>();
        ringEdgeRenderer = CreateCircleVisual("Neon dohyo edge", new Color(0.72f, 1f, 1f), ArenaRadius - 0.08f, 1).GetComponent<SpriteRenderer>();
        arenaFloorRenderer = CreateCircleVisual("Dohyo interior", new Color(0.012f, 0.025f, 0.035f), ArenaRadius - 0.18f, 2).GetComponent<SpriteRenderer>();
        safeAreaVisual = CreateCircleVisual("Safe fighting area", new Color(0.018f, 0.032f, 0.042f), BaseSafeRadius, 3);
        safeAreaRenderer = safeAreaVisual.GetComponent<SpriteRenderer>();
        CreateCircleVisual("Center mark", new Color(0.08f, 0.17f, 0.19f), 0.13f, 4);
    }

    private void ApplyRoundArenaPalette()
    {
        if (ringGlowRenderer == null)
        {
            return;
        }

        if (roundIndex == 0)
        {
            ringGlowRenderer.color = new Color(0.18f, 0.78f, 1f, 0.28f);
            ringEdgeRenderer.color = new Color(0.72f, 0.96f, 1f);
            arenaFloorRenderer.color = new Color(0.012f, 0.025f, 0.040f);
            safeAreaRenderer.color = new Color(0.018f, 0.032f, 0.050f);
        }
        else if (roundIndex == 1)
        {
            ringGlowRenderer.color = new Color(1f, 0.40f, 0.16f, 0.24f);
            ringEdgeRenderer.color = new Color(1f, 0.78f, 0.48f);
            arenaFloorRenderer.color = new Color(0.040f, 0.018f, 0.020f);
            safeAreaRenderer.color = new Color(0.055f, 0.022f, 0.023f);
        }
        else
        {
            ringGlowRenderer.color = new Color(0.36f, 0.44f, 1f, 0.25f);
            ringEdgeRenderer.color = new Color(0.66f, 0.78f, 1f);
            arenaFloorRenderer.color = new Color(0.016f, 0.022f, 0.046f);
            safeAreaRenderer.color = new Color(0.025f, 0.034f, 0.067f);
        }
    }

    private void CreatePlayers()
    {
        players[0] = CreatePlayer("Player 1", playerColours[0], new Vector2(-1.5f, 0f), PlayerInputHandler.ControlScheme.Wasd, 0);
        players[1] = CreatePlayer("Player 2", playerColours[1], new Vector2(1.5f, 0f), PlayerInputHandler.ControlScheme.ArrowKeys, 1);
    }

    private PlayerState CreatePlayer(string playerName, Color colour, Vector2 position, PlayerInputHandler.ControlScheme controlScheme, int playerNumber)
    {
        GameObject playerObject = new GameObject(playerName);
        playerObject.transform.position = position;
        playerObject.transform.localScale = Vector3.one * (PlayerRadius * 2f);

        SpriteRenderer renderer = playerObject.AddComponent<SpriteRenderer>();
        renderer.sprite = CreatePixelSumoSprite(colour, playerLooks[playerNumber]);
        renderer.color = Color.white;
        renderer.sortingOrder = 5;

        CircleCollider2D collider = playerObject.AddComponent<CircleCollider2D>();
        collider.sharedMaterial = new PhysicsMaterial2D("Sumo Contact") { friction = 0.18f, bounciness = 0.12f };

        Rigidbody2D body = playerObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.mass = 1f;
        body.freezeRotation = false;
        body.angularDamping = 100f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        PlayerInputHandler inputHandler = playerObject.AddComponent<PlayerInputHandler>();
        PlayerController controller = playerObject.AddComponent<PlayerController>();
        PlayerInputHandler.ControlScheme configuredScheme = playerNumber == 1 && playAgainstComputer
            ? PlayerInputHandler.ControlScheme.Computer
            : playerNumber == 0 && playAgainstComputer
                ? PlayerInputHandler.ControlScheme.WasdAndArrows
                : controlScheme;
        inputHandler.Configure(configuredScheme);
        controller.Configure(inputHandler, MoveSpeed, colour);
        return new PlayerState { Controller = controller, Input = inputHandler, Renderer = renderer, PlayerNumber = playerNumber };
    }

    private Transform CreateCircleVisual(string objectName, Color colour, float radius, int sortingOrder)
    {
        GameObject visual = new GameObject(objectName);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = circleSprite;
        renderer.color = colour;
        renderer.sortingOrder = sortingOrder;
        visual.transform.localScale = Vector3.one * (radius * 2f);
        return visual.transform;
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        float radius = size * 0.5f;
        Vector2 centre = new Vector2(radius - 0.5f, radius - 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = Mathf.Clamp01(radius - Vector2.Distance(new Vector2(x, y), centre));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateBarricadeSprite()
    {
        const int width = 24;
        const int height = 18;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[width * height];
        Color stone = new Color(0.72f, 0.79f, 0.82f);
        Color dark = new Color(0.12f, 0.19f, 0.24f);
        Color edge = new Color(0.04f, 0.07f, 0.10f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool border = x < 2 || x >= width - 2 || y < 2 || y >= height - 2;
                Color pixel = border ? edge : ((x / 4 + y / 4) % 2 == 0 ? stone : dark);
                if (!border && (x == 5 || x == 6 || x == 17 || x == 18))
                {
                    pixel = Color.Lerp(pixel, Color.white, 0.28f);
                }
                pixels[y * width + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
    }

    private static Sprite CreatePixelSumoSprite(Color teamColour, int look)
    {
        const int size = 28;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[size * size];
        Color outline = Color.Lerp(Color.black, teamColour, 0.22f);
        Color highlight = Color.Lerp(teamColour, Color.white, 0.28f);
        Color belt = new Color(0.98f, 0.73f, 0.22f);

        for (int y = 1; y < size; y++)
        {
            for (int x = 2; x < size - 2; x++)
            {
                bool head = y >= 19 && (y >= 21 ? x >= 10 && x <= 17 : x >= 9 && x <= 18);
                bool torso = y >= 12 && y <= 20 && x >= (y >= 18 ? 9 : 7) && x <= (y >= 18 ? 18 : 20);
                bool arms = y >= 13 && y <= 17 && ((x >= 3 && x <= 8) || (x >= 19 && x <= 24));
                bool hips = y >= 7 && y <= 13 && x >= 6 && x <= 21;
                bool legs = y >= 2 && y <= 7 && ((x >= 7 && x <= 12) || (x >= 15 && x <= 20));
                bool feet = y == 1 && ((x >= 5 && x <= 12) || (x >= 15 && x <= 22));
                if (!(head || torso || arms || hips || legs || feet))
                {
                    continue;
                }

                bool edge = !PixelSumoInside(x - 1, y, size) || !PixelSumoInside(x + 1, y, size)
                    || !PixelSumoInside(x, y - 1, size) || !PixelSumoInside(x, y + 1, size);
                Color colour = edge ? outline : teamColour;
                if (!edge && ((x + y + look) % 7 == 0 || x == 8 && y > 12))
                {
                    colour = highlight;
                }

                if (y == 8 || y == 9)
                {
                    colour = belt;
                }
                if (head && y == (look == 1 ? 19 : 24) && x >= 9 && x <= 18)
                {
                    colour = Color.white;
                }
                if (head && y == 21 && (x == 11 || x == 16))
                {
                    colour = outline;
                }
                if (look == 2 && y == 26 && x >= 12 && x <= 15)
                {
                    colour = teamColour;
                }

                pixels[y * size + x] = colour;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static bool PixelSumoInside(int x, int y, int size)
    {
        if (x < 2 || x >= size - 2 || y < 1 || y >= size)
        {
            return false;
        }

        bool head = y >= 19 && (y >= 21 ? x >= 10 && x <= 17 : x >= 9 && x <= 18);
        bool torso = y >= 12 && y <= 20 && x >= (y >= 18 ? 9 : 7) && x <= (y >= 18 ? 18 : 20);
        bool arms = y >= 13 && y <= 17 && ((x >= 3 && x <= 8) || (x >= 19 && x <= 24));
        bool hips = y >= 7 && y <= 13 && x >= 6 && x <= 21;
        bool legs = y >= 2 && y <= 7 && ((x >= 7 && x <= 12) || (x >= 15 && x <= 20));
        bool feet = y == 1 && ((x >= 5 && x <= 12) || (x >= 15 && x <= 22));
        return head || torso || arms || hips || legs || feet;
    }

    private static Texture2D CreateColourWheelTexture()
    {
        const int size = 160;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        float centre = (size - 1) * 0.5f;
        float radius = size * 0.47f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - centre;
                float dy = y - centre;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                if (distance > radius)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float hue = Mathf.Repeat(Mathf.Atan2(dy, dx) / (2f * Mathf.PI), 1f);
                float saturation = Mathf.Clamp01(distance / radius);
                texture.SetPixel(x, y, Color.HSVToRGB(hue, saturation, 1f));
            }
        }

        texture.Apply();
        return texture;
    }

    private void InitializeMenuPixels()
    {
        System.Random random = new System.Random(41);
        for (int i = 0; i < menuPixels.Length; i++)
        {
            menuPixels[i] = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
            menuPixelColours[i] = i % 3 == 0 ? new Color(0.25f, 0.72f, 1f) : i % 3 == 1 ? new Color(0.89f, 0.69f, 0.24f) : new Color(0.7f, 0.78f, 0.81f);
            menuPixelSizes[i] = 5f + (float)random.NextDouble() * 12f;
        }
    }

    private void RefreshPlayerAppearance(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= players.Length || players[playerIndex]?.Renderer == null)
        {
            return;
        }

        Sprite oldSprite = players[playerIndex].Renderer.sprite;
        Sprite newSprite = CreatePixelSumoSprite(playerColours[playerIndex], playerLooks[playerIndex]);
        players[playerIndex].Renderer.sprite = newSprite;
        players[playerIndex].Renderer.color = Color.white;
        players[playerIndex].Controller.SetEffectColour(playerColours[playerIndex]);
        if (oldSprite != null && oldSprite != newSprite)
        {
            Destroy(oldSprite.texture);
            Destroy(oldSprite);
        }

        if (playerIndex == 0)
        {
            menuFighterSprite = newSprite;
        }
    }

    private void OnGUI()
    {
        EnsureGuiStyles();
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        if (scale <= 0f)
        {
            scale = 1f;
        }

        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        if (gameState != GameState.Countdown && gameState != GameState.Playing)
        {
            DrawDecorativePixels(width, height);
        }
        if (gameState == GameState.MainMenu)
        {
            DrawMenu(width, height, titleStyle, subtitleStyle, bodyStyle, smallStyle);
        }
        else if (gameState == GameState.PlayerSetup)
        {
            DrawPlayerSetup(width, height, titleStyle, subtitleStyle, bodyStyle, smallStyle);
        }
        else if (gameState == GameState.Instructions)
        {
            DrawInstructions(width, height, titleStyle, subtitleStyle, bodyStyle, smallStyle);
        }
        else if (gameState == GameState.Countdown || gameState == GameState.Playing)
        {
            DrawMatchHud(width, height, bodyStyle, smallStyle);
        }
        else if (gameState == GameState.RoundResult)
        {
            DrawRoundResult(width, height, titleStyle, subtitleStyle, bodyStyle, smallStyle);
        }
        else
        {
            DrawChampionshipResult(width, height, titleStyle, subtitleStyle, bodyStyle, smallStyle);
        }

        GUI.matrix = oldMatrix;
    }

    private void DrawMenu(float width, float height, GUIStyle title, GUIStyle subtitle, GUIStyle body, GUIStyle small)
    {
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        float cx = width * 0.5f;
        GUI.Label(new Rect(cx - 400f, height * 0.12f, 800f, 130f), "SUMO CLASH", titleStyle);
        GUI.Label(new Rect(cx - 260f, height * 0.31f, 520f, 38f), "PIXEL DOHYO", subtitleStyle);
        GUI.DrawTexture(new Rect(cx + 235f, height * 0.39f, 190f, 190f), menuFighterSprite.texture);
        if (GUI.Button(new Rect(cx - 260f, height * 0.79f, 260f, 42f), playAgainstComputer ? "MODE: VS COMPUTER" : "MODE: 2 PLAYERS", instructionsButtonStyle))
        {
            playAgainstComputer = !playAgainstComputer;
            playerNames[1] = playAgainstComputer ? "DOJO CPU" : "PLAYER 2";
            players[0].Input.Configure(playAgainstComputer ? PlayerInputHandler.ControlScheme.WasdAndArrows : PlayerInputHandler.ControlScheme.Wasd);
            players[1].Input.Configure(playAgainstComputer ? PlayerInputHandler.ControlScheme.Computer : PlayerInputHandler.ControlScheme.ArrowKeys);
            audioManager.PlayMenu();
        }
        if (GUI.Button(new Rect(cx - 260f, height * 0.39f, 190f, 58f), "PLAY", primaryButtonStyle))
        {
            StartPlayerSetup();
        }
        if (GUI.Button(new Rect(cx - 260f, height * 0.53f, 190f, 58f), "INST", instructionsButtonStyle))
        {
            gameState = GameState.Instructions;
            audioManager.PlayMenu();
        }
        if (GUI.Button(new Rect(cx - 260f, height * 0.67f, 190f, 58f), "QUIT", quitButtonStyle))
        {
            Application.Quit();
        }
        GUI.Label(new Rect(cx - 280f, height * 0.90f, 560f, 40f), "THREE ROUNDS • THE RING GETS SMALLER EACH ROUND", small);
    }

    private void DrawPlayerSetup(float width, float height, GUIStyle title, GUIStyle subtitle, GUIStyle body, GUIStyle small)
    {
        int p = setupPlayerIndex;
        float cx = width * 0.5f;
        GUI.Label(new Rect(cx - 400f, height * 0.06f, 800f, 110f), "SUMO CLASH", titleStyle);
        GUI.Label(new Rect(cx - 260f, height * 0.24f, 520f, 52f), p == 0 ? "PLAYER 1" : (playAgainstComputer ? "CPU OPPONENT" : "PLAYER 2"), playerCardTitleStyle);
        string defaultName = p == 0 ? "PLAYER 1" : playAgainstComputer ? "DOJO CPU" : "PLAYER 2";
        playerNames[p] = GUI.TextField(new Rect(cx - 125f, height * 0.34f, 250f, 42f), playerNames[p] == "DOJO CPU" && p == 1 ? "DOJO CPU" : playerNames[p], 18);
        if (string.IsNullOrWhiteSpace(playerNames[p])) playerNames[p] = defaultName;

        Rect fighterRect = new Rect(cx - 85f, height * 0.46f, 170f, 170f);
        GUI.DrawTexture(fighterRect, players[p].Renderer.sprite.texture);
        Rect wheelRect = new Rect(cx + 205f, height * 0.47f, 150f, 150f);
        GUI.DrawTexture(wheelRect, colourWheelTexture);
        if (GUI.Button(wheelRect, GUIContent.none, transparentButtonStyle) && Event.current != null)
        {
            Vector2 local = Event.current.mousePosition - new Vector2(wheelRect.x, wheelRect.y);
            float dx = local.x / wheelRect.width - 0.5f;
            float dy = 0.5f - local.y / wheelRect.height;
            playerColours[p] = Color.HSVToRGB(Mathf.Repeat(Mathf.Atan2(dy, dx) / (2f * Mathf.PI), 1f), Mathf.Clamp01(new Vector2(dx, dy).magnitude * 2f), 1f);
            RefreshPlayerAppearance(p);
        }
        if (GUI.Button(new Rect(cx - 195f, height * 0.52f, 56f, 56f), "◀", arrowButtonStyle))
        {
            playerLooks[p] = (playerLooks[p] + 2) % 3;
            RefreshPlayerAppearance(p);
        }
        if (GUI.Button(new Rect(cx + 140f, height * 0.52f, 56f, 56f), "▶", arrowButtonStyle))
        {
            playerLooks[p] = (playerLooks[p] + 1) % 3;
            RefreshPlayerAppearance(p);
        }
        GUI.Label(new Rect(cx - 250f, height * 0.70f, 500f, 35f), "CHOOSE YOUR SUMO • CLICK THE COLOR WHEEL", small);
        if (GUI.Button(new Rect(cx - 250f, height * 0.81f, 190f, 58f), "BACK", quitButtonStyle)) BackFromSetup();
        string action = p == 0 && !playAgainstComputer ? "NEXT" : "PLAY";
        if (GUI.Button(new Rect(cx + 60f, height * 0.81f, 190f, 58f), action, primaryButtonStyle)) CompletePlayerSetup();
        string setupControls = p == 0
            ? "PLAYER 1: WASD   •   Q BOOST   •   E BRACE"
            : playAgainstComputer
                ? "CPU OPPONENT   •   ADAPTS EACH ROUND"
                : "PLAYER 2: ARROW KEYS   •   RIGHT CTRL BOOST   •   RIGHT SHIFT BRACE";
        GUI.Label(new Rect(cx - 360f, height * 0.92f, 720f, 25f), setupControls, small);
    }

    private void DrawInstructions(float width, float height, GUIStyle title, GUIStyle subtitle, GUIStyle body, GUIStyle small)
    {
        float cx = width * 0.5f;
        const float panelHeight = 650f;
        float panelTop = (height - panelHeight) * 0.5f;
        DrawPanel(width, height, 900f, panelHeight);
        GUI.Label(new Rect(cx - 400f, panelTop + 20f, 800f, 50f), "HOW TO PLAY", instructionsHeadingStyle);
        GUI.Label(new Rect(cx - 380f, panelTop + 70f, 760f, 28f), "THREE ROUNDS • LOCAL VS COMPUTER OR TWO PLAYERS", subtitleStyle);
        DrawInstructionRow(new Rect(cx - 375f, panelTop + 116f, 750f, 86f), new Color(0.34f, 0.82f, 1f),
            "ROUND 1  /  OPEN DOHYO", "Player 1 moves with WASD; player 2 uses arrow keys. Hold Q / Right Ctrl, then release to boost.");
        string roundTwoKeys = playAgainstComputer
            ? "Move keys are off. Wrestlers spin automatically; use Q / E. CPU matches keep the 2-second boost recovery."
            : "Move keys are off. P1: Q boost / A brace. P2: P boost / L brace. No recovery pause in rounds 2–3.";
        DrawInstructionRow(new Rect(cx - 375f, panelTop + 212f, 750f, 86f), new Color(1f, 0.56f, 0.30f),
            "ROUNDS 2 + 3  /  SPINNING DUELS", roundTwoKeys);
        DrawInstructionRow(new Rect(cx - 375f, panelTop + 308f, 750f, 86f), new Color(0.42f, 0.91f, 0.70f),
            "ROUND 3  /  BARRICADE DUEL", "Same spin and boost rules, with four fixed barriers. Use them for cover and find a route around.");
        string boostAndRingOut = playAgainstComputer
            ? "Boost lasts up to 2 seconds, then recovers for 2 seconds. A round ends when the whole wrestler clears the ring."
            : "Round 1 boost recovers for 2 seconds; rounds 2–3 have no recovery pause. A round ends when the whole wrestler clears the ring.";
        DrawInstructionRow(new Rect(cx - 375f, panelTop + 404f, 750f, 86f), new Color(0.95f, 0.77f, 0.32f),
            "BOOST + RING OUT", boostAndRingOut);
        if (GUI.Button(new Rect(cx - 130f, panelTop + 555f, 260f, 58f), "BACK", primaryButtonStyle)) gameState = GameState.MainMenu;
    }

    private void DrawInstructionRow(Rect rect, Color accent, string heading, string detail)
    {
        GUI.color = new Color(0.035f, 0.075f, 0.09f, 0.96f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = accent;
        GUI.DrawTexture(new Rect(rect.x, rect.y, 5f, rect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 22f, rect.y + 10f, rect.width - 42f, 28f), heading, instructionsSectionStyle);
        GUI.Label(new Rect(rect.x + 22f, rect.y + 39f, rect.width - 42f, 40f), detail, instructionsDetailStyle);
    }

    private void DrawDecorativePixels(float width, float height)
    {
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
        for (int i = 0; i < menuPixels.Length; i++)
        {
            Vector2 point = menuPixels[i];
            GUI.color = menuPixelColours[i];
            GUI.DrawTexture(new Rect(point.x * width, point.y * height, menuPixelSizes[i], menuPixelSizes[i]), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    private void DrawMatchHud(float width, float height, GUIStyle body, GUIStyle small)
    {
        GUI.Label(new Rect(24f, 18f, 360f, 42f), $"{playerNames[0]}  {scores[0]}", body);
        GUI.Label(new Rect(width - 384f, 18f, 360f, 42f), $"{playerNames[1]}  {scores[1]}", body);
        GUI.Label(new Rect(width * 0.5f - 220f, 20f, 440f, 42f), gameState == GameState.Countdown ? CountdownText() : $"ROUND {roundIndex + 1} / {ChampionshipRounds}  •  PUSH OUT", body);
        DrawChargeMeter(new Rect(44f, 68f, 260f, 16f), players[0].Controller.ChargeAmount, playerColours[0]);
        DrawChargeMeter(new Rect(width - 304f, 68f, 260f, 16f), players[1].Controller.ChargeAmount, playerColours[1]);
        string level = roundIndex == 0 ? "OPEN RING • STANDARD MOVEMENT" : roundIndex == 1 ? "SPINNING SUMOS • CHARGE TO DASH TOWARD YOUR HEAD" : "SPINNING DUEL • FOUR FIXED BOUNCE BARRICADES";
        string chargeRule = roundIndex >= 1 && !playAgainstComputer
            ? "P1: Q BOOST / A BRACE   •   P2: P BOOST / L BRACE   •   NO RECOVERY"
            : roundIndex >= 1 ? "BOOST MAX 2S • 2S COOLDOWN" : "Q / CTRL CHARGE";
        GUI.Label(new Rect(width * 0.5f - 430f, height - 43f, 860f, 28f), $"{level}   •   {chargeRule}   •   ESC MENU", small);
    }

    private string CountdownText()
    {
        int count = Mathf.CeilToInt(phaseRemaining - 0.4f);
        return count > 0 ? count.ToString() : "CLASH!";
    }

    private void DrawRoundResult(float width, float height, GUIStyle title, GUIStyle subtitle, GUIStyle body, GUIStyle small)
    {
        const float panelHeight = 500f;
        DrawPanel(width, height, 760f, panelHeight);
        float cx = width * 0.5f;
        float top = (height - panelHeight) * 0.5f;
        GUI.Label(new Rect(cx - 340f, top + 36f, 680f, 72f), isDraw ? "ROUND DRAW" : $"{playerNames[winner]} WINS!", instructionsHeadingStyle);
        GUI.Label(new Rect(cx - 330f, top + 118f, 660f, 42f), $"ROUND {roundIndex + 1} COMPLETE   •   SCORE {scores[0]} - {scores[1]}", subtitle);
        GUI.Label(new Rect(cx - 320f, top + 174f, 640f, 48f), winner >= 0 ? $"{playerNames[winner]} gains a small movement and dash bonus." : "No advantage this round.", body);
        string next = roundIndex + 1 < ChampionshipRounds ? "NEXT ROUND" : "CHAMPIONSHIP";
        if (GUI.Button(new Rect(cx - 165f, top + 246f, 330f, 62f), next, primaryButtonStyle)) AdvanceRound();
        if (GUI.Button(new Rect(cx - 140f, top + 328f, 280f, 54f), "MAIN MENU", quitButtonStyle)) gameState = GameState.MainMenu;
        GUI.Label(new Rect(cx - 270f, top + 414f, 540f, 28f), "Press ENTER for next round", small);
    }

    private void DrawChampionshipResult(float width, float height, GUIStyle title, GUIStyle subtitle, GUIStyle body, GUIStyle small)
    {
        const float panelHeight = 500f;
        DrawPanel(width, height, 760f, panelHeight);
        float cx = width * 0.5f;
        float top = (height - panelHeight) * 0.5f;
        string headline = scores[0] == scores[1] ? "CHAMPIONSHIP DRAW" : $"{playerNames[scores[0] > scores[1] ? 0 : 1]} IS CHAMPION!";
        GUI.Label(new Rect(cx - 350f, top + 48f, 700f, 72f), headline, instructionsHeadingStyle);
        GUI.Label(new Rect(cx - 300f, top + 142f, 600f, 46f), $"FINAL SCORE   {scores[0]}  —  {scores[1]}", subtitle);
        if (GUI.Button(new Rect(cx - 165f, top + 232f, 330f, 62f), "PLAY AGAIN", primaryButtonStyle)) StartPlayerSetup();
        if (GUI.Button(new Rect(cx - 140f, top + 322f, 280f, 54f), "MAIN MENU", quitButtonStyle)) gameState = GameState.MainMenu;
        GUI.Label(new Rect(cx - 270f, top + 414f, 540f, 28f), "Press ENTER to return to the menu", small);
    }

    private static GUIStyle MakeStyle(int fontSize, FontStyle fontStyle, TextAnchor alignment, Color colour)
    {
        return new GUIStyle(GUI.skin.label)
        {
            alignment = alignment,
            fontSize = fontSize,
            fontStyle = fontStyle,
            wordWrap = true,
            normal = { textColor = colour }
        };
    }

    private void EnsureGuiStyles()
    {
        if (standardButtonStyle != null)
        {
            return;
        }

        buttonTexture = CreateSolidTexture(new Color(0.08f, 0.16f, 0.19f, 1f));
        buttonHoverTexture = CreateSolidTexture(new Color(0.13f, 0.25f, 0.28f, 1f));
        buttonActiveTexture = CreateSolidTexture(new Color(0.20f, 0.34f, 0.34f, 1f));
        primaryTexture = CreateSolidTexture(new Color(0.83f, 0.52f, 0.13f, 1f));

        standardButtonStyle = CreateButtonStyle(18, Color.white, buttonTexture, buttonHoverTexture, buttonActiveTexture);
        primaryButtonStyle = CreateButtonStyle(20, new Color(0.07f, 0.08f, 0.07f), primaryTexture,
            CreateSolidTexture(new Color(1f, 0.70f, 0.22f, 1f)), CreateSolidTexture(new Color(0.70f, 0.40f, 0.08f, 1f)));
        arrowButtonStyle = CreateButtonStyle(32, new Color(0.88f, 0.72f, 0.43f), buttonTexture, buttonHoverTexture, buttonActiveTexture);
        instructionsButtonStyle = CreateButtonStyle(19, new Color(0.04f, 0.05f, 0.08f), CreateSolidTexture(new Color(0.24f, 0.50f, 0.90f)), CreateSolidTexture(new Color(0.34f, 0.64f, 1f)), CreateSolidTexture(new Color(0.15f, 0.36f, 0.72f)));
        quitButtonStyle = CreateButtonStyle(19, new Color(0.10f, 0.03f, 0.05f), CreateSolidTexture(new Color(0.88f, 0.28f, 0.38f)), CreateSolidTexture(new Color(1f, 0.40f, 0.50f)), CreateSolidTexture(new Color(0.68f, 0.17f, 0.26f)));
        transparentButtonStyle = new GUIStyle(GUI.skin.button) { normal = { background = CreateSolidTexture(Color.clear) }, hover = { background = CreateSolidTexture(new Color(1f, 1f, 1f, 0.08f)) }, active = { background = CreateSolidTexture(new Color(1f, 1f, 1f, 0.15f)) } };
        titleStyle = MakeStyle(92, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.94f, 1f, 0.93f));
        subtitleStyle = MakeStyle(19, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.73f, 0.81f, 0.86f));
        bodyStyle = MakeStyle(20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        smallStyle = MakeStyle(16, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.79f, 0.86f, 0.87f));
        playerCardTitleStyle = MakeStyle(20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        colourNameStyle = MakeStyle(16, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        playerCardTitleStyle = MakeStyle(36, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.56f, 0.91f, 1f));
        instructionsHeadingStyle = MakeStyle(40, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.92f, 1f, 0.95f));
        instructionsSectionStyle = MakeStyle(19, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.72f, 0.91f, 0.95f));
        instructionsDetailStyle = MakeStyle(16, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.78f, 0.84f, 0.87f));
    }

    private static GUIStyle CreateButtonStyle(int fontSize, Color textColour, Texture2D normal, Texture2D hover, Texture2D active)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            border = new RectOffset(12, 12, 10, 10),
            padding = new RectOffset(8, 8, 5, 5)
        };
        style.normal.background = normal;
        style.hover.background = hover;
        style.active.background = active;
        style.focused.background = hover;
        style.normal.textColor = textColour;
        style.hover.textColor = Color.white;
        style.active.textColor = Color.white;
        style.focused.textColor = Color.white;
        return style;
    }

    private static Texture2D CreateSolidTexture(Color colour)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point
        };
        texture.SetPixel(0, 0, colour);
        texture.Apply();
        return texture;
    }

    private static void DrawPanel(float width, float height, float panelWidth, float panelHeight)
    {
        Rect panel = new Rect((width - panelWidth) * 0.5f, (height - panelHeight) * 0.5f, panelWidth, panelHeight);
        GUI.color = new Color(0f, 0f, 0f, 0.34f);
        GUI.DrawTexture(new Rect(panel.x + 8f, panel.y + 10f, panel.width, panel.height), Texture2D.whiteTexture);
        GUI.color = new Color(0.025f, 0.055f, 0.075f, 0.97f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(0.83f, 0.52f, 0.13f, 0.95f);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 4f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private static void DrawChargeMeter(Rect rect, float amount, Color colour)
    {
        GUI.color = new Color(0.02f, 0.04f, 0.06f, 0.9f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = colour;
        GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * (amount / 1.2f), rect.height - 4f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}

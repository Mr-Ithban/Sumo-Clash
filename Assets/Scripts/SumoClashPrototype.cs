using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First playable Sumo Clash prototype. It creates its placeholder arena,
/// players and HUD at runtime, so SampleScene itself stays unchanged.
/// </summary>
public sealed class SumoClashPrototype : MonoBehaviour
{
    private const float ArenaRadius = 3.75f;
    private const float SafeRadius = 3.30f;
    private const float PlayerRadius = 0.45f;
    private const float MoveSpeed = 5f;
    private const float CountdownDuration = 3f;
    private const float ResultDuration = 3f;

    private readonly float[] roundTimes = { 90f, 75f, 60f, 45f };
    private readonly PlayerState[] players = new PlayerState[2];

    private Sprite circleSprite;
    private int roundIndex;
    private float timeRemaining;
    private float phaseRemaining;
    private GameState gameState;
    private string resultMessage = string.Empty;

    private enum GameState
    {
        Countdown,
        Playing,
        RoundResult,
        ChampionshipResult
    }

    private sealed class PlayerState
    {
        public Rigidbody2D Body;
        public int Score;
        public bool IsOut;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreatePrototype()
    {
        if (FindAnyObjectByType<SumoClashPrototype>() != null)
        {
            return;
        }

        new GameObject("Sumo Clash Prototype").AddComponent<SumoClashPrototype>();
    }

    private void Awake()
    {
        circleSprite = CreateCircleSprite();
        ConfigureCamera();
        CreateArena();
        CreatePlayers();
        StartChampionship();
    }

    private void Update()
    {
        if (gameState == GameState.Countdown)
        {
            phaseRemaining -= Time.deltaTime;
            if (phaseRemaining <= 0f)
            {
                gameState = GameState.Playing;
                timeRemaining = roundTimes[roundIndex];
            }
        }
        else if (gameState == GameState.Playing)
        {
            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f)
            {
                EndRound(-1, "Time up - round draw");
            }
        }
        else if (gameState == GameState.RoundResult)
        {
            phaseRemaining -= Time.deltaTime;
            if (phaseRemaining <= 0f)
            {
                BeginRound();
            }
        }
        else if (gameState == GameState.ChampionshipResult && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            StartChampionship();
        }
    }

    private void FixedUpdate()
    {
        if (gameState != GameState.Playing)
        {
            StopPlayers();
            return;
        }

        MovePlayer(players[0], ReadPlayerOneInput());
        MovePlayer(players[1], ReadPlayerTwoInput());

        players[0].IsOut = players[0].Body.position.magnitude > SafeRadius;
        players[1].IsOut = players[1].Body.position.magnitude > SafeRadius;

        if (players[0].IsOut || players[1].IsOut)
        {
            if (players[0].IsOut && players[1].IsOut)
            {
                EndRound(-1, "Simultaneous elimination - round draw");
            }
            else
            {
                int winner = players[0].IsOut ? 1 : 0;
                EndRound(winner, $"Player {winner + 1} wins the round!");
            }
        }
    }

    private void StartChampionship()
    {
        players[0].Score = 0;
        players[1].Score = 0;
        roundIndex = 0;
        BeginRound();
    }

    private void BeginRound()
    {
        foreach (PlayerState player in players)
        {
            player.IsOut = false;
        }

        players[0].Body.position = new Vector2(-1.5f, 0f);
        players[1].Body.position = new Vector2(1.5f, 0f);
        StopPlayers();

        resultMessage = string.Empty;
        phaseRemaining = CountdownDuration;
        gameState = GameState.Countdown;
    }

    private void EndRound(int winner, string message)
    {
        if (gameState != GameState.Playing)
        {
            return;
        }

        StopPlayers();
        if (winner >= 0)
        {
            players[winner].Score++;
        }

        resultMessage = message;
        roundIndex++;

        if (roundIndex >= roundTimes.Length)
        {
            gameState = GameState.ChampionshipResult;
            resultMessage = GetChampionshipMessage();
            return;
        }

        phaseRemaining = ResultDuration;
        gameState = GameState.RoundResult;
    }

    private string GetChampionshipMessage()
    {
        if (players[0].Score == players[1].Score)
        {
            return "Championship draw! Press R to play again.";
        }

        int winner = players[0].Score > players[1].Score ? 0 : 1;
        return $"Player {winner + 1} is the champion! Press R to play again.";
    }

    private static Vector2 ReadPlayerOneInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        return new Vector2(
            (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
            (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)).normalized;
    }

    private static Vector2 ReadPlayerTwoInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        return new Vector2(
            (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f),
            (keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed ? 1f : 0f)).normalized;
    }

    private static void MovePlayer(PlayerState player, Vector2 input)
    {
        player.Body.linearVelocity = input * MoveSpeed;
    }

    private void StopPlayers()
    {
        foreach (PlayerState player in players)
        {
            if (player?.Body != null)
            {
                player.Body.linearVelocity = Vector2.zero;
                player.Body.angularVelocity = 0f;
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
        camera.backgroundColor = new Color(0.055f, 0.075f, 0.11f);
    }

    private void CreateArena()
    {
        CreateCircleVisual("Arena Ring", new Color(0.95f, 0.73f, 0.18f), ArenaRadius, 0);
        CreateCircleVisual("Arena Floor", new Color(0.12f, 0.29f, 0.25f), ArenaRadius - 0.12f, 1);
        CreateCircleVisual("Safe Area", new Color(0.16f, 0.36f, 0.31f), SafeRadius, 2);
    }

    private void CreatePlayers()
    {
        players[0] = CreatePlayer("Player 1", new Color(0.25f, 0.65f, 1f), new Vector2(-1.5f, 0f));
        players[1] = CreatePlayer("Player 2", new Color(1f, 0.32f, 0.36f), new Vector2(1.5f, 0f));
    }

    private PlayerState CreatePlayer(string playerName, Color colour, Vector2 position)
    {
        GameObject playerObject = new GameObject(playerName);
        playerObject.transform.position = position;

        SpriteRenderer renderer = playerObject.AddComponent<SpriteRenderer>();
        renderer.sprite = circleSprite;
        renderer.color = colour;
        renderer.sortingOrder = 5;
        playerObject.transform.localScale = Vector3.one * (PlayerRadius * 2f);

        CircleCollider2D collider = playerObject.AddComponent<CircleCollider2D>();
        collider.sharedMaterial = new PhysicsMaterial2D("Sumo Contact")
        {
            friction = 0.35f,
            bounciness = 0f
        };

        Rigidbody2D body = playerObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.mass = 1f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        return new PlayerState { Body = body };
    }

    private void CreateCircleVisual(string objectName, Color colour, float radius, int sortingOrder)
    {
        GameObject visual = new GameObject(objectName);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = circleSprite;
        renderer.color = colour;
        renderer.sortingOrder = sortingOrder;
        visual.transform.localScale = Vector3.one * (radius * 2f);
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
                float distance = Vector2.Distance(new Vector2(x, y), centre);
                float alpha = Mathf.Clamp01(radius - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void OnGUI()
    {
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = 18,
            normal = { textColor = Color.white }
        };

        GUI.Label(new Rect(0f, 18f, Screen.width, 36f), "SUMO CLASH", titleStyle);
        GUI.Label(new Rect(0f, 58f, Screen.width, 28f), $"Player 1  {players[0].Score}  -  {players[1].Score}  Player 2", infoStyle);

        string centreText = gameState switch
        {
            GameState.Countdown => $"Round {roundIndex + 1} - {Mathf.CeilToInt(phaseRemaining)}",
            GameState.Playing => $"Round {roundIndex + 1} - {Mathf.CeilToInt(timeRemaining)} seconds",
            _ => resultMessage
        };
        GUI.Label(new Rect(0f, 92f, Screen.width, 32f), centreText, infoStyle);

        GUI.Label(new Rect(0f, Screen.height - 34f, Screen.width, 24f), "Player 1: WASD    |    Player 2: Arrow Keys", infoStyle);
    }
}

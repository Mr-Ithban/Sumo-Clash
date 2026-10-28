using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads movement keys for one local player and exposes one normalized direction.
/// It does not move the player itself.
/// </summary>
public sealed class PlayerInputHandler : MonoBehaviour
{
    public enum ControlScheme
    {
        Wasd,
        ArrowKeys,
        WasdAndArrows,
        Computer
    }

    private ControlScheme controlScheme;
    private bool spinningTwoPlayerControls;

    public Vector2 MoveInput { get; private set; }
    public bool ChargeHeld { get; private set; }
    public bool SlowHeld { get; private set; }
    private bool chargeReleaseBuffered;

    public void Configure(ControlScheme selectedControlScheme)
    {
        controlScheme = selectedControlScheme;
    }

    public void SetSpinningTwoPlayerControls(bool enabled)
    {
        spinningTwoPlayerControls = enabled;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            if (ChargeHeld)
            {
                chargeReleaseBuffered = true;
            }
            MoveInput = Vector2.zero;
            ChargeHeld = false;
            SlowHeld = false;
            return;
        }

        if (controlScheme == ControlScheme.Computer)
        {
            return;
        }

        if (controlScheme == ControlScheme.Wasd || controlScheme == ControlScheme.WasdAndArrows)
        {
            MoveInput = new Vector2(
                (keyboard.dKey.isPressed || controlScheme == ControlScheme.WasdAndArrows && keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || controlScheme == ControlScheme.WasdAndArrows && keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed || controlScheme == ControlScheme.WasdAndArrows && keyboard.upArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.sKey.isPressed || controlScheme == ControlScheme.WasdAndArrows && keyboard.downArrowKey.isPressed ? 1f : 0f)).normalized;
            bool charge = keyboard.qKey.isPressed || controlScheme == ControlScheme.WasdAndArrows && keyboard.rightCtrlKey.isPressed;
            bool brace = spinningTwoPlayerControls ? keyboard.aKey.isPressed : keyboard.eKey.isPressed;
            if (controlScheme == ControlScheme.WasdAndArrows && !spinningTwoPlayerControls)
            {
                brace |= keyboard.rightShiftKey.isPressed;
            }
            ReadAbilityKeys(charge, brace);
        }
        else
        {
            MoveInput = new Vector2(
                (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed ? 1f : 0f)).normalized;
            bool charge = spinningTwoPlayerControls ? keyboard.pKey.isPressed : keyboard.rightCtrlKey.isPressed;
            bool brace = spinningTwoPlayerControls ? keyboard.lKey.isPressed : keyboard.rightShiftKey.isPressed;
            ReadAbilityKeys(charge, brace);
        }
    }

    private void ReadAbilityKeys(bool chargeHeld, bool slowHeld)
    {
        if (ChargeHeld && !chargeHeld)
        {
            chargeReleaseBuffered = true;
        }

        ChargeHeld = chargeHeld;
        SlowHeld = slowHeld;
    }

    public bool ConsumeChargeRelease()
    {
        bool wasReleased = chargeReleaseBuffered;
        chargeReleaseBuffered = false;
        return wasReleased;
    }

    public void SetComputerInput(Vector2 move, bool chargeHeld, bool slowHeld)
    {
        if (controlScheme != ControlScheme.Computer)
        {
            return;
        }

        MoveInput = move.sqrMagnitude > 1f ? move.normalized : move;
        ReadAbilityKeys(chargeHeld, slowHeld);
    }
}

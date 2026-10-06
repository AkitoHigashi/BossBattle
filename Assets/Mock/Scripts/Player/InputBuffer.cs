using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class InputBuffer : MonoBehaviour
{
    public InputAction MoveAction => _moveAction;
    public InputAction InteractAction => _interactAction;
    public InputAction DodgeAction => _dodgeAction;
    public InputAction Skill1Action => _skill1Action;
    public InputAction Skill2Action => _skill2Action;
    public InputAction Skill3Action => _skill3Action;
    public InputAction Skill4Action => _skill4Action;
    public InputAction Skill5Action => _skill5Action;
    public InputAction Skill6Action => _skill6Action;
    public InputAction UltimateAction => _ultimeteAction;
    public InputAction UniqueAction => _uniqueAction;

    private const string MOVE_ACTION = "Move";
    private const string INTERACT_ACTION = "Interact";
    private const string DODGE_ACTION = "Dodge";
    private const string SKILL1_ACTION = "Skill1";
    private const string SKILL2_ACTION = "Skill2";
    private const string SKILL3_ACTION = "Skill3";
    private const string SKILL4_ACTION = "Skill4";
    private const string SKILL5_ACTION = "Skill5";
    private const string SKILL6_ACTION = "Skill6";
    private const string ULTIMETE_ACTION = "Ultimate";
    private const string UNIQUE_ACTION = "Unique";

    private InputAction _moveAction;
    private InputAction _interactAction;
    private InputAction _dodgeAction;
    private InputAction _skill1Action;
    private InputAction _skill2Action;
    private InputAction _skill3Action;
    private InputAction _skill4Action;
    private InputAction _skill5Action;
    private InputAction _skill6Action;
    private InputAction _ultimeteAction;
    private InputAction _uniqueAction;

    private bool _initialized;

    private void OnEnable() => SetActionsEnabled(true);
    private void OnDisable() => SetActionsEnabled(false);

    private void Awake()
    {
        if (_initialized) return;
        _initialized = true;
        if (TryGetComponent<PlayerInput>(out var playerInput))
        {
            _moveAction = playerInput.actions[MOVE_ACTION];
            _interactAction = playerInput.actions[INTERACT_ACTION];
            _dodgeAction = playerInput.actions[DODGE_ACTION];
            _skill1Action = playerInput.actions[SKILL1_ACTION];
            _skill2Action = playerInput.actions[SKILL2_ACTION];
            _skill3Action = playerInput.actions[SKILL3_ACTION];
            _skill4Action = playerInput.actions[SKILL4_ACTION];
            _skill5Action = playerInput.actions[SKILL5_ACTION];
            _skill6Action = playerInput.actions[SKILL6_ACTION];
            _ultimeteAction = playerInput.actions[ULTIMETE_ACTION];
            _uniqueAction = playerInput.actions[UNIQUE_ACTION];
            SetActionsEnabled(isActiveAndEnabled);
        }
    }

    private void SetActionsEnabled(bool value)
    {
        SetActionEnabled(_moveAction, value);
        SetActionEnabled(_interactAction, value);
        SetActionEnabled(_dodgeAction, value);
        SetActionEnabled(_skill1Action, value);
        SetActionEnabled(_skill2Action, value);
        SetActionEnabled(_skill3Action, value);
        SetActionEnabled(_skill4Action, value);
        SetActionEnabled(_skill5Action, value);
        SetActionEnabled(_skill6Action, value);
        SetActionEnabled(_ultimeteAction, value);
        SetActionEnabled(_uniqueAction, value);
    }

    private static void SetActionEnabled(InputAction action, bool value)
    {
        if (action == null) return;
        if (value) action.Enable();
        else action.Disable();
    }
}
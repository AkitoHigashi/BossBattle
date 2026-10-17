using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Only assigned by GameInitializer in scn_skillTest.
public class SkillTestInput : MonoBehaviour
{
    [SerializeField] private SkillDefinition _normalAttack;
    [SerializeField] private SkillDefinition _blinkSlash;
    private SkillSystem _skills;
    private InputAction _attackAction;
    private InputAction _blinkAction;
    private bool _subscribed;

    public void Initialize(InputBuffer input, SkillSystem skills)
    {
        if (_skills != null) return;
        if (input == null || !input.IsInitialized || skills == null || _normalAttack == null || _blinkSlash == null)
            throw new InvalidOperationException("SkillTestInputの依存関係またはスキルSOが未設定です。");
        _attackAction = input.AttackAction;
        _blinkAction = input.Skill1Action;
        if (_attackAction == null || _blinkAction == null)
            throw new InvalidOperationException("Attack/Skill1のInputActionを設定してください。");
        _skills = skills;
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable()
    {
        if (_skills != null) Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        _attackAction.performed += OnAttack;
        _blinkAction.performed += OnBlink;
        _subscribed = true;
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        Use(_normalAttack);
    }

    private void OnBlink(InputAction.CallbackContext context)
    {
        Use(_blinkSlash);
    }

    private void Use(SkillDefinition definition)
    {
        Vector3 direction = this.transform.forward;
        bool accepted = _skills.TryUseSkill(definition, direction);
        Debug.Log($"[SkillTest] {definition.SkillName}: accepted={accepted}, cooldown={_skills.GetCooldownRemaing(definition):0.00}s", this);
    }

    private void OnDisable()
    {
        if (_subscribed)
        {
            _attackAction.performed -= OnAttack;
            _blinkAction.performed -= OnBlink;
            _subscribed = false;
        }
        _skills?.CancelActiveSkill();
    }
}
